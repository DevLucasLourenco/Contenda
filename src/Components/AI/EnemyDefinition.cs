using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Os parâmetros de percepção e comportamento de um inimigo.
/// </summary>
/// <remarks>
/// Deliberadamente pequeno: o resto do roster completo (spec 09 §5 —
/// `ModelScene`/`AnimationSet`/`AttackKind`/`ScoreValue`/`StaggerResistance`)
/// pertence a tickets futuros (29, 34), que ainda não existem em código.
/// Adicionar esses campos agora seria balanceamento para um sistema que não
/// roda ainda -- regra do CLAUDE.md contra desenhar para necessidade
/// hipotética. Dano e alcance de ataque não entram aqui: já vêm do
/// <see cref="Contenda.Weapons.WeaponDefinition"/> da arma equipada, a MESMA
/// que o jogador usa (ticket 22 exige isso).
/// </remarks>
[GlobalClass]
public sealed partial class EnemyDefinition : Resource
{
    /// <summary>Raio em que o inimigo passa a perceber o alvo, em metros.</summary>
    [Export(PropertyHint.Range, "1,40,0.5")] public float DetectionRadius { get; set; } = 22f;

    /// <summary>
    /// Raio além do qual, já em perseguição, o alvo conta como fora de vista.
    /// </summary>
    /// <remarks>Maior que <see cref="DetectionRadius"/> de propósito: evita ioiô de estado perto da borda.</remarks>
    [Export(PropertyHint.Range, "1,60,0.5")] public float LoseTargetRadius { get; set; } = 30f;

    /// <summary>Quanto tempo sem ver o alvo até desistir de vez, em segundos.</summary>
    [Export(PropertyHint.Range, "0.5,10,0.5")] public float LoseTargetDelay { get; set; } = 3f;

    /// <summary>Duração do estado de alerta antes de partir para cima, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float AlertDuration { get; set; } = 0.4f;

    /// <summary>
    /// Distância para começar a preparar o golpe, em metros.
    /// </summary>
    /// <remarks>
    /// Independente do alcance real da arma: começar a preparação um pouco
    /// ANTES de estar dentro do alcance de dano dá tempo do avanço do golpe
    /// (spec 07 §4) fechar a distância, em vez de exigir estar imóvel e
    /// colado no alvo primeiro.
    /// </remarks>
    [Export(PropertyHint.Range, "0.5,20,0.1")] public float AttackRange { get; set; } = 2.2f;

    /// <summary>
    /// Preparação visível antes do golpe em si, em segundos.
    /// </summary>
    /// <remarks>
    /// Maior que a janela de acerto de qualquer passo do jogador de propósito
    /// -- a telegrafia é o que evita o dano parecer aleatório sob câmera fixa
    /// e vários inimigos, spec 09 §7.
    /// </remarks>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float AttackWindup { get; set; } = 0.45f;

    /// <summary>Tempo sem poder atacar de novo depois de golpear, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float AttackCooldown { get; set; } = 1.4f;

    /// <summary>Duração do atordoamento ao apanhar, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float StaggerDuration { get; set; } = 0.5f;

    /// <summary>
    /// Quanto tempo o corpo fica visível, sem colisão, antes de voltar ao
    /// pool, em segundos.
    /// </summary>
    /// <remarks>
    /// Espaço reservado para a "animação de morte" de verdade (ticket 34):
    /// sem modelo/`AnimationPlayer` ainda (ADR-010), o placeholder de hoje é
    /// só este tempo de espera com a colisão já desligada -- o suficiente
    /// para o contrato de reciclagem do ticket 25 valer sem esperar arte.
    /// </remarks>
    [Export(PropertyHint.Range, "0,5,0.05")] public float DeathDuration { get; set; } = 1.2f;

    /// <summary>Se é uma elite -- mais perigoso, e precisa se anunciar como tal de longe.</summary>
    /// <remarks>Ticket 26, spec 09 §5. Vale a pena além do tamanho da própria barra: ver <see cref="EliteTint"/>.</remarks>
    [Export] public bool IsElite { get; set; }

    /// <summary>
    /// Cor de destaque de uma elite -- tingimento emissivo na própria malha,
    /// já que o projeto ainda não tem modelo/textura de verdade (ADR-010).
    /// </summary>
    /// <remarks>"Elite dourado" é a convenção de cor de time da spec 11 §4; o padrão aqui só a repete.</remarks>
    [Export] public Color EliteTint { get; set; } = Colors.Gold;

    /// <summary>Se é o chefe da onda -- ganha barra própria no alto da tela, com nome.</summary>
    /// <remarks>
    /// Ticket 26. O nome exibido vem do <c>CharacterDefinition.DisplayName</c>
    /// deste inimigo, não daqui -- nome é identidade do PERSONAGEM, o mesmo
    /// campo que qualquer arquétipo do jogador já usa.
    /// </remarks>
    [Export] public bool IsBoss { get; set; }

    /// <summary>
    /// Quantos golpes da cadeia da arma o inimigo encadeia num único ataque.
    /// </summary>
    /// <remarks>
    /// Ticket 28: é o que dá ao chefe "mais de um golpe" sem classe nova (spec
    /// 09 §6) -- a arma dele traz N passos de combo com dano/alcance/avanço
    /// diferentes, e o cérebro só insiste até o passo <c>ComboHits</c>. 1 (padrão)
    /// é um golpe simples, como todo o resto do roster. Só faz sentido com arma
    /// corpo a corpo.
    /// </remarks>
    [Export(PropertyHint.Range, "1,5,1")] public int ComboHits { get; set; } = 1;

    /// <summary>A cena que o <c>EnemyPool</c> instancia para esta espécie. Ver <see cref="PoolSize"/>.</summary>
    /// <remarks>
    /// Ticket 28: com mais de uma espécie, o pool precisa saber de onde criar
    /// cada uma -- o grunt do boot continua vindo dos caminhos exportados do
    /// próprio pool, mas o resto do roster se prewarma por aqui.
    /// </remarks>
    [Export] public PackedScene? Scene { get; set; }

    /// <summary>Quantas instâncias pré-alocar desta espécie no início da partida. Spec 10 §7.</summary>
    [Export(PropertyHint.Range, "1,200,1")] public int PoolSize { get; set; } = 10;
}
