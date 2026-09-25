using System;
using System.IO;

namespace Contenda.Persistence;

/// <summary>Lê e grava o perfil do jogador (`user://profile.cfg`). Spec 14 §3.</summary>
/// <remarks>
/// Sem engine: recebe o caminho já resolvido para o sistema de arquivos (quem
/// chama traduz `user://`), o que deixa tudo testável em xUnit. Escrito ao fim
/// de cada partida, nunca durante o gameplay, sempre de forma atômica
/// (<see cref="AtomicFile"/>).
/// </remarks>
public sealed class ProfileStore
{
    private readonly string _caminho;
    private readonly Action<string, string>? _escrever;

    /// <param name="caminho">Caminho real do arquivo.</param>
    /// <param name="escrever">Só para o teste simular uma gravação interrompida.</param>
    public ProfileStore(string caminho, Action<string, string>? escrever = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(caminho);

        _caminho = caminho;
        _escrever = escrever;
    }

    /// <summary>O perfil gravado; vazio se não existe ou não dá para ler.</summary>
    public ProfileData Load()
    {
        try
        {
            return File.Exists(_caminho) ? ProfileSerializer.Parse(File.ReadAllText(_caminho)) : new ProfileData();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ProfileData();
        }
    }

    /// <summary>Soma uma partida às estatísticas e guarda o recorde do personagem se ele foi superado.</summary>
    public void RecordMatch(string characterId, int score, int waves, int kills, int timeSeconds)
    {
        ArgumentException.ThrowIfNullOrEmpty(characterId);

        var perfil = Load();
        GuardarCopiaSeIlegivel(perfil);
        perfil.TotalMatches++;
        perfil.TotalKills += kills;
        perfil.TotalPlaytimeSeconds += timeSeconds;

        if (!perfil.Best.TryGetValue(characterId, out var atual) || score > atual.Score)
            perfil.Best[characterId] = new BestResult(score, waves, timeSeconds);

        AtomicFile.WriteAllText(_caminho, ProfileSerializer.Serialize(perfil), _escrever);
    }

    /// <remarks>
    /// Um arquivo que existe, tem conteúdo, e não rendeu NADA reconhecível foi
    /// corrompido de fora (disco, edição à mão). Gravar por cima o apagaria de
    /// vez -- o ticket 29 chama isso de "apaga todo o histórico" -- então a cópia
    /// fica em `.corrupt` antes da troca.
    /// </remarks>
    private void GuardarCopiaSeIlegivel(ProfileData perfil)
    {
        try
        {
            if (perfil.TotalMatches == 0 && perfil.Best.Count == 0
                && File.Exists(_caminho) && new FileInfo(_caminho).Length > 0)
            {
                File.Copy(_caminho, _caminho + ".corrupt", overwrite: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A cópia é cortesia: sem ela, ainda vale gravar o resultado novo.
        }
    }
}
