using Godot;

namespace Contenda.Core;

/// <summary>
/// Identidade do build.
/// </summary>
/// <remarks>
/// Existe já no ticket 01 para que a compilação prove alguma coisa: um assembly
/// sem nenhum tipo compila mesmo que a referência ao GodotSharp esteja quebrada.
/// Ao tocar em <c>Engine</c>, este arquivo faz o build falhar se o
/// <c>Godot.NET.Sdk</c> não estiver corretamente resolvido.
/// </remarks>
public static class ProjectInfo
{
    public const string Name = "Contenda";

    /// <summary>
    /// Versão do assembly. A fonte de verdade é <c>&lt;Version&gt;</c> no
    /// <c>Contenda.csproj</c> — não redeclare o número aqui, senão um bump vira
    /// duas edições que podem divergir em silêncio.
    /// </summary>
    public static string Version { get; } =
        typeof(ProjectInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Linha de identificação para log de boot e tela de configurações.</summary>
    public static string Describe()
    {
        // Fronteira com a API da engine: GetVersionInfo() devolve
        // Godot.Collections.Dictionary e não há como evitar. Convertemos aqui
        // mesmo, com AsString(), em vez de deixar o tipo vazar para dentro do
        // projeto — ver convenções de código §2.
        var engine = Engine.GetVersionInfo()["string"].AsString();
        return $"{Name} {Version} · Godot {engine}";
    }
}
