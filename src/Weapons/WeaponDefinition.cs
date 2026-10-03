using Godot;

namespace Contenda.Weapons;

/// <summary>
/// O que uma arma é.
/// </summary>
/// <remarks>
/// **É aqui que M1 ganha significado.** O botão não quer dizer "soco": quer
/// dizer "ataque básico", e a arma equipada decide o resto. Trocar este recurso
/// troca o comportamento do ataque sem um único `if` sobre qual personagem está
/// em jogo — ver spec 07 §1.
/// </remarks>
[GlobalClass]
public sealed partial class WeaponDefinition : Resource
{
    /// <summary>ID usado pelo recurso do braço-canhão e sua apresentação de disparo.</summary>
    public static readonly StringName ArmCannonId = new("weapon.arm_cannon");

    /// <summary>Identificador estável.</summary>
    [Export] public StringName Id { get; set; } = new("sem_arma");

    /// <summary>Nome exibido.</summary>
    [Export] public string DisplayName { get; set; } = "Sem arma";

    /// <summary>Modelo visual instanciado no socket da mão do rig, se houver.</summary>
    [Export] public PackedScene? ModelScene { get; set; }

    [Export] public Vector3 ModelPositionOffset { get; set; } = Vector3.Zero;
    [Export] public Vector3 ModelRotationOffsetDegrees { get; set; } = Vector3.Zero;
    [Export] public Vector3 ModelScale { get; set; } = Vector3.One;

    /// <summary>Corpo a corpo ou tiro.</summary>
    [Export] public WeaponKind Kind { get; set; } = WeaponKind.Melee;

    /// <summary>Dano antes dos multiplicadores.</summary>
    [Export(PropertyHint.Range, "1,200,1")] public float BaseDamage { get; set; } = 20f;

    /// <summary>Alcance do golpe, em metros.</summary>
    [Export(PropertyHint.Range, "0.5,30,0.1")] public float Range { get; set; } = 2.2f;

    /// <summary>
    /// Meia-abertura do cone de acerto, em graus. 180 significa em volta todo.
    /// </summary>
    /// <remarks>Só corpo a corpo. Hitscan mira em linha reta; ver <see cref="SpreadDegrees"/>.</remarks>
    [Export(PropertyHint.Range, "10,180,5")] public float HalfAngle { get; set; } = 60f;

    /// <summary>Repulsão base aplicada ao alvo.</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float Knockback { get; set; } = 2f;

    /// <summary>A cadeia de golpes. Vazia vira um golpe único com os padrões. Só corpo a corpo.</summary>
    [Export] public MeleeComboStep[] ComboSteps { get; set; } = [];

    /// <summary>
    /// Tempo mínimo entre tiros, em segundos. Só hitscan.
    /// </summary>
    /// <remarks>
    /// Dividido por <c>AttackSpeed</c> do <c>StatBlock</c> a cada tiro. Uma
    /// arma de forma pode sobrescrever este recurso para mudar a cadência sem
    /// alterar a arma base -- Overdrive usa 0,50 s contra 0,30 s do revólver.
    /// Ver spec 07 §5 e ticket 20.
    /// </remarks>
    [Export(PropertyHint.Range, "0.05,3,0.01")] public float AttackInterval { get; set; } = 0.3f;

    /// <summary>Cartuchos no tambor antes de recarregar. Só hitscan.</summary>
    [Export(PropertyHint.Range, "1,20,1")] public int MagazineSize { get; set; } = 6;

    /// <summary>Tempo de recarga automática ao esvaziar o tambor, em segundos. Só hitscan.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float ReloadTime { get; set; } = 1.6f;

    /// <summary>Meia-abertura do cone de dispersão aleatória do tiro, em graus. Só hitscan.</summary>
    [Export(PropertyHint.Range, "0,15,0.1")] public float SpreadDegrees { get; set; } = 1.5f;

    /// <summary>Raio horizontal da explosão por tiro; zero mantém hitscan comum.</summary>
    [Export(PropertyHint.Range, "0,10,0.1")] public float ExplosionRadius { get; set; }

    /// <summary>Velocidade do projétil quando a arma dispara dano explosivo.</summary>
    [Export(PropertyHint.Range, "1,60,1")] public float ProjectileSpeed { get; set; } = 20f;

    /// <summary>Raio de contato com o alvo; a explosão usa ExplosionRadius.</summary>
    [Export(PropertyHint.Range, "0.05,2,0.05")] public float ProjectileImpactRadius { get; set; } = 0.5f;

    /// <summary>Multiplicador de dano na borda da explosão.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float EdgeDamageMultiplier { get; set; } = 0.6f;

    /// <summary>Arma de energia que não consome munição nem recarrega.</summary>
    [Export] public bool InfiniteAmmo { get; set; }

    /// <summary>
    /// Duração do congelamento local ao conectar, em segundos. Só hitscan —
    /// corpo a corpo usa o valor por passo em <see cref="MeleeComboStep"/>.
    /// </summary>
    /// <remarks>Ver ticket 11. Aplicado aos dois envolvidos — quem atirou e quem apanhou.</remarks>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float HitstopSeconds { get; set; } = 0.04f;

    /// <summary>
    /// Somado a <see cref="HitstopSeconds"/> quando o tiro sai crítico.
    /// </summary>
    /// <remarks>
    /// Somado, não substituído: o congelamento do crítico precisa ficar
    /// SEMPRE maior que o normal, mesmo se algum dia este golpe já tiver um
    /// `HitstopSeconds` fora do padrão — spec 16 §5 ("crítico é sempre mais
    /// longo que o normal"). O padrão (0,04 s + 0,05 s = 0,09 s) reproduz a
    /// tabela da spec.
    /// </remarks>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float CriticalHitstopBonus { get; set; } = 0.05f;

    // --- combate aéreo (ticket 19, spec 16 §6) — só corpo a corpo ------------

    /// <summary>
    /// A cadeia de golpes no ar. Vazia (padrão) significa que esta arma não
    /// tem ataque aéreo — o corpo a corpo se recusa a atacar no ar.
    /// </summary>
    [Export] public MeleeComboStep[] AerialComboSteps { get; set; } = [];

    /// <summary>Alcance do golpe aéreo, em metros. Menor que <see cref="Range"/> — spec 16 §6.</summary>
    [Export(PropertyHint.Range, "0.5,30,0.1")] public float AerialRange { get; set; } = 2f;

    /// <summary>Meia-abertura do cone de acerto aéreo, em graus.</summary>
    [Export(PropertyHint.Range, "10,180,5")] public float AerialHalfAngle { get; set; } = 45f;

    /// <summary>
    /// Diferença de altura tolerada no ar, em metros — maior que a de solo
    /// (<c>CombatComponent.VerticalReach</c>): um alvo em pleno combo aéreo
    /// sobe e desce mais do que a folga vertical de um golpe no chão prevê.
    /// </summary>
    [Export(PropertyHint.Range, "0.5,8,0.1")] public float AerialVerticalReach { get; set; } = 4f;

    /// <summary>
    /// Impulso vertical aplicado ao alvo E a quem golpeia, em cada acerto
    /// aéreo — spec 16 §6: "0.8 m/s", é o que sustenta o combo no ar.
    /// </summary>
    [Export(PropertyHint.Range, "0,5,0.1")] public float AerialVerticalKnockback { get; set; } = 0.8f;

    /// <summary>
    /// Multiplicador de gravidade enquanto a janela de acerto aérea estiver
    /// aberta — spec 16 §6: "0.35×", o golpe "segura" no ar sem virar voo.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float AerialGravityScale { get; set; } = 0.35f;

    // --- estocada de queda (ticket 19, spec 16 §6) — só corpo a corpo --------

    /// <summary>
    /// Dano da estocada de queda ao aterrissar.
    /// </summary>
    /// <remarks>
    /// Autoral, não derivado em código: a spec 16 §6 define "1.4× do golpe
    /// pesado de solo" (Heavy Lunge, 75 de dano — 75 × 1.4 = 105), mas
    /// cravar essa RELAÇÃO em `.cs` faria o valor da estocada derivar
    /// silenciosamente se alguém rebalancear Heavy Lunge depois — regra 4 do
    /// CLAUDE.md, balanceamento vive em `.tres`.
    /// </remarks>
    [Export(PropertyHint.Range, "0,300,1")] public float DiveDamage { get; set; } = 105f;

    /// <summary>Raio da área de dano ao aterrissar, em metros — spec 16 §6: "3.5 m".</summary>
    [Export(PropertyHint.Range, "0.5,10,0.1")] public float DiveRadius { get; set; } = 3.5f;

    /// <summary>Repulsão radial aplicada a cada alvo atingido pelo pouso.</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float DiveKnockback { get; set; } = 6f;

    /// <summary>
    /// Multiplicador de gravidade durante o mergulho — bem maior que 1, para
    /// acelerar a queda em vez de segurá-la (o oposto de <see cref="AerialGravityScale"/>).
    /// </summary>
    [Export(PropertyHint.Range, "1,8,0.1")] public float DiveGravityScale { get; set; } = 3.5f;

    /// <summary>Velocidade de avanço horizontal durante o mergulho, em m/s — é o que faz a queda ser DIAGONAL.</summary>
    [Export(PropertyHint.Range, "0,15,0.5")] public float DiveForwardSpeed { get; set; } = 6f;

    /// <summary>Congelamento ao aterrissar, em segundos.</summary>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float DiveHitstopSeconds { get; set; } = 0.09f;

    /// <summary>Somado a <see cref="DiveHitstopSeconds"/> quando o pouso sai crítico. Mesma disciplina de <see cref="CriticalHitstopBonus"/>.</summary>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float DiveCriticalHitstopBonus { get; set; } = 0.05f;

    /// <summary>
    /// Recuperação ao aterrissar, em segundos — trava Movimento e Rotação.
    /// </summary>
    /// <remarks>É o risco que equilibra o poder da estocada — spec 16 §6.</remarks>
    [Export(PropertyHint.Range, "0,2,0.05")] public float DiveRecoverySeconds { get; set; } = 0.4f;
}
