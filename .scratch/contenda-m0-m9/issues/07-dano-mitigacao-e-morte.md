# 07: Manequim leva dano com mitigação e morre uma vez só

**What to build:** existe um manequim de treino na arena. Aplicar dano nele
reduz a vida já descontada da defesa, mostra que ele apanhou, e ao chegar a zero
ele morre — uma vez só, mesmo levando dois golpes no mesmo instante. É a
primeira vez que o jogo tem consequência.

**Blocked by:** 05

**Status:** implementado — falta apertar M1 com teclado

- [x] O manequim tem vida visível e apanha
- [x] O dano recebido é reduzido pela defesa do alvo
- [x] Dois golpes no mesmo frame disparam morte **uma** vez
- [x] Um alvo recém-atingido fica brevemente invulnerável
- [x] Adicionar e depois remover um modificador devolve o atributo ao valor base
      **exato**
- [x] Mudar a vida máxima não mata nem cura de graça
- [x] Existe um jeito de devolver o alvo ao estado de recém-criado
- [x] Testes cobrindo dano, mitigação, morte idempotente e reversão de modificador

## Comments

Este ticket entrega dois pilares que quase todo o resto do jogo usa.

**O bloco de atributos com modificadores por fonte.** Transformações, buffs e
equipamentos nunca escrevem em campos de outros componentes: escrevem
modificadores marcados com sua origem, e os componentes leem o valor final.
É isso que torna reverter uma transformação, no M4, uma única chamada em vez de
uma fonte permanente de bugs. Ver ADR-006.

**A fila de dano.** Dano recebido no frame é enfileirado e resolvido num ponto
único, senão dois golpes simultâneos disparam morte duas vezes. Está na lista de
bugs previsíveis da spec 15, §5.

O "jeito de devolver ao estado de recém-criado" parece prematuro aqui, mas é o
contrato de reciclagem de que o pool de inimigos depende no M5. Sai barato agora
e caro depois.

---

**Implementado em 2026-09-04.** 64 testes verdes, build e verificador limpos, e
uma sonda headless que verifica a cadeia inteira na árvore de nós real.

**A sonda pegou um bug que os testes unitários não alcançavam.** Dois golpes de
60 num alvo de 100 não o matavam: eu ligava a invulnerabilidade **dentro** do
laço da fila, então o primeiro golpe do quadro bloqueava o segundo — anulando o
propósito da fila. Só aparece com os componentes montados; `HealthState`
isolado não sabe que existe fila. Agora os i-frames ligam depois de a fila
inteira ser drenada.

**Achados do code-review corrigidos:**

- a vida máxima não chegava ao estado pelo caminho dos atributos, então o
  critério "mudar a vida máxima não mata nem cura de graça" só passava em POCO;
  agora `MaxHealth` passa pelo `StatBlock`, que é como uma transformação vai
  alterá-la no M4
- balanceamento estava cravado na cena; virou `HealthDefinition` em `.tres`,
  como a regra 4 exige
- o preenchimento da barra deslizava para fora do fundo: eu deduzia meia largura
  fixa de 0,5 numa barra de 1,2 m
- `Configure` reassinava o estado sem desassinar o anterior, e `Bind` podia
  assinar duas vezes num nó reciclado
- i-frames contavam por relógio de parede, que continua correndo com o jogo
  pausado; agora contam por `delta`
- `DamageInfo` estava em `Components/Combat/` declarando namespace `Health`
- `Kill` dizia "sem passar pela fila" e enfileirava

**Em aberto:** apertar M1 e ver o manequim apanhar. O `DebugDamageDealer` é
andaime declarado e sai no ticket 08, junto com o `IWeapon` de verdade.

**Divergência registrada:** a spec 04 §2 dá zero i-frames a inimigos; o manequim
usa 0,25 s de propósito, por existir para demonstrar que a invulnerabilidade
funciona. Está anotado no `HealthDefinition`.
