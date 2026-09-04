# 23: Inimigos atravessam a cidade sem se amontoar nem travar

**What to build:** vários inimigos convergindo para o jogador ao mesmo tempo,
contornando carros e barricadas, descendo para a praça, subindo na caçamba — e
chegando espalhados, não empilhados uns dentro dos outros.

**Blocked by:** 22, 21

**Status:** ready-for-agent

- [ ] Inimigos contornam obstáculos em vez de encostar e ficar raspando na parede
- [ ] Descem e sobem as rampas da praça sem travar em quina
- [ ] Sobem nos objetos escaláveis pelas ligações de navegação
- [ ] Vinte inimigos convergindo num ponto chegam **espalhados**, não sobrepostos
- [ ] Um inimigo sem caminho válido não congela: avança na direção do jogador e
      tenta de novo
- [ ] O recálculo de rota é distribuído no tempo, não todos no mesmo quadro
- [ ] Inimigo distante pensa menos vezes por segundo que inimigo próximo
- [ ] Quarenta inimigos ativos mantêm 60 quadros por segundo

## Comments

Amontoado é o que mais estraga combate corpo a corpo com câmera fixa: o jogador
perde a leitura de quantos são e de onde vem o golpe. Evitação de colisão
sozinha não resolve — precisa de uma força de separação leve por cima.

Recalcular rota de quarenta inimigos no mesmo quadro produz um engasgo visível.
Distribuir a fase entre eles é mais barato que qualquer otimização posterior.
