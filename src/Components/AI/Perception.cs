using Godot;

namespace Contenda.Components.AI;

/// <summary>Detecta se um alvo está visível: dentro de um raio, com linha de visão livre.</summary>
/// <remarks>
/// Não é um <c>ICharacterComponent</c>: não precisa de <c>Bind</c>/<c>Configure</c>
/// próprios. Usa o <see cref="RayCast3D"/> reutilizado pelo <c>EnemyBrain</c>;
/// evita uma consulta que devolve <c>Godot.Collections.Dictionary</c> em cada
/// pensamento. Ver spec 09 §3.
///
/// Não tenta ser um `ICharacterComponent` nem um `Node` na árvore de
/// propósito: é dono só do `EnemyBrain`, do mesmo jeito que `MeleeCombo` é
/// dono só do `MeleeWeapon` — ninguém mais precisa alcançar isto ainda.
/// </remarks>
public sealed class Perception
{
    private readonly RayCast3D _raycast;

    public Perception(RayCast3D raycast)
    {
        _raycast = raycast;
    }

    /// <summary>
    /// Se <paramref name="alvo"/> está dentro de <paramref name="raio"/> de
    /// <paramref name="origem"/> E com linha de visão livre até ele.
    /// </summary>
    public bool Visivel(Vector3 origem, Vector3 alvo, float raio, uint mascaraDeParede)
    {
        var ate = alvo - origem;
        if (ate.LengthSquared() > raio * raio)
            return false;

        return LinhaDeVisaoLivre(origem, alvo, mascaraDeParede);
    }

    private bool LinhaDeVisaoLivre(Vector3 origem, Vector3 alvo, uint mascara)
    {
        _raycast.GlobalPosition = origem;
        _raycast.TargetPosition = _raycast.ToLocal(alvo);
        _raycast.CollisionMask = mascara;
        _raycast.ForceRaycastUpdate();
        return !_raycast.IsColliding();
    }
}
