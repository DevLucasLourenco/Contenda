using System;
using System.IO;

namespace Contenda.Persistence;

/// <summary>
/// Grava um arquivo de texto sem nunca deixá-lo pela metade: escreve num
/// `.tmp` ao lado e só então troca pelo definitivo. Spec 14 §3.
/// </summary>
/// <remarks>
/// Se o jogo fechar no meio da escrita, o que sobra é um `.tmp` incompleto --
/// o arquivo de verdade continua o anterior, inteiro. A troca em si é um
/// `File.Replace`/`Move` do sistema de arquivos, não uma cópia byte a byte.
/// `escrever` existe para o teste simular a interrupção.
/// </remarks>
public static class AtomicFile
{
    public static void WriteAllText(string caminho, string texto, Action<string, string>? escrever = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(caminho);
        ArgumentNullException.ThrowIfNull(texto);

        var pasta = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(pasta))
            Directory.CreateDirectory(pasta);

        var temporario = caminho + ".tmp";
        (escrever ?? File.WriteAllText)(temporario, texto);

        if (File.Exists(caminho))
            File.Replace(temporario, caminho, destinationBackupFileName: null);
        else
            File.Move(temporario, caminho);
    }
}
