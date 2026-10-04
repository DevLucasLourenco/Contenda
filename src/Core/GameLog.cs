using Godot;

namespace Contenda.Core;

/// <summary>Registra mensagens internas somente em builds de desenvolvimento.</summary>
public static class GameLog
{
    /// <summary>Emite uma mensagem de diagnóstico se a execução for de debug.</summary>
    public static void Debug(string message)
    {
        if (OS.IsDebugBuild())
            GD.Print(message);
    }
}
