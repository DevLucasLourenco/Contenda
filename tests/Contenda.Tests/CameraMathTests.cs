using Contenda.Camera;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// A matemática do enquadramento 2.5D.
/// </summary>
/// <remarks>
/// Vale testar porque é onde um sinal trocado passa despercebido: a câmera
/// continua "funcionando", só que enquadrando do lado errado, e ninguém nota até
/// alguém reclamar que o mapa parece espelhado.
///
/// <see cref="Vector3"/> e <see cref="Mathf"/> são struct e static gerenciados —
/// não chamam código nativo, então rodam fora do editor.
/// </remarks>
public sealed class CameraMathTests
{
    private const float Tol = 0.01f;

    [Fact]
    public void Offset_padrao_poe_a_camera_acima_e_a_nordeste_do_alvo()
    {
        var offset = CameraMath.OffsetFromAngles(-55f, 45f, 14f);

        // Altura = distância × sen(55°) = 14 × 0,819
        Assert.Equal(11.47f, offset.Y, Tol);

        // Yaw de 45° divide o afastamento horizontal igualmente entre X e Z,
        // ambos positivos: a câmera fica a nordeste e olha para sudoeste.
        Assert.True(offset.X > 0f, "câmera deve ficar em +X");
        Assert.True(offset.Z > 0f, "câmera deve ficar em +Z");
        Assert.Equal(offset.X, offset.Z, Tol);
    }

    [Fact]
    public void Distancia_ate_o_alvo_e_exatamente_a_configurada()
    {
        foreach (var d in new[] { 11f, 14f, 18f })
        {
            var offset = CameraMath.OffsetFromAngles(-55f, 45f, d);
            Assert.Equal(d, offset.Length(), Tol);
        }
    }

    [Fact]
    public void Pitch_mais_fechado_levanta_a_camera_e_a_aproxima_no_plano()
    {
        var raso = CameraMath.OffsetFromAngles(-50f, 45f, 14f);
        var fechado = CameraMath.OffsetFromAngles(-60f, 45f, 14f);

        Assert.True(fechado.Y > raso.Y, "pitch mais fechado deve elevar a câmera");

        var horizRaso = new Vector2(raso.X, raso.Z).Length();
        var horizFechado = new Vector2(fechado.X, fechado.Z).Length();
        Assert.True(horizFechado < horizRaso, "pitch mais fechado deve aproximar no plano");
    }

    [Fact]
    public void A_camera_olha_de_volta_para_o_alvo()
    {
        var offset = CameraMath.OffsetFromAngles(-55f, 45f, 14f);
        var direcaoDeVista = CameraMath.LookDirection(-55f, 45f);

        // Partindo da câmera e andando na direção de vista pela distância certa,
        // chega-se ao alvo. É o teste que pega sinal trocado.
        var chegada = offset + (direcaoDeVista * 14f);
        Assert.Equal(0f, chegada.Length(), Tol);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(35f)]
    [InlineData(45f)]
    [InlineData(90f)]
    public void Yaw_gira_o_offset_no_plano_sem_mexer_na_altura(float yaw)
    {
        var referencia = CameraMath.OffsetFromAngles(-55f, 0f, 14f);
        var girado = CameraMath.OffsetFromAngles(-55f, yaw, 14f);

        Assert.Equal(referencia.Y, girado.Y, Tol);
        Assert.Equal(
            new Vector2(referencia.X, referencia.Z).Length(),
            new Vector2(girado.X, girado.Z).Length(),
            Tol);
    }

    [Fact]
    public void Movimento_do_WASD_e_relativo_a_camera_nao_ao_mundo()
    {
        // Com yaw de 45°, "para frente" na tela é a diagonal do mundo. Este é o
        // requisito não negociável da spec 02 §8: W move para cima na tela.
        var frente = CameraMath.MovementToWorld(new Vector2(0f, -1f), 45f);

        Assert.Equal(1f, frente.Length(), Tol);
        Assert.Equal(0f, frente.Y, Tol);

        // Afastar-se da câmera, que está em +X/+Z, significa ir para −X e −Z.
        Assert.True(frente.X < 0f, "W deve afastar da câmera em X");
        Assert.True(frente.Z < 0f, "W deve afastar da câmera em Z");
        Assert.Equal(frente.X, frente.Z, Tol);
    }

    [Fact]
    public void Direcoes_opostas_do_WASD_produzem_vetores_opostos()
    {
        var w = CameraMath.MovementToWorld(new Vector2(0f, -1f), 45f);
        var s = CameraMath.MovementToWorld(new Vector2(0f, 1f), 45f);
        var a = CameraMath.MovementToWorld(new Vector2(-1f, 0f), 45f);
        var d = CameraMath.MovementToWorld(new Vector2(1f, 0f), 45f);

        Assert.Equal(0f, (w + s).Length(), Tol);
        Assert.Equal(0f, (a + d).Length(), Tol);

        // E W é perpendicular a D: as quatro direções formam uma cruz na tela.
        Assert.Equal(0f, w.Dot(d), Tol);
    }

    [Fact]
    public void Entrada_nula_nao_produz_direcao_espuria()
    {
        Assert.Equal(Vector3.Zero, CameraMath.MovementToWorld(Vector2.Zero, 45f));
    }

    [Fact]
    public void As_oito_direcoes_formam_uma_rosa_completa_na_tela()
    {
        // O critério do ticket 05: W para cima na tela, e as oito direções
        // consistentes entre si. Um sinal trocado numa diagonal passa
        // despercebido ao testar só os quatro eixos.
        var entradas = new (string Nome, Vector2 Eixo)[]
        {
            ("W",  new Vector2(0f, -1f)),
            ("WD", new Vector2(1f, -1f).Normalized()),
            ("D",  new Vector2(1f, 0f)),
            ("SD", new Vector2(1f, 1f).Normalized()),
            ("S",  new Vector2(0f, 1f)),
            ("SA", new Vector2(-1f, 1f).Normalized()),
            ("A",  new Vector2(-1f, 0f)),
            ("WA", new Vector2(-1f, -1f).Normalized()),
        };

        var anterior = CameraMath.MovementToWorld(entradas[^1].Eixo, 45f);
        foreach (var (nome, eixo) in entradas)
        {
            var atual = CameraMath.MovementToWorld(eixo, 45f);

            Assert.Equal(1f, atual.Length(), Tol);
            Assert.Equal(0f, atual.Y, Tol);

            // Passos consecutivos da rosa ficam a 45° um do outro: sem buracos
            // nem direções repetidas.
            var graus = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(anterior.Dot(atual), -1f, 1f)));
            Assert.True(Mathf.Abs(graus - 45f) < 0.5f,
                $"de {nome} para o anterior deu {graus:0.0}°, esperado 45°");

            anterior = atual;
        }
    }

    [Fact]
    public void Diagonais_ficam_exatamente_entre_os_eixos_vizinhos()
    {
        var w = CameraMath.MovementToWorld(new Vector2(0f, -1f), 45f);
        var d = CameraMath.MovementToWorld(new Vector2(1f, 0f), 45f);
        var wd = CameraMath.MovementToWorld(new Vector2(1f, -1f).Normalized(), 45f);

        Assert.Equal(0f, ((w + d).Normalized() - wd).Length(), Tol);
    }
}
