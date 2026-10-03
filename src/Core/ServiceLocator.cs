using Godot;
using Contenda.Vfx;

namespace Contenda.Core;

/// <summary>
/// Acesso tipado aos autoloads.
/// </summary>
/// <remarks>
/// Existe para que nenhum lugar do projeto escreva
/// <c>GetNode&lt;GameSession&gt;("/root/GameSession")</c> — caminho literal em
/// string, proibido pelas convenções §2, e que só falha em runtime quando alguém
/// renomeia o autoload.
///
/// Isto **não** é um contêiner de injeção de dependência nem um lugar para
/// guardar estado de gameplay. É uma tabela de cinco entradas fixas. Estado de
/// personagem vive no <c>CharacterContext</c>.
/// </remarks>
public static class ServiceLocator
{
    private static GameEvents? _events;
    private static GameSession? _session;
    private static SceneRouter? _router;
    private static AudioDirector? _audio;
    private static CombatVfxDirector? _vfx;

    public static GameEvents Events => _events ?? throw NotReady(nameof(GameEvents));
    public static GameSession Session => _session ?? throw NotReady(nameof(GameSession));
    public static SceneRouter Router => _router ?? throw NotReady(nameof(SceneRouter));
    public static AudioDirector Audio => _audio ?? throw NotReady(nameof(AudioDirector));
    public static CombatVfxDirector Vfx => _vfx ?? throw NotReady(nameof(CombatVfxDirector));

    /// <summary>
    /// Chamado por cada autoload no próprio <c>_Ready</c>. Nós não procuramos os
    /// serviços; eles se anunciam.
    /// </summary>
    public static void Register(Node service)
    {
        switch (service)
        {
            case GameEvents e: _events = e; break;
            case GameSession s: _session = s; break;
            case SceneRouter r: _router = r; break;
            case AudioDirector a: _audio = a; break;
            case CombatVfxDirector v: _vfx = v; break;
            default:
                GD.PushWarning($"ServiceLocator não conhece {service.GetType().Name}");
                break;
        }
    }

    /// <summary>Limpa o registro. Só faz sentido em teste de cena.</summary>
    public static void Clear()
    {
        _events = null;
        _session = null;
        _router = null;
        _audio = null;
        _vfx = null;
    }

    private static System.InvalidOperationException NotReady(string name) =>
        new($"{name} ainda não foi registrado. Ele é autoload: verifique a ordem " +
            "em Project Settings → Autoload, e que este acesso não roda antes do boot.");
}
