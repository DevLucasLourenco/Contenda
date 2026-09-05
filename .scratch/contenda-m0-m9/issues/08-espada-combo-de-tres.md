# 08: Ataque básico com espada encadeia três golpes

**What to build:** clicar o botão esquerdo com o espadachim dá um golpe de
espada. Clicar de novo dentro da janela encadeia o segundo, e depois o terceiro,
mais forte e com mais empurrão. Deixar a janela passar volta ao primeiro. Cada
golpe avança um pouco o personagem, para que acertar sob a câmera fixa seja
possível.

**Blocked by:** 07

**Status:** PARCIAL — dois requisitos do próprio ticket não foram feitos

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
duas sondas headless verificando a cadeia na árvore real.

**O que NÃO foi feito, e é do escopo deste ticket:**

1. **`IWeapon` não existe.** O `CombatComponent` **é** a espada: estado do combo,
   varredura em cone e avanço estão embutidos nele. A razão declarada da
   abstração — "permite o ticket 09 existir sem um único `if`" — continua sem
   base. O `WeaponDefinition.Kind` existe mas ninguém ramifica nele; ele é
   semente do `if` que a spec 07 §1 proíbe, não a solução.
2. **`ActionLock` não existe.** A spec 07 §8 e os comentários deste ticket pedem
   trava por fonte e com duração durante o golpe. Movimento e rotação seguem
   livres no meio do ataque; a única contenção é o combo recusar reinício antes
   da janela abrir.

Fechar isto exige um ticket próprio, ou reabrir este. **Não marque como
concluído.**

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
