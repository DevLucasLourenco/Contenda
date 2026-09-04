# 11: Peso de impacto — hitstop, knockback, flash e números

**What to build:** bater deixa de ser um número mudo e passa a ser satisfatório.
O golpe congela por um instante, empurra o alvo, faz o material piscar e mostra o
dano subindo na tela.

**Blocked by:** 08.

**Status:** ready-for-agent

- [ ] **hitstop por escala de tempo local** dos dois envolvidos — 0.04 s no golpe
      normal, 0.09 s no finalizador. **Nunca** via `Engine.TimeScale`, que
      congelaria a horda inteira junto no M5
- [ ] knockback com impulso na direção do golpe, decaindo em 0.25 s
- [ ] flash emissivo branco de 0.08 s ao levar dano
- [ ] números de dano flutuantes (`Label3D` billboard, **pooled**), subindo 1 m e
      sumindo em 0.6 s
- [ ] screen shake aditivo — 0.15 ao acertar, 0.3 ao levar dano — que nunca
      altera a rotação base da câmera
- [ ] intensidade do shake respeitando uma configuração (mesmo que o menu só
      chegue no M7)

## Comments

Isto **não** é polimento adiável, e por isso está no M2 e não no M9: é o critério
de pronto do milestone. Se bater num manequim parado não for divertido aqui, não
vai ficar melhor com 40 inimigos em volta.

O hitstop local em vez de global é a decisão que evita retrabalho no M5.
