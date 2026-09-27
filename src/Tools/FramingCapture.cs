using Contenda.Characters.Base;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Abre uma cena, espera estabilizar e salva um PNG do que a câmera vê.
/// </summary>
/// <remarks>
/// Existe porque o critério que decide o ticket 04 — "a câmera 2.5D é legível e
/// agradável" — é julgamento humano, e ninguém julga enquadramento lendo
/// coordenadas. Isto transforma a pergunta num arquivo que dá para olhar.
///
/// Rode assim (SEM <c>--headless</c>: capturar exige renderização de verdade):
/// <code>
/// godot --path . --scene res://scenes/debug/FramingCapture.tscn
/// </code>
///
/// A imagem sai em <c>user://</c> — em Windows,
/// <c>%APPDATA%\Godot\app_userdata\Contenda</c>.
/// </remarks>
public sealed partial class FramingCapture : Node
{
    /// <summary>Cena a fotografar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>Nome do arquivo gerado, dentro de <c>user://</c>.</summary>
    [Export] public string OutputName { get; set; } = "enquadramento.png";

    /// <summary>
    /// Quadros a esperar antes de capturar.
    /// </summary>
    /// <remarks>
    /// Capturar no primeiro quadro pega a cena antes de sombras, céu e oclusão
    /// terem convergido — a foto sai mais escura e mais chapada do que o jogo
    /// realmente é, e levaria a ajustar a câmera pelo motivo errado.
    /// </remarks>
    [Export] public int WarmupFrames { get; set; } = 30;

    /// <summary>Posiciona e congela o jogador antes da captura, para validar outros níveis da arena.</summary>
    [Export] public bool OverridePlayerPosition { get; set; }

    [Export] public Vector3 PlayerPosition { get; set; }

    /// <summary>Aplica uma condição de luz à cópia local do ambiente da cena.</summary>
    [Export] public bool OverrideLighting { get; set; }

    [Export(PropertyHint.Range, "0,3,0.05")] public float SunEnergy { get; set; } = 1.1f;

    [Export(PropertyHint.Range, "0,3,0.05")] public float AmbientLightEnergy { get; set; } = 1f;

    private int _quadros;
    private bool _capturado;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[captura] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        var cena = packed.Instantiate();
        AddChild(cena);

        if (OverridePlayerPosition)
        {
            var jogador = Encontrar<CharacterController>(cena);
            if (jogador is null)
            {
                GD.PushError($"[captura] não encontrei o jogador em {ScenePath}");
                GetTree().Quit(1);
                return;
            }

            jogador.SetPhysicsProcess(false);
            jogador.GlobalPosition = PlayerPosition;
        }

        if (OverrideLighting)
        {
            var ambiente = Encontrar<WorldEnvironment>(cena);
            if (ambiente?.Environment?.Duplicate() is Environment copia)
            {
                copia.AmbientLightEnergy = AmbientLightEnergy;
                ambiente.Environment = copia;
            }

            var sol = Encontrar<DirectionalLight3D>(cena);
            if (sol is not null)
                sol.LightEnergy = SunEnergy;
        }

        GD.Print($"[captura] {ScenePath} carregada; aquecendo {WarmupFrames} quadros…");
    }

    public override void _Process(double delta)
    {
        if (_capturado)
            return;

        if (++_quadros < WarmupFrames)
            return;

        _capturado = true;

        var imagem = GetViewport().GetTexture().GetImage();
        var destino = $"user://{OutputName}";
        var erro = imagem.SavePng(destino);

        if (erro != Error.Ok)
        {
            GD.PrintErr($"[captura] falha ao salvar: {erro}");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[captura] {imagem.GetWidth()}x{imagem.GetHeight()} salvo em {destino}");
        GD.Print($"[captura] caminho real: {ProjectSettings.GlobalizePath(destino)}");
        GetTree().Quit();
    }

    private static T? Encontrar<T>(Node raiz) where T : Node
    {
        if (raiz is T achado)
            return achado;

        foreach (var filho in raiz.GetChildren())
        {
            var dentro = Encontrar<T>(filho);
            if (dentro is not null)
                return dentro;
        }

        return null;
    }
}
