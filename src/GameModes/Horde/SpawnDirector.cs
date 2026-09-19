using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Core;
using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>
/// Escolhe ONDE um inimigo nasce, e materializa o nascimento (marcador no
/// chão, depois o inimigo de verdade, invulnerável por um instante).
/// </summary>
/// <remarks>
/// Ver docs/specs/10-modos-de-jogo-horde.md §6. Quem decide QUANTOS e QUANDO
/// pedir é o <see cref="WaveDirector"/> -- este nó só sabe materializar UM
/// pedido de cada vez, sem noção de onda nem de composição.
///
/// Pontos de spawn vêm dos filhos de <see cref="SpawnPointsPath"/>
/// (`SpawnPoints/Spawn1..8` em Arena.tscn, ticket 21) -- não um array de
/// `NodePath` um a um: Godot C# não exporta bem array de referência a nó, e
/// enumerar os filhos de um container é o idiomatismo natural para "aqui
/// estão os candidatos".
/// </remarks>
public sealed partial class SpawnDirector : Node
{
    /// <summary>Container cujos filhos <see cref="Marker3D"/> são os pontos de spawn candidatos.</summary>
    [Export] public NodePath SpawnPointsPath { get; set; } = new();

    /// <summary>A câmera de combate, para o teste de frustum. Ver spec 10 §6.</summary>
    [Export] public NodePath CameraPath { get; set; } = new();

    /// <summary>Distância mínima do jogador para um ponto valer.</summary>
    [Export(PropertyHint.Range, "1,20,0.5")] public float MinDistanceFromPlayer { get; set; } = 12f;

    /// <summary>Distância máxima do jogador para um ponto valer.</summary>
    [Export(PropertyHint.Range, "5,60,0.5")] public float MaxDistanceFromPlayer { get; set; } = 30f;

    /// <summary>Peso do ponto usado por último, contra os demais (peso 1,0). Menor que 1 desencoraja repetir.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float LastUsedWeight { get; set; } = 0.15f;

    /// <summary>Quanto tempo o marcador no chão fica visível antes do inimigo aparecer. Spec 10 §6.</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float TelegraphSeconds { get; set; } = 0.4f;

    /// <summary>Invulnerabilidade concedida ao inimigo recém-nascido. Spec 10 §6.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float SpawnProtectionSeconds { get; set; } = 0.5f;

    private readonly List<Marker3D> _pontos = [];
    private int _ultimoIndiceUsado = -1;

    private readonly record struct SpawnPendente(EnemyDefinition Definicao, Vector3 Posicao, float Restante);
    private readonly List<SpawnPendente> _pendentes = [];

    private Camera3D? _camera;
    private EnemyPool? _pool;

    public override void _Ready()
    {
        var container = GetNodeOrNull<Node>(SpawnPointsPath);
        if (container is not null)
        {
            foreach (var filho in container.GetChildren())
            {
                if (filho is Marker3D marcador)
                    _pontos.Add(marcador);
            }
        }

        _camera = GetNodeOrNull<Camera3D>(CameraPath);
        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");

        if (_pontos.Count == 0 || _camera is null || _pool is null)
            GD.PushError($"{Name}: SpawnPointsPath, CameraPath ou EnemyPool não resolveram.");
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = _pendentes.Count - 1; i >= 0; i--)
        {
            var pendente = _pendentes[i];
            var restante = pendente.Restante - (float)delta;

            if (restante > 0f)
            {
                _pendentes[i] = pendente with { Restante = restante };
                continue;
            }

            Materializar(pendente.Definicao, pendente.Posicao);
            _pendentes.RemoveAt(i);
        }
    }

    /// <summary>Pede um nascimento: escolhe um ponto, acende o marcador, e agenda o inimigo de verdade.</summary>
    /// <remarks>
    /// Não devolve o <see cref="CharacterController"/> criado -- ele só passa
    /// a existir depois de <see cref="TelegraphSeconds"/>, e "quem chamou
    /// segura uma referência a algo que ainda nem nasceu" seria mais um jeito
    /// de errar do que um recurso de verdade: o <see cref="WaveDirector"/>
    /// já não precisa da instância, só de contar via <see cref="GameEvents.EnemyKilled"/>.
    /// </remarks>
    public void RequestSpawn(EnemyDefinition definicao)
    {
        ArgumentNullException.ThrowIfNull(definicao);

        var jogador = ServiceLocator.Session.PlayerBody;
        if (jogador is null || _pontos.Count == 0)
            return;

        var posicao = EscolherPonto(jogador.GlobalPosition);

        ServiceLocator.Events.RaiseSpawnMarker(new SpawnMarkerEvent(posicao, TelegraphSeconds));
        _pendentes.Add(new SpawnPendente(definicao, posicao, TelegraphSeconds));
    }

    private void Materializar(EnemyDefinition definicao, Vector3 posicao)
    {
        var inimigo = _pool?.Acquire(definicao, posicao);
        inimigo?.Context?.Health?.GrantInvulnerability(SpawnProtectionSeconds);
    }

    /// <remarks>
    /// Três passadas, relaxando uma regra de cada vez -- spec 10 §6: (1)
    /// dentro de [min, max] do jogador E fora do frustum da câmera; (2) se
    /// nada sobrar, só a distância; (3) se AINDA assim nada sobrar (mapa
    /// pequeno demais, ou jogador num canto), qualquer ponto conhecido.
    /// "Nunca falha em spawnar" é literal na spec -- por isso o último
    /// degrau nunca fica vazio enquanto houver ALGUM ponto cadastrado.
    /// </remarks>
    private Vector3 EscolherPonto(Vector3 posicaoDoJogador)
    {
        var candidatos = new List<int>(_pontos.Count);

        for (var i = 0; i < _pontos.Count; i++)
        {
            var distancia = _pontos[i].GlobalPosition.DistanceTo(posicaoDoJogador);
            if (!SpawnPointMath.IsDistanceValid(distancia, MinDistanceFromPlayer, MaxDistanceFromPlayer))
                continue;

            if (_camera is not null && _camera.IsPositionInFrustum(_pontos[i].GlobalPosition))
                continue;

            candidatos.Add(i);
        }

        if (candidatos.Count == 0)
        {
            for (var i = 0; i < _pontos.Count; i++)
            {
                var distancia = _pontos[i].GlobalPosition.DistanceTo(posicaoDoJogador);
                if (SpawnPointMath.IsDistanceValid(distancia, MinDistanceFromPlayer, MaxDistanceFromPlayer))
                    candidatos.Add(i);
            }
        }

        if (candidatos.Count == 0)
        {
            for (var i = 0; i < _pontos.Count; i++)
                candidatos.Add(i);
        }

        var pesos = new List<float>(candidatos.Count);
        foreach (var indice in candidatos)
            pesos.Add(indice == _ultimoIndiceUsado ? LastUsedWeight : 1f);

        var escolhido = candidatos[SpawnPointMath.ChooseWeightedIndex(pesos, GD.Randf())];
        _ultimoIndiceUsado = escolhido;

        return _pontos[escolhido].GlobalPosition;
    }
}
