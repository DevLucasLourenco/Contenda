using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Core;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>Varre um grupo em busca de alvos válidos para uma habilidade.</summary>
/// <remarks>
/// "Alvo válido" (não é eu, não é do meu time, está vivo) se repetia quase
/// idêntico em <c>MeleeArcBehavior</c>, <c>UppercutBehavior</c>,
/// <c>HitscanShotBehavior</c>, <c>HitscanBurstBehavior</c> e no
/// <c>ProjectilePool</c> — cinco cópias no mesmo ticket. Depende de
/// <c>SceneTree</c> e <c>CharacterController</c>, então, ao contrário de
/// <see cref="AbilityGeometry"/>, não dá para testar em xUnit; é o mesmo tipo
/// de fronteira com a engine que o <c>MeleeWeapon.AmostrarAlvos</c> já tem.
/// </remarks>
public static class AbilityTargeting
{
    /// <summary>
    /// Varre <paramref name="grupo"/> e chama <paramref name="paraCadaAlvo"/> para cada alvo válido.
    /// </summary>
    /// <remarks>
    /// Sem alocar uma lista para devolver: quem chama decide o que fazer com
    /// cada alvo (medir distância, ângulo, aplicar dano) sem que esta função
    /// precise conhecer nenhuma dessas regras específicas de <c>Kind</c>.
    /// <paramref name="paraCadaAlvo"/> devolve <c>false</c> para parar a
    /// varredura na hora -- é o que permite a um <c>Kind</c> com
    /// <c>MaxTargets</c> encerrar cedo sem esta função saber que
    /// <c>MaxTargets</c> existe.
    /// </remarks>
    public static void ForEachValidTarget(
        SceneTree arvore,
        StringName grupo,
        CharacterBody3D? self,
        Team selfTeam,
        Func<CharacterController, bool> paraCadaAlvo)
    {
        // Fronteira com a engine: GetNodesInGroup só é chamado daqui, e só uma
        // vez por resolução -- nunca dentro de um laço por quadro. Convenções §5.
        foreach (var no in arvore.GetNodesInGroup(grupo))
        {
            if (!IsValidTarget(no, self, selfTeam, out var alvo))
                continue;

            if (!paraCadaAlvo(alvo!))
                return;
        }
    }

    /// <summary>
    /// Filtra uma lista já amostrada (por exemplo, o cache de um projétil em
    /// voo) em vez de varrer o grupo de novo.
    /// </summary>
    /// <remarks>
    /// Ainda reconfere time/vida a cada chamada -- um alvo do cache pode ter
    /// morrido ou trocado de time entre o momento em que foi amostrado e agora
    /// -- só não repete a varredura de <c>GetNodesInGroup</c> em si, que é a
    /// parte que aloca.
    /// </remarks>
    public static void ForEachValidTarget(
        List<CharacterController> candidatos,
        Team selfTeam,
        Func<CharacterController, bool> paraCadaAlvo)
    {
        for (var i = 0; i < candidatos.Count; i++)
        {
            if (!IsValidTarget(candidatos[i], self: null, selfTeam, out var alvo))
                continue;

            if (!paraCadaAlvo(alvo!))
                return;
        }
    }

    private static bool IsValidTarget(GodotObject no, CharacterBody3D? self, Team selfTeam, out CharacterController? alvo)
    {
        alvo = null;

        if (no is not CharacterController candidato || candidato == self || !GodotObject.IsInstanceValid(candidato))
            return false;

        if (candidato.Team == selfTeam)
            return false;

        var vida = candidato.Context?.Health;
        if (vida is null || !vida.IsAlive)
            return false;

        alvo = candidato;
        return true;
    }
}
