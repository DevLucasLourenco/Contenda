using System;
using Contenda.Characters.Base;
using Godot;

namespace Contenda.Weapons;

/// <summary>
/// Decide qual <see cref="IWeapon"/> uma <see cref="WeaponDefinition"/> produz.
/// </summary>
/// <remarks>
/// Único lugar do projeto que ramifica em <see cref="WeaponKind"/>. O
/// <c>CombatComponent</c> não sabe o que está segurando — só a fábrica sabe, e
/// é por isso que o revólver do ticket 09 nasce aqui, sem tocar no contêiner.
/// Ver spec 07 §1 e o ticket 08.
/// </remarks>
public static class WeaponFactory
{
    public static IWeapon Criar(
        WeaponDefinition definicao,
        CharacterContext contexto,
        Node dono,
        StringName targetGroup,
        float verticalReach)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(dono);

        return definicao.Kind switch
        {
            WeaponKind.Melee => new MeleeWeapon(definicao, contexto, dono, targetGroup, verticalReach),
            _ => SemImplementacao(definicao),
        };
    }

    /// <remarks>
    /// Hitscan é o ticket 09: ainda não tem <see cref="IWeapon"/>. Erro de
    /// CONTEÚDO — um `.tres` pedindo um `WeaponKind` sem implementação — falha
    /// alto no boot em debug e só loga em release, convenções §9: um jogador não
    /// deveria ver a build inteira cair por isso, mas quem está editando o
    /// `.tres` deveria descobrir na hora, não em produção.
    /// </remarks>
    private static IWeapon SemImplementacao(WeaponDefinition definicao)
    {
        var mensagem = $"WeaponFactory: {definicao.Kind} ainda não tem IWeapon (ticket 09). "
                       + $"Arma '{definicao.Id}' fica sem ataque.";

        if (OS.IsDebugBuild())
            throw new NotSupportedException(mensagem);

        GD.PushError(mensagem);
        return NullWeapon.Instance;
    }
}
