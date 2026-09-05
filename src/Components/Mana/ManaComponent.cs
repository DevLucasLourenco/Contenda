using System;
using Contenda.Characters.Base;
using Contenda.Components.Stats;
using Godot;

namespace Contenda.Components.Mana;

/// <summary>
/// Casca de nó sobre o <see cref="ManaState"/>.
/// </summary>
/// <remarks>
/// Não contém lógica de consumo ou regeneração: tudo isso mora no estado, que
/// é POCO e testável. Aqui só ficam o `[Export]`, a leitura de
/// <c>ManaRegen</c> do <see cref="StatsComponent"/> e o repasse de eventos —
/// mesmo desenho do <c>HealthComponent</c>.
/// </remarks>
public sealed partial class ManaComponent : Node, ICharacterComponent
{
    /// <summary>Valores de mana, quando a definição do personagem não traz os seus.</summary>
    [Export] public ManaDefinition? Fallback { get; set; }

    private ManaState _estado = new(100f, 1f);
    private StatsComponent? _stats;

    /// <summary>Mana atual.</summary>
    public float Current => _estado.Current;

    /// <summary>Mana máxima efetiva.</summary>
    public float Max => _estado.Max;

    /// <summary>Fração de mana, de 0 a 1.</summary>
    public float Percent => _estado.Percent;

    /// <summary>Avisa que a mana mudou, com o valor atual e o máximo.</summary>
    public event Action<float, float>? ManaChanged;

    /// <summary>Avisa que a mana chegou a zero.</summary>
    public event Action? Depleted;

    // --- ciclo de vida -------------------------------------------------------

    public void Bind(CharacterContext contexto)
    {
        // Desassinar antes de assinar: Bind pode rodar de novo num nó reciclado
        // pelo pool, e assinar duas vezes dobraria todo aviso.
        if (_stats is not null)
            _stats.StatChanged -= AoMudarAtributo;

        _stats = contexto.Stats;

        if (_stats is not null)
            _stats.StatChanged += AoMudarAtributo;
    }

    public void Configure(CharacterDefinition definicao)
    {
        var valores = definicao.Mana ?? Fallback ?? new ManaDefinition();

        TrocarEstado(new ManaState(valores.MaxMana, valores.RegenDelayAfterSpend, valores.StartingManaPercent));

        // A mana máxima passa pelos ATRIBUTOS, e não direto para o estado: é
        // assim que uma transformação que aumenta a mana máxima, no M4, chega
        // até aqui sem ninguém ligar os dois à mão. Ver HealthComponent.
        _stats?.SetBase(StatId.MaxMana, valores.MaxMana);
        _stats?.SetBase(StatId.ManaRegen, valores.RegenPerSecond);
    }

    public override void _ExitTree()
    {
        if (_stats is not null)
            _stats.StatChanged -= AoMudarAtributo;

        DesassinarEstado();
    }

    // --- API -----------------------------------------------------------------

    /// <summary>Se um <see cref="TryConsume"/> deste valor passaria agora.</summary>
    public bool CanConsume(float quanto) => _estado.CanConsume(quanto);

    /// <summary>Gasto atômico. Habilidades usam este — sem saldo, nada muda.</summary>
    public bool TryConsume(float quanto) => _estado.TryConsume(quanto);

    /// <summary>Dreno parcial. Transformações usam este — nunca falha.</summary>
    public float Drain(float quantoPorQuadro) => _estado.Drain(quantoPorQuadro);

    /// <summary>Devolve mana, sem passar do máximo.</summary>
    public void Restore(float quanto) => _estado.Restore(quanto);

    /// <summary>
    /// Envelhece a pausa de regeneração e regenera. Chamado pelo contêiner.
    /// </summary>
    public void Tick(float delta) => _estado.Advance(delta, _stats?.Get(StatId.ManaRegen) ?? 0f);

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn() => _estado.Reset();

    // --- privados ------------------------------------------------------------

    private void TrocarEstado(ManaState novo)
    {
        DesassinarEstado();
        _estado = novo;
        _estado.ManaChanged += RepassarMudanca;
        _estado.Depleted += RepassarDepleted;
    }

    private void DesassinarEstado()
    {
        _estado.ManaChanged -= RepassarMudanca;
        _estado.Depleted -= RepassarDepleted;
    }

    private void AoMudarAtributo(StatId stat, float valor)
    {
        if (stat == StatId.MaxMana)
            _estado.SetMax(valor);
    }

    private void RepassarMudanca(float atual, float max) => ManaChanged?.Invoke(atual, max);

    private void RepassarDepleted() => Depleted?.Invoke();
}
