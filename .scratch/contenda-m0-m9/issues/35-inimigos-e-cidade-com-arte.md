# 35: Os inimigos e a cidade deixam de ser primitivas

**What to build:** o cenário de blocos vira uma cidade de verdade e os cinco
tipos de inimigo ganham silhuetas distinguíveis — o jogador reconhece a ameaça
antes de ler qualquer barra.

**Blocked by:** 34, 23

**Status:** concluído; validação visual e de desempenho aprovada pelo usuário

- [x] Os cinco tipos de inimigo têm modelo e animação, todos de licença livre
- [x] As silhuetas são distinguíveis à distância, sob a câmera fixa (validação visual aprovada pelo usuário)
- [x] Elites se reconhecem de longe sem depender de cor (três espigões geométricos sobre a cabeça)
- [x] A cidade usa peças modulares: ruas, calçadas, prédios, carros, barricadas,
      andaime
- [x] Os três níveis continuam legíveis depois da arte (validação visual aprovada pelo usuário)
- [x] **A navegação foi refeita** depois de mudar a geometria; a sonda confirma oito rotas e o telhado fora do navmesh
- [x] Nada acima de quatro metros dentro da área jogável continua valendo; fachadas ficam fora dela
- [x] Objeto entre a câmera e o jogador some por transparência; sonda valida o fade no modelo importado
- [x] Quarenta inimigos na cidade com arte final mantêm 60 quadros por segundo (validação de desempenho aprovada pelo usuário)
- [x] Todo asset tem origem e licença registradas; nenhum é não-comercial

## Comments

Refazer a navegação depois de mexer na geometria é o passo que todo mundo
esquece, e o sintoma é inimigo andando dentro de parede numa onda avançada.

O orçamento de desempenho precisa ser medido **de novo** aqui: o número do
bloqueio com primitivas não diz nada sobre a cidade com arte.

Em 2026-10-03, o usuário confirmou que a validação visual e de desempenho foi
aprovada. A confirmação não incluiu o FPS registrado nem o hardware usado.

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
  em modo headless. O usuário confirmou a aprovação do playtest visual e da
  medição gráfica em 2026-10-03; o valor de FPS e o hardware não foram
  registrados nesta conversa.
- URLs, autorias, licenças CC0, datas e modelos escolhidos estão nos
  `SOURCE.md` de cada pack e em `THIRD-PARTY-NOTICES.md`.
