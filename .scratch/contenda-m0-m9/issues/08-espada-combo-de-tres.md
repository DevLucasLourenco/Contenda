# 08: Ataque básico com espada encadeia três golpes

**What to build:** clicar o botão esquerdo com o espadachim dá um golpe de
espada. Clicar de novo dentro da janela encadeia o segundo, e depois o terceiro,
mais forte e com mais empurrão. Deixar a janela passar volta ao primeiro. Cada
golpe avança um pouco o personagem, para que acertar sob a câmera fixa seja
possível.

**Blocked by:** 07

**Status:** CONCLUÍDO

- [x] O botão esquerdo dá um golpe que acerta o manequim
- [x] Três cliques no ritmo encadeiam golpes crescentes; o terceiro empurra mais
- [x] Perder a janela reinicia a sequência sem outra penalidade
- [x] Cada golpe atinge cada alvo **uma única vez**
- [x] A área de golpe só existe durante a janela ativa; nunca fica ligada
- [x] Nenhum dano entre entidades do mesmo time
- [x] Os números de dano e as janelas vêm de dados, não do código

## Comments

O botão esquerdo **não significa "soco"**: significa "ataque básico". A arma
equipada decide o que isso quer dizer. Essa abstração é o que permite o ticket
09 existir sem um único `if` sobre qual personagem está em jogo, e o que permite
o terceiro, o décimo e o quinquagésimo personagem depois. Ver spec 07, §1.

As janelas de acerto vivem em dados, **não** em faixas de chamada dentro da
animação. Motivo: no M8 os modelos e animações são substituídos, e faixas
embutidas se perderiam junto — ver spec 13, §6.

O bloqueio de ações durante o golpe é por fonte e com duração. Somar travas sem
origem produz o bug clássico de duas fontes travarem o movimento, uma liberar, e
o jogador destravar cedo demais.

---

**Parcialmente implementado em 2026-09-04.** 75 testes verdes, build limpo, e
duas sondas headless verificando a cadeia na árvore real. Faltavam `IWeapon` e
`ActionLock` — ver histórico abaixo.

**Concluído em 2026-09-05.** Os dois pontos em aberto foram fechados:

1. **`IWeapon` existe** ([`src/Weapons/IWeapon.cs`](../../../src/Weapons/IWeapon.cs)).
   O combo, o avanço e a varredura em cone saíram do `CombatComponent` e foram
   para [`MeleeWeapon`](../../../src/Weapons/MeleeWeapon.cs), que implementa a
   interface. Uma [`WeaponFactory`](../../../src/Weapons/WeaponFactory.cs) —
   único lugar do projeto que ramifica em `WeaponKind` — decide qual `IWeapon`
   uma `WeaponDefinition` produz; o revólver hitscan do ticket 09 entra como
   uma classe nova ali, sem tocar no `CombatComponent`. Um `WeaponKind` sem
   implementação (hoje, `Hitscan`) recebe um
   [`NullWeapon`](../../../src/Weapons/NullWeapon.cs) — null object, com
   `GD.PushError` — em vez de deixar o contêiner com referência nula.
   `CombatComponent` agora só faz `Bind`/`Configure`/`Tick` e repassa eventos;
   não sabe mais o que está segurando.
2. **`ActionLock` existe**
   ([`ActionLock.cs`](../../../src/Components/Combat/ActionLock.cs) +
   [`ActionLockSet.cs`](../../../src/Components/Combat/ActionLockSet.cs), POCO
   testado em xUnit pelo mesmo motivo do `MeleeCombo`: janela de tempo é onde
   erro de comparação passa despercebido). Cada golpe agora trava `Movement` e
   `Rotation` da fonte `"combat.attack"`, refrescada a cada quadro enquanto
   `IsAttacking` for verdadeiro — a duração vem do `CombatComponent`, não da
   arma, então qualquer `IWeapon` futuro herda a trava de graça. A trava é por
   fonte e soma por OR: uma segunda fonte (atordoamento, no ticket 11) não é
   liberada cedo demais só porque esta expirou. `MovementComponent` consulta
   `ctx.Combat.ActiveLocks` e ignora WASD e giro enquanto travado, mas continua
   integrando gravidade e repulsão — travar a própria locomoção não devia
   imunizar contra ser lançado por um golpe alheio.

A `CombatProbe` ganhou duas verificações novas: a trava fica ativa durante o
golpe (quadro 2) e se libera sozinha, sem `ClearLock` explícito, assim que a
janela de combo expira (quadro 140). **Não verificado nesta sessão:** o efeito
real do lock sobre o WASD em playtest manual — a CLI não tem como segurar tecla
no editor. A sonda prova a fiação (`ActiveLocks` liga e desliga na hora certa);
vale confirmar no editor que segurar movimento durante o golpe realmente não
desloca o personagem.

**Bugs reais corrigidos, achados pelas sondas e pelo code-review:**

- **Duas classes `[GlobalClass]` no mesmo arquivo:** o Godot registra uma classe
  de script por arquivo, então `MeleeComboStep` nunca existiu como recurso e a
  espada carregava vazia. Separados em arquivos próprios.
- **Alcance e cone mediam em 3D:** a diferença de altura entre quem golpeia e
  quem apanha reprovava o acerto, e ela existe sempre. Agora medem no plano,
  com folga vertical à parte.
- **Janelas fixadas na construção:** os passos 2 e 3 herdavam o tempo do
  passo 1, então o acerto sairia fora da animação.
- **O avanço atravessava parede:** somar em `GlobalPosition` acontece depois do
  `MoveAndSlide`, sem varredura. Com 1 m no terceiro golpe o jogador terminava
  dentro da parede da borda e era cuspido para fora da arena. Agora usa
  `MoveAndCollide`.
- **`GetNodesInGroup` por quadro:** alocava durante toda a janela, contra as
  convenções §5. A lista é amostrada uma vez, na abertura da janela.
- **A repulsão era calculada e ignorada,** então "o terceiro empurra mais"
  falhava. O manequim ganhou locomoção para poder ser empurrado.
- **O jogador não estava no grupo `damageable`:** quando os inimigos ganharem
  armas no M5, ele ficaria silenciosamente inatingível.

**Artefato de teste, não do jogo:** a sonda acusava o primeiro golpe errando. O
jogador encara o **cursor**, e em modo headless o cursor fica no canto da tela —
ele girava para lá logo após o pedido. A sonda passou a manter o alvo à frente
durante o golpe.
