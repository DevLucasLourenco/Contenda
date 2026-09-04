# 17: O personagem pula e avança com dash

**What to build:** as duas ações de mobilidade que sustentam todo o resto do
desenho novo. Espaço pula com a tolerância que faz o pulo parecer justo; Shift
avança rápido, com um instante de invulnerabilidade que serve de escape.

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] Espaço pula até uma altura configurada **em metros**, não em velocidade
- [ ] Pular logo depois de sair de uma borda ainda funciona
- [ ] Apertar Espaço pouco antes de aterrissar pula assim que toca o chão
- [ ] A queda é mais pesada que a subida — o pulo não flutua
- [ ] O controle no ar é reduzido, mas existe
- [ ] Shift avança na direção do movimento, ou na que o personagem encara se
      estiver parado
- [ ] Durante o dash não dá para curvar
- [ ] O dash concede invulnerabilidade breve e **serve para escapar de atordoamento**
- [ ] O dash **não consome mana**, só recarga
- [ ] Dá para usar o dash uma vez por pulo, no ar
- [ ] A câmera **não balança a cada pulo**, mas acompanha quando o jogador muda
      de patamar

## Comments

O último critério é o que costuma ser esquecido e o que mais incomoda: seguir a
altura do jogador com a mesma suavização usada em X e Z faz a câmera pular junto,
e sob ângulo fixo isso enjoa em minutos. Altura precisa de suavização própria,
muito mais lenta.

O dash não custar mana é decisão de desenho, não esquecimento — ver
[spec 16 §4](../../../docs/specs/16-mobilidade-criticos-e-combate-aereo.md).
