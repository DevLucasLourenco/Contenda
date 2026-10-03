# 35: Os inimigos e a cidade deixam de ser primitivas

**What to build:** o cenário de blocos vira uma cidade de verdade e os cinco
tipos de inimigo ganham silhuetas distinguíveis — o jogador reconhece a ameaça
antes de ler qualquer barra.

**Blocked by:** 34, 23

**Status:** implementado; pendente playtest visual e medição de desempenho em máquina de referência

- [x] Os cinco tipos de inimigo têm modelo e animação, todos de licença livre
- [ ] As silhuetas são distinguíveis à distância, sob a câmera fixa (modelos distintos configurados; falta conferir no playtest visual)
- [x] Elites se reconhecem de longe sem depender de cor (três espigões geométricos sobre a cabeça)
- [x] A cidade usa peças modulares: ruas, calçadas, prédios, carros, barricadas,
      andaime
- [ ] Os três níveis continuam legíveis depois da arte — falta conferir no playtest visual
- [x] **A navegação foi refeita** depois de mudar a geometria; a sonda confirma oito rotas e o telhado fora do navmesh
- [x] Nada acima de quatro metros dentro da área jogável continua valendo; fachadas ficam fora dela
- [x] Objeto entre a câmera e o jogador some por transparência; sonda valida o fade no modelo importado
- [ ] Quarenta inimigos na cidade com arte final mantêm 60 quadros por segundo (a sonda exercita 40 inimigos, mas FPS gráfico exige máquina com renderização)
- [x] Todo asset tem origem e licença registradas; nenhum é não-comercial

## Comments

Refazer a navegação depois de mexer na geometria é o passo que todo mundo
esquece, e o sintoma é inimigo andando dentro de parede numa onda avançada.

O orçamento de desempenho precisa ser medido **de novo** aqui: o número do
bloqueio com primitivas não diz nada sobre a cidade com arte.

## Implementação

- Os cinco inimigos usam modelos KayKit CC0 com os AnimationSets do rig
  compartilhado; `EnemyRosterProbe` confirma modelo, esqueleto, corpo, árvore
  de animação e clipes Idle/Death para cada espécie.
- Brute e Warlord exibem uma coroa de três espigões geométricos; o teste de
  elenco verifica a coroa para elites/chefes e sua ausência nas outras espécies.
- A cidade usa Kenney CC0: pisos de rua e calçadas instanciados em lote,
  faixas de pedestres, fachadas modulares, contêiner, van, carros, barricadas,
  cercamento e sinalização de obra. Colisões existentes foram mantidas; os
  dois carros acrescentados têm colisão própria.
- `arena_nav.tres` foi rebakeada incluindo os carros. `UrbanArenaProbe` valida
  as rampas, ligações, refúgio e fade no modelo importado.
- `EnemyCrowdProbe` passou com 40 inimigos, mas não mede 60 FPS de renderização
  em modo headless. Também falta a inspeção visual dos três níveis sob a câmera
  fixa.
- URLs, autorias, licenças CC0, datas e modelos escolhidos estão nos
  `SOURCE.md` de cada pack e em `THIRD-PARTY-NOTICES.md`.
