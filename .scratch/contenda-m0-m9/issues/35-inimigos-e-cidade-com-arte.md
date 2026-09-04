# 35: Os inimigos e a cidade deixam de ser primitivas

**What to build:** o cenário de blocos vira uma cidade de verdade e os cinco
tipos de inimigo ganham silhuetas distinguíveis — o jogador reconhece a ameaça
antes de ler qualquer barra.

**Blocked by:** 34, 23

**Status:** ready-for-agent

- [ ] Os cinco tipos de inimigo têm modelo e animação, todos de licença livre
- [ ] As silhuetas são distinguíveis à distância, sob a câmera fixa
- [ ] Elites se reconhecem de longe sem depender de cor
- [ ] A cidade usa peças modulares: ruas, calçadas, prédios, carros, barricadas,
      andaime
- [ ] Os três níveis continuam legíveis depois da arte — não ficaram mais confusos
- [ ] **A navegação foi refeita** depois de mudar a geometria
- [ ] Nada acima de quatro metros dentro da área jogável continua valendo
- [ ] Objeto entre a câmera e o jogador some por transparência
- [ ] Quarenta inimigos na cidade com arte final mantêm 60 quadros por segundo
- [ ] Todo asset tem origem e licença registradas; nenhum é não-comercial

## Comments

Refazer a navegação depois de mexer na geometria é o passo que todo mundo
esquece, e o sintoma é inimigo andando dentro de parede numa onda avançada.

O orçamento de desempenho precisa ser medido **de novo** aqui: o número do
bloqueio com primitivas não diz nada sobre a cidade com arte.
