using Godot;

namespace Contenda.Weapons;

/// <summary>
/// Tambor, cadência e recarga automática de uma arma hitscan.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>MeleeCombo</c>: cadência e recarga são janelas
/// de tempo, e testá-las exige controlar o relógio fora da engine. Ver ticket
/// 09 e spec 07 §5.
///
/// O intervalo entre tiros é passado a cada <see cref="TryFire"/>, não fixado
/// na construção: <c>AttackSpeed</c> do <c>StatBlock</c> divide o
/// <c>AttackInterval</c> da arma, e quem calcula essa divisão é o
/// <c>HitscanWeapon</c> a cada tiro — fixá-lo aqui faria o atributo parar de
/// ter efeito depois do primeiro disparo.
/// </remarks>
public sealed class RevolverState
{
    private readonly int _capacidade;
    private readonly float _reloadTime;
    private readonly bool _infiniteAmmo;

    private float _cooldownRestante;
    private float _reloadRestante;

    public RevolverState(int capacidade, float reloadTime, bool infiniteAmmo = false)
    {
        _capacidade = Mathf.Max(1, capacidade);
        _reloadTime = reloadTime;
        _infiniteAmmo = infiniteAmmo;
        Rounds = _capacidade;
    }

    /// <summary>Cartuchos restantes no tambor.</summary>
    public int Rounds { get; private set; }

    /// <summary>Se está recarregando.</summary>
    public bool IsReloading => _reloadRestante > 0f;

    /// <summary>Se um tiro agora seria aceito.</summary>
    public bool CanFire => !IsReloading && _cooldownRestante <= 0f && (_infiniteAmmo || Rounds > 0);

    /// <summary>Envelhece a cadência e a recarga pelo quadro.</summary>
    public void Advance(float delta)
    {
        if (_cooldownRestante > 0f)
            _cooldownRestante -= delta;

        if (_reloadRestante <= 0f)
            return;

        _reloadRestante -= delta;
        if (_reloadRestante <= 0f)
        {
            _reloadRestante = 0f;
            Rounds = _capacidade;
        }
    }

    /// <summary>
    /// Tenta disparar. Esvaziar o tambor inicia a recarga automaticamente.
    /// </summary>
    /// <param name="intervalo">
    /// Tempo mínimo até o próximo tiro, já com <c>AttackSpeed</c> aplicado.
    /// </param>
    /// <returns>Se o tiro saiu.</returns>
    public bool TryFire(float intervalo)
    {
        if (!CanFire)
            return false;

        if (!_infiniteAmmo)
            Rounds--;
        _cooldownRestante = intervalo;

        if (!_infiniteAmmo && Rounds <= 0)
            _reloadRestante = _reloadTime;

        return true;
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        Rounds = _capacidade;
        _cooldownRestante = 0f;
        _reloadRestante = 0f;
    }
}
