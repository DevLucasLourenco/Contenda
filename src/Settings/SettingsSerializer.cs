using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Contenda.Settings;

/// <summary>
/// Converte as configurações de e para o texto de `user://settings.cfg` (INI,
/// o formato do `ConfigFile`), sem tocar em disco nem na engine. Spec 14 §2.
/// </summary>
/// <remarks>
/// Leitura tolerante, como o perfil (ticket 29): valor ilegível cai no padrão,
/// número fora da faixa é puxado para dentro, e uma `version` desconhecida
/// devolve os padrões inteiros em vez de arriscar interpretar um formato que
/// esta versão do jogo não conhece. Apagar o arquivo dá os padrões (o
/// <see cref="Parse"/> de texto vazio é o padrão).
/// </remarks>
public static class SettingsSerializer
{
    public static string Serialize(GameSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var t = new StringBuilder();
        t.Append("[meta]\nversion = ").Append(GameSettings.CurrentVersion).Append('\n');

        t.Append("\n[video]\n");
        t.Append("window_mode = ").Append(Nome(s.Video.WindowMode)).Append('\n');
        t.Append("resolution_width = ").Append(s.Video.ResolutionWidth).Append('\n');
        t.Append("resolution_height = ").Append(s.Video.ResolutionHeight).Append('\n');
        t.Append("vsync = ").Append(Nome(s.Video.VSync)).Append('\n');
        t.Append("fps_limit = ").Append(s.Video.FpsLimit).Append('\n');
        t.Append("shadows = ").Append(Nome(s.Video.Shadows)).Append('\n');
        t.Append("render_scale = ").Append(s.Video.RenderScale.ToString("0.##", CultureInfo.InvariantCulture)).Append('\n');

        t.Append("\n[audio]\n");
        t.Append("master = ").Append(s.Audio.Master).Append('\n');
        t.Append("music = ").Append(s.Audio.Music).Append('\n');
        t.Append("sfx = ").Append(s.Audio.Sfx).Append('\n');
        t.Append("ui = ").Append(s.Audio.Ui).Append('\n');
        t.Append("ambience = ").Append(s.Audio.Ambience).Append('\n');

        t.Append("\n[gameplay]\n");
        t.Append("shake_intensity = ").Append(s.Gameplay.ShakeIntensityPercent).Append('\n');
        t.Append("show_damage_numbers = ").Append(s.Gameplay.ShowDamageNumbers ? "true" : "false").Append('\n');
        t.Append("show_combo_guide = ").Append(s.Gameplay.ShowComboGuide ? "true" : "false").Append('\n');
        t.Append("command_window = ").Append(Nome(s.Gameplay.CommandWindow)).Append('\n');

        t.Append("\n[bindings]\n");
        foreach (var (acao, teclas) in s.Bindings)
            t.Append(acao).Append(" = ").Append(string.Join(',', teclas)).Append('\n');

        return t.ToString();
    }

    public static GameSettings Parse(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var s = new GameSettings();
        var secao = "";
        var versaoDesconhecida = false;

        foreach (var linhaBruta in texto.Split('\n'))
        {
            var linha = linhaBruta.Trim();
            if (linha.Length == 0 || linha[0] is ';' or '#')
                continue;

            if (linha[0] == '[' && linha[^1] == ']')
            {
                secao = linha[1..^1].Trim();
                continue;
            }

            var igual = linha.IndexOf('=');
            if (igual <= 0)
                continue;

            var chave = linha[..igual].Trim();
            var valor = linha[(igual + 1)..].Trim();

            switch (secao)
            {
                case "meta" when chave == "version":
                    if (!int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var versao)
                        || versao != GameSettings.CurrentVersion)
                    {
                        versaoDesconhecida = true;
                    }

                    break;
                case "video": LerVideo(s.Video, chave, valor); break;
                case "audio": LerAudio(s.Audio, chave, valor); break;
                case "gameplay": LerJogo(s.Gameplay, chave, valor); break;
                case "bindings": LerTeclas(s, chave, valor); break;
            }
        }

        if (versaoDesconhecida)
            return new GameSettings();

        s.Normalize();
        return s;
    }

    private static void LerVideo(VideoSettings v, string chave, string valor)
    {
        switch (chave)
        {
            case "window_mode": v.WindowMode = Enumeracao(valor, v.WindowMode); break;
            case "resolution_width": v.ResolutionWidth = Inteiro(valor, v.ResolutionWidth); break;
            case "resolution_height": v.ResolutionHeight = Inteiro(valor, v.ResolutionHeight); break;
            case "vsync": v.VSync = Enumeracao(valor, v.VSync); break;
            case "fps_limit": v.FpsLimit = Inteiro(valor, v.FpsLimit); break;
            case "shadows": v.Shadows = Enumeracao(valor, v.Shadows); break;
            case "render_scale":
                if (float.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var escala))
                    v.RenderScale = escala;

                break;
        }
    }

    private static void LerAudio(AudioSettings a, string chave, string valor)
    {
        switch (chave)
        {
            case "master": a.Master = Inteiro(valor, a.Master); break;
            case "music": a.Music = Inteiro(valor, a.Music); break;
            case "sfx": a.Sfx = Inteiro(valor, a.Sfx); break;
            case "ui": a.Ui = Inteiro(valor, a.Ui); break;
            case "ambience": a.Ambience = Inteiro(valor, a.Ambience); break;
        }
    }

    private static void LerJogo(GameplaySettings g, string chave, string valor)
    {
        switch (chave)
        {
            case "shake_intensity": g.ShakeIntensityPercent = Inteiro(valor, g.ShakeIntensityPercent); break;
            case "show_damage_numbers": g.ShowDamageNumbers = Booleano(valor, g.ShowDamageNumbers); break;
            case "show_combo_guide": g.ShowComboGuide = Booleano(valor, g.ShowComboGuide); break;
            case "command_window": g.CommandWindow = Enumeracao(valor, g.CommandWindow); break;
        }
    }

    private static void LerTeclas(GameSettings s, string acao, string valor)
    {
        var teclas = new List<string>();
        foreach (var pedaco in valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (BindingSpec.IsValid(pedaco))
                teclas.Add(pedaco);
        }

        // Uma ação com a lista vazia é legítima: o jogador tirou a tecla dela.
        s.Bindings[acao] = teclas;
    }

    private static int Inteiro(string valor, int padrao)
        => int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : padrao;

    private static bool Booleano(string valor, bool padrao) => valor switch
    {
        "true" => true,
        "false" => false,
        _ => padrao,
    };

    private static T Enumeracao<T>(string valor, T padrao) where T : struct, Enum
        => Enum.TryParse<T>(valor.Replace("_", ""), ignoreCase: true, out var e) && Enum.IsDefined(e) ? e : padrao;

    private static string Nome<T>(T valor) where T : struct, Enum => valor.ToString().ToLowerInvariant();
}
