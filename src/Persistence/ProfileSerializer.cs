using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Contenda.Persistence;

/// <summary>
/// Converte o perfil de e para o texto de `user://profile.cfg`, sem tocar em
/// disco nem na engine.
/// </summary>
/// <remarks>
/// O formato é o INI da spec 14 §3 (o mesmo que o `ConfigFile` do Godot lê), mas
/// escrito e lido à mão: assim o perfil inteiro -- inclusive o que acontece com
/// um arquivo corrompido -- é testável em xUnit, onde `ConfigFile` (um objeto da
/// engine) não roda. Leitura tolerante: linha ilegível é ignorada, nunca uma
/// exceção -- um perfil meio estragado vale mais que um jogo que não abre.
/// </remarks>
public static class ProfileSerializer
{
    private const string SecaoDeEstatisticas = "stats";
    private const string PrefixoDeMelhor = "best.";

    public static string Serialize(ProfileData perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        var texto = new StringBuilder();
        texto.Append('[').Append(SecaoDeEstatisticas).Append("]\n");
        texto.Append("total_matches = ").Append(perfil.TotalMatches).Append('\n');
        texto.Append("total_kills = ").Append(perfil.TotalKills).Append('\n');
        texto.Append("total_playtime_seconds = ").Append(perfil.TotalPlaytimeSeconds).Append('\n');

        foreach (var (personagem, melhor) in perfil.Best)
        {
            texto.Append("\n[").Append(PrefixoDeMelhor).Append(Sanitizar(personagem)).Append("]\n");
            texto.Append("score = ").Append(melhor.Score).Append('\n');
            texto.Append("waves = ").Append(melhor.Waves).Append('\n');
            texto.Append("time_seconds = ").Append(melhor.TimeSeconds).Append('\n');
        }

        return texto.ToString();
    }

    public static ProfileData Parse(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var perfil = new ProfileData();
        var secao = "";
        var valoresDoMelhor = new Dictionary<string, Dictionary<string, int>>();

        foreach (var linhaBruta in texto.Split('\n'))
        {
            var linha = linhaBruta.Trim();
            if (linha.Length == 0 || linha[0] == ';' || linha[0] == '#')
                continue;

            if (linha[0] == '[' && linha[^1] == ']')
            {
                secao = linha[1..^1].Trim();
                continue;
            }

            var igual = linha.IndexOf('=');
            if (igual <= 0
                || !int.TryParse(linha[(igual + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor))
            {
                continue;
            }

            var chave = linha[..igual].Trim();

            if (secao == SecaoDeEstatisticas)
            {
                switch (chave)
                {
                    case "total_matches": perfil.TotalMatches = valor; break;
                    case "total_kills": perfil.TotalKills = valor; break;
                    case "total_playtime_seconds": perfil.TotalPlaytimeSeconds = valor; break;
                }
            }
            else if (secao.StartsWith(PrefixoDeMelhor, StringComparison.Ordinal))
            {
                var personagem = secao[PrefixoDeMelhor.Length..];
                if (!valoresDoMelhor.TryGetValue(personagem, out var campos))
                    valoresDoMelhor[personagem] = campos = new Dictionary<string, int>();

                campos[chave] = valor;
            }
        }

        foreach (var (personagem, campos) in valoresDoMelhor)
        {
            if (campos.TryGetValue("score", out var pontos))
            {
                perfil.Best[personagem] = new BestResult(
                    pontos, campos.GetValueOrDefault("waves"), campos.GetValueOrDefault("time_seconds"));
            }
        }

        return perfil;
    }

    /// <summary>Nome de seção seguro: só letras, dígitos, `_` e `-`.</summary>
    private static string Sanitizar(string personagem)
    {
        var limpo = new StringBuilder(personagem.Length);
        foreach (var c in personagem)
            limpo.Append(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_');

        return limpo.ToString();
    }
}
