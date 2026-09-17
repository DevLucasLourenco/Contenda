# 26: Dá para saber quanto falta para cada inimigo cair

**What to build:** cada inimigo mostra a própria vida acima da cabeça, e os que
são mais perigosos se anunciam como tais. O chefe ganha uma barra própria no
alto da tela.

**Blocked by:** 25

**Status:** done

- [x] A barra aparece sobre o inimigo só depois que ele apanha pela primeira vez
- [x] Ela some sozinha algum tempo depois do último golpe
- [x] A barra é legível sob a câmera fixa, em qualquer um dos três níveis da cidade
- [x] Elites se distinguem de longe, e não só pela barra maior
- [x] O chefe tem barra própria no alto da tela, com nome
- [x] Com quarenta inimigos apanhando ao mesmo tempo, as barras não derrubam o
      desempenho
- [x] **Nenhum nó 2D no mundo** — a vida sobre o inimigo é 3D ou projetada da
      câmera; `Control` só na interface

## Comments

Vida de inimigo no HUD central não faz sentido em modo horda: são muitos e o
jogador precisa saber de qual está perto de derrubar. Sobre a cabeça é onde o
olho já está.

O último critério existe porque este é o ticket mais tentador do projeto para
alguém resolver com um `Sprite2D` no mundo — que é reprovação de review.

## Implementação

**Bar por inimigo (1-3):** já existia. `WorldHealthBar` (ticket 22) já cobria
"aparece só ao apanhar" e "some sozinha depois de `HideAfter`" -- billboard
3D, `QuadMesh`, nada de `Sprite2D`. Nenhuma mudança de código aqui; "legível
em qualquer um dos três níveis da cidade" (ticket 23, ainda não construída)
vale por design (billboard acompanha a câmera em qualquer posição/altura),
mas não há como testar contra a cidade de verdade antes dela existir.

**Elite (4):** `EnemyDefinition` ganhou `IsElite`/`EliteTint` (spec 09 §5,
"elite dourado" da spec 11 §4 como padrão). Novo componente
`EliteMarkerComponent` tinge `MeshInstance3D.MaterialOverride` da malha do
corpo, permanente -- mesma técnica de `DamageFlashComponent`/
`AttackTelegraphComponent` (sem modelo/textura de verdade ainda, ADR-010).

Spec 11 §2.4 é explícita: "Elites e boss têm barra maior, COM NOME" -- não só
o tingimento da própria malha. `WorldHealthBar` ganhou `DestaqueScale`
(cresce a barra inteira, 1.4× por padrão) e um `Label3D` (`NameLabelPath`)
que mostra `CharacterDefinition.DisplayName` quando `IsElite`/`IsBoss`,
aplicado em `Configure`/`ResetForSpawn` (mesma disciplina do ticket 25: só o
primeiro roda de novo por vida inteira do nó pooled). Tingir sozinho também
violaria a própria regra de daltonismo da spec 11 §4 ("nunca comunicar só
por cor: forma/ícone/texto sempre acompanham") -- tamanho e nome são os
sinais que não dependem de cor nenhuma.

**Achado real rodando o probe, não só calibração:** `DamageFlashComponent` e
`AttackTelegraphComponent` já disputavam a mesma `MaterialOverride` desde os
tickets 07/09, cada um "desligando" com `null` direto. Um tingimento
PERMANENTE de elite quebra essa suposição -- o primeiro flash de dano ou
windup de ataque apagaria o tingimento para sempre. Corrigido com
`CharacterContext.BodyBaseMaterial` (nulo para a maioria, o material da
elite quando houver uma): os dois agora restauram A ELE, não a `null` direto.

Isso expôs um SEGUNDO bug, pré-existente e sem relação com elite nenhuma:
`EnemyBrain.AoApanhar` chama `AttackTelegraph.DesligarAviso()`
INCONDICIONALMENTE em todo golpe recebido (só por garantia de cancelar um
windup em andamento), e `DamageFlashComponent` também assina o MESMO
`Health.Damaged`. Como `DesligarAviso()` escrevia a `MaterialOverride`
mesmo sem telegrafia nenhuma ativa, e os dois handlers rodam no MESMO
despacho síncrono do evento, `DesligarAviso()` apagava o flash de dano que
acabara de acender um instante antes -- o flash de um inimigo, na prática,
nunca chegava a aparecer, para NENHUM inimigo, desde que `EnemyBrain`
existe (ticket 22). Corrigido tornando `DesligarAviso()` um no-op de
verdade quando `IsWarning` já é falso, em vez de sempre reescrever a malha.

**Chefe (5):** `EnemyDefinition.IsBoss` -- ao `Configure`/`ResetForSpawn`,
`EnemyBrain` se anuncia em `GameSession.BossBody` (mesmo espírito de
`PlayerBody`, ticket 22: quem é anuncia, ninguém procura por caminho), e
`AoMorrer` limpa o campo. Novo `BossHealthBar.cs` + nó `Chefe` em
`Hud.tscn`, mesmo desenho de `HealthBar` (camada de dano,
`_PhysicsProcess` por causa dos probes headless) com `Bind`/`Unbind` (ao
contrário do jogador, um chefe pode morrer no meio da partida).
`HudController` ganhou `AtualizarChefe()`, rodando TODO quadro (não só até
achar a primeira vez, como o jogador) lendo `GameSession.BossBody` -- nunca
`GetNodesInGroup` por quadro, proibido pelas convenções §5. O nome exibido
vem de `CharacterDefinition.DisplayName`, o mesmo campo que qualquer
arquétipo do jogador já usa.

**Bug real de propagação, achado rodando o probe:** `EnemyPool.Acquire`
nunca de fato aplicava a `EnemyDefinition` recebida ao `EnemyBrain` do
inimigo -- a cena sempre trazia a PRÓPRIA definição hard-coded
(`EnemyGrunt.tscn` → `grunt.tres`), e o parâmetro `definicao` só servia de
chave para o dicionário interno do pool. Isto nunca apareceu antes porque o
único uso real (ticket 25) sempre passava a MESMA definição já baked na
cena. Corrigido em `CriarInstancia`: agora atribui `cerebro.Definition =
definicao;` na criação -- e como `Acquire` já chama `ResetForSpawn` na
sequência (ticket 25), tudo que depende de `Definition` (máquina de estado,
tingimento de elite, anúncio de chefe) se reconstrói correto. Isto também
significa que uma variante elite/chefe pode reusar a MESMA cena
(`EnemyGrunt.tscn`) com um `.tres` diferente, sem precisar de uma cena nova
por variante -- exatamente o que o probe deste ticket faz.

**Desempenho com 40 inimigos (6):** nada de novo por quadro além do que já
existia -- `WorldHealthBar` só atualiza (`Size`) quando já está vinculada, e
`AtualizarChefe` é uma leitura de property, não uma varredura. Sem
profiler automatizado neste projeto (é um probe, não um profiler): a
garantia é de desenho (nenhuma alocação nem busca por grupo por quadro),
não uma medição de frame time.

**Probe novo:** `src/Tools/EnemyVisibilityProbe.cs` +
`scenes/debug/EnemyVisibilityProbe.tscn`. Constrói uma `EnemyDefinition`
elite e uma de chefe em runtime (fixtures de teste sobre o MESMO
`EnemyGrunt.tscn`, não `.tres` novos -- não é conteúdo real do roster).
Cinco fases: adquirir a elite e confirmar o tingimento, a barra maior E o
nome visível; sobreviver a um flash de dano de verdade (esperando o sinal
`IsFlashing`, não um número de quadros chutado); sobreviver a uma telegrafia
de ataque ligada/desligada de verdade; o chefe aparecer na barra própria com
vida cheia; e o chefe morrer e a barra sumir de novo.

### Code review

Duas revisões em paralelo (Standards e Spec) sobre o diff staged. Achados
reais, corrigidos:

- **Spec, requisito incompleto:** a implementação original só tingia a
  malha da elite, mas a spec 11 §2.4 pede "barra maior, COM NOME" -- as
  duas coisas, não só a cor. Pior: comunicar só por cor viola a própria
  regra de daltonismo da spec 11 §4. Corrigido: `WorldHealthBar` ganhou
  `DestaqueScale` (a barra inteira cresce) e um `Label3D` com o nome,
  aplicados junto com o tingimento. Probe atualizado para cobrir os dois.
- **Standards, convenções §6 (`!` sem comentário):** três casos sem a
  justificativa exigida -- `HudController.AtualizarChefe` (a garantia vem
  da variável `vivo` computada antes, que o compilador não enxerga através
  dela) e `EliteMarkerComponent.AplicarOuLimpar` (`def!.EliteTint`, coberto
  pelo `if (!IsElite) return` logo acima). Comentários adicionados nos dois.
- **Standards, Duplicated Code:** `DamageFlashComponent.Desligar` e
  `AttackTelegraphComponent.DesligarAviso` repetiam a mesma linha de
  restaurar `MaterialOverride` ao material de base. Extraído
  `CharacterContext.RestaurarMaterialDaMalha`, ponto único agora que um
  terceiro escritor permanente (`EliteMarkerComponent`) vive na mesma
  vizinhança.
- **Spec, julgamento (sem mudança):** os dois bugs "achados via probe"
  (`BodyBaseMaterial` e a propagação de `EnemyDefinition` no `EnemyPool`)
  foram avaliados quanto a escopo -- o primeiro é pré-requisito direto do
  tingimento permanente deste próprio ticket; o segundo é pré-requisito
  direto de qualquer variante elite/chefe reusar a mesma cena. Os dois
  ficam. O terceiro achado (`DesligarAviso` apagando o flash de dano de
  QUALQUER inimigo, não só elite) é scope creep genuíno -- uma correção de
  um bug pré-existente sem relação com elite/chefe, encontrado incidentalmente
  pela mesma investigação. Mantido no mesmo commit por pragmatismo (mesma
  causa raiz, mesmo arquivo, custo de reabrir a investigação depois seria
  maior que o custo de misturar), mas registrado aqui explicitamente para
  quem revisar o histórico depois.
- **Spec, honestidade do checklist (sem mudança):** os itens 3 ("legível
  nos três níveis da cidade") e 6 ("40 inimigos sem engasgo") já eram
  documentados como não-medidos/não-testáveis contra a cidade real (que
  ainda não existe) -- mantido como estava, já é a divulgação correta.

Gate completo re-executado após as correções: 325/325 testes, 14/14 probes
headless (incluindo `EnemyVisibilityProbe` 3x seguidas), `ExportRelease` e
`check-no-2d.sh` limpos.
