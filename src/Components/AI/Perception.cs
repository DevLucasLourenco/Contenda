using Godot;

namespace Contenda.Components.AI;

/// <summary>Detecta se um alvo está visível: dentro de um raio, com linha de visão livre.</summary>
/// <remarks>
/// Não é um <c>ICharacterComponent</c>: não precisa de <c>Bind</c>/<c>Configure</c>
/// próprios, só de uma referência ao <c>Node</c> dono para o raycast — mesma
/// fronteira com a engine que <c>MeleeWeapon</c> já usa para <c>GetTree()</c>,
/// e o mesmo raycast que <c>HitscanWeapon.AlcanceAteParede</c> já faz contra
/// <see cref="Contenda.Core.PhysicsLayers.World"/>. Ver spec 09 §3.
///
/// Não tenta ser um `ICharacterComponent` nem um `Node` na árvore de
/// propósito: é dono só do `EnemyBrain`, do mesmo jeito que `MeleeCombo` é
/// dono só do `MeleeWeapon` — ninguém mais precisa alcançar isto ainda.
/// </remarks>
public sealed class Perception
{
    private readonly Node _dono;

    public Perception(Node dono)
    {
        _dono = dono;
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
        var espaco = _dono.GetViewport().World3D.DirectSpaceState;
        var parametros = PhysicsRayQueryParameters3D.Create(origem, alvo, mascara);

        // Fronteira com a engine: Godot.Collections.Dictionary só aqui.
        var resultado = espaco.IntersectRay(parametros);
        return resultado.Count == 0;
    }
}
