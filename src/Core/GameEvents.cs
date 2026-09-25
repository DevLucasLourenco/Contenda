using System;
using Godot;

namespace Contenda.Core;

/// <summary>
/// Um golpe conectou e precisa de um número flutuante — em quem, quanto e se
/// foi crítico.
/// </summary>
/// <param name="Position">Onde mostrar o número, no mundo.</param>
/// <param name="Amount">Quanto de dano, já com mitigação aplicada.</param>
/// <param name="IsCritical">Se foi crítico. Ticket 18, spec 16 §5.</param>
public readonly record struct DamageNumberEvent(Vector3 Position, float Amount, bool IsCritical);

/// <summary>Um projétil de habilidade precisa nascer, sem que quem pediu segure o nó.</summary>
/// <param name="Origin">De onde o projétil parte.</param>
/// <param name="Direction">Direção do voo, já normalizada e no plano horizontal.</param>
/// <param name="Speed">Velocidade de voo, em metros por segundo.</param>
/// <param name="LifeTime">Quanto tempo voa antes de se autodestruir sem acertar nada.</param>
/// <param name="Damage">Dano da explosão, já com multiplicadores aplicados.</param>
/// <param name="ExplosionRadius">Raio da explosão — tanto o gatilho de impacto quanto o de área.</param>
/// <param name="MaxTargets">Quantos alvos a explosão atinge. Zero é ilimitado.</param>
/// <param name="Knockback">Repulsão aplicada a cada alvo atingido.</param>
/// <param name="SourceId">Identidade de quem disparou, sem manter o objeto vivo.</param>
/// <param name="SourceTag">O que disparou: `ability.explosive_shot`.</param>
/// <param name="ShooterTeam">Time de quem disparou. Decide quem a explosão pode ferir.</param>
/// <param name="TargetGroup">Grupo varrido em busca de alvos ao explodir.</param>
/// <param name="IsCritical">
/// Se o disparo saiu crítico. Sorteado no disparo, não na explosão -- a
/// explosão em área reaproveita o mesmo valor para todo mundo que ela
/// atinge, mesma disciplina de um golpe corpo a corpo em área. Ticket 18.
/// </param>
public readonly record struct ProjectileFireEvent(
    Vector3 Origin,
    Vector3 Direction,
    float Speed,
    float LifeTime,
    float Damage,
    float ExplosionRadius,
    int MaxTargets,
    float Knockback,
    ulong SourceId,
    string SourceTag,
    Team ShooterTeam,
    StringName TargetGroup,
    bool IsCritical);

/// <summary>
/// Um inimigo morreu. Sem identidade nem posição de propósito -- os
/// assinantes (<c>WaveDirector</c>, ticket 27; <c>HordeGameMode</c>, ticket
/// 28) só precisam CONTAR, nunca SABER QUEM; spec 09 §4/10 §4 é explícita que
/// a contagem é "por evento EnemyKilled do GameEvents, não por varredura de
/// cena a cada frame". Campos entram quando um assinante de verdade precisar
/// deles (pontuação, ticket 29, por exemplo) -- adicioná-los antes seria dado
/// para uma necessidade que ainda não existe.
/// </summary>
/// <param name="IsBoss">
/// Se quem morreu era o chefe -- é o que faz o <c>WaveDirector</c> parar os
/// reforços de uma onda de chefe (ticket 28).
/// </param>
/// <param name="ScoreValue">Quanto o abate vale, antes dos multiplicadores (ticket 29).</param>
public readonly record struct EnemyKilledEvent(bool IsBoss, int ScoreValue);

/// <summary>O placar mudou -- para o HUD reagir na hora, sem varrer nada (ticket 29).</summary>
/// <param name="Score">Pontos totais.</param>
/// <param name="ComboMultiplier">Multiplicador de combo agora (1,0 sem sequência).</param>
public readonly record struct ScoreChangedEvent(int Score, float ComboMultiplier);

/// <summary>A partida terminou -- para a tela de resultado (ticket 29).</summary>
/// <param name="Result">O que sobrou da partida.</param>
public readonly record struct MatchEndedEvent(Contenda.GameModes.GameModeResult Result);

/// <summary>O carregamento de uma cena andou -- para a tela de carregamento mostrar progresso de verdade (ticket 30).</summary>
/// <param name="Progress">De 0 a 1, o que o <c>ResourceLoader</c> reporta.</param>
/// <param name="Loading">Falso quando o carregamento acabou (ou nunca começou) e a tela deve sumir.</param>
public readonly record struct SceneLoadProgressEvent(float Progress, bool Loading);

/// <summary>Uma onda começou e precisa anunciar o próprio nome na tela.</summary>
/// <param name="DisplayName">O texto do banner, ex.: "ONDA 3".</param>
public readonly record struct WaveAnnouncedEvent(string DisplayName);

/// <summary>Um marcador de nascimento de inimigo precisa aparecer no chão, antes dele surgir de verdade.</summary>
/// <param name="Position">Onde, no mundo.</param>
/// <param name="Duration">Quanto tempo o marcador fica visível antes do inimigo aparecer.</param>
public readonly record struct SpawnMarkerEvent(Vector3 Position, float Duration);

/// <summary>
/// Barramento para eventos entre sistemas sem relação direta — o abate de um
/// inimigo chegando ao placar, por exemplo.
/// </summary>
/// <remarks>
/// Regra do projeto: este barramento transporta **apenas dados por valor**
/// (<c>readonly record struct</c>), nunca referências a nós. Um evento que
/// carrega um nó mantém vivo algo que o pool achou que tinha reciclado.
///
/// Comunicação dentro do mesmo personagem NÃO passa por aqui: usa chamada direta
/// pelo <c>CharacterContext</c> ou <c>event</c> do próprio componente.
/// Ver docs/specs/01-arquitetura-tecnica.md §5.
///
/// Os eventos concretos entram conforme os sistemas que os disparam — o de
/// abate chega no M6, com o modo horda. Adicioná-los antes seria abstração para
/// necessidade que a spec ainda não tem. <see cref="DamageNumberEvent"/> é o
/// primeiro: o número flutuante do ticket 11 é pooled e genérico — não sabe
/// (nem devia saber) o que é uma arma, um `CombatComponent` ou um `HealthState`.
/// </remarks>
public sealed partial class GameEvents : Node
{
    /// <summary>Avisa que um golpe conectou em algum lugar do mundo.</summary>
    public event Action<DamageNumberEvent>? DamageNumberRequested;

    /// <summary>Avisa que um projétil de habilidade precisa nascer.</summary>
    public event Action<ProjectileFireEvent>? ProjectileFireRequested;

    /// <summary>Avisa que um inimigo morreu. Ticket 27.</summary>
    public event Action<EnemyKilledEvent>? EnemyKilled;

    /// <summary>Avisa que uma onda começou, para o banner na tela. Ticket 27.</summary>
    public event Action<WaveAnnouncedEvent>? WaveAnnounced;

    /// <summary>Avisa que um marcador de nascimento precisa aparecer no chão. Ticket 27.</summary>
    public event Action<SpawnMarkerEvent>? SpawnMarkerRequested;

    /// <summary>Avisa o andamento do carregamento de uma cena. Ticket 30.</summary>
    public event Action<SceneLoadProgressEvent>? SceneLoadProgress;

    /// <summary>Avisa que o placar mudou. Ticket 29.</summary>
    public event Action<ScoreChangedEvent>? ScoreChanged;

    /// <summary>Avisa que a partida terminou. Ticket 29.</summary>
    public event Action<MatchEndedEvent>? MatchEnded;

    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] GameEvents pronto");
    }

    /// <summary>Dispara <see cref="DamageNumberRequested"/>.</summary>
    public void RaiseDamageNumber(in DamageNumberEvent evento) => DamageNumberRequested?.Invoke(evento);

    /// <summary>Dispara <see cref="ProjectileFireRequested"/>.</summary>
    public void RaiseProjectileFire(in ProjectileFireEvent evento) => ProjectileFireRequested?.Invoke(evento);

    /// <summary>Dispara <see cref="EnemyKilled"/>.</summary>
    public void RaiseEnemyKilled(in EnemyKilledEvent evento) => EnemyKilled?.Invoke(evento);

    /// <summary>Dispara <see cref="SceneLoadProgress"/>.</summary>
    public void RaiseSceneLoadProgress(in SceneLoadProgressEvent evento) => SceneLoadProgress?.Invoke(evento);

    /// <summary>Dispara <see cref="ScoreChanged"/>.</summary>
    public void RaiseScoreChanged(in ScoreChangedEvent evento) => ScoreChanged?.Invoke(evento);

    /// <summary>Dispara <see cref="MatchEnded"/>.</summary>
    public void RaiseMatchEnded(in MatchEndedEvent evento) => MatchEnded?.Invoke(evento);

    /// <summary>Dispara <see cref="WaveAnnounced"/>.</summary>
    public void RaiseWaveAnnounced(in WaveAnnouncedEvent evento) => WaveAnnounced?.Invoke(evento);

    /// <summary>Dispara <see cref="SpawnMarkerRequested"/>.</summary>
    public void RaiseSpawnMarker(in SpawnMarkerEvent evento) => SpawnMarkerRequested?.Invoke(evento);
}
