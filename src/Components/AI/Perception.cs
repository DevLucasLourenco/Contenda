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

    /// <remarks>
    /// Reaproveitado entre chamadas, nunca recriado -- um por inimigo, para
    /// a vida inteira dele, em vez de um `PhysicsRayQueryParameters3D.Create`
    /// novo a cada quadro (ticket 23: com uma horda inteira em `Chase` ao
    /// mesmo tempo, cada um chamando isto todo quadro, o volume de objetos
    /// `RefCounted` de vida curtíssima expunha um "Leaked unsafe reference"
    /// do runtime C#/Godot em desligamento -- benigno isolado, mas
    /// observável demais para ignorar nesta escala).
    /// </remarks>
    private PhysicsRayQueryParameters3D? _parametros;

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

        _parametros ??= PhysicsRayQueryParameters3D.Create(origem, alvo, mascara);
        _parametros.From = origem;
        _parametros.To = alvo;
        _parametros.CollisionMask = mascara;

        // Fronteira com a engine: Godot.Collections.Dictionary só aqui.
        var resultado = espaco.IntersectRay(_parametros);
        return resultado.Count == 0;
    }
}
