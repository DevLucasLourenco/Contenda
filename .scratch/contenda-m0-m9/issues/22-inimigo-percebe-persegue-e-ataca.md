# 22: Um inimigo percebe o jogador, persegue e ataca com aviso

**What to build:** o primeiro oponente que reage. Ele nota o jogador a certa
distância, encara antes de partir, persegue, para ao chegar perto, avisa que vai
bater, bate, e descansa antes de bater de novo.

**Blocked by:** 07, 08

**Status:** ready-for-agent

- [ ] O inimigo ignora o jogador até ele entrar no raio de percepção
- [ ] Ele **não** persegue através de parede
- [ ] Há um instante de alerta antes de partir para cima — o jogador vê que foi notado
- [ ] O golpe tem aviso visível antes de causar dano: animação, brilho e som
- [ ] Depois de bater ele fica um tempo sem poder bater de novo
- [ ] Apanhar o interrompe; ele se recompõe e volta a perseguir
- [ ] Perder o jogador de vista não faz ele desistir na hora — só depois de um tempo
- [ ] O inimigo usa **os mesmos** componentes de vida, atributos, movimento e
      combate que o jogador; nada de locomoção duplicada

## Comments

O aviso antes do golpe não é enfeite: com câmera fixa e vários inimigos em volta,
sem telegrafia o dano parece aleatório e o jogador culpa o jogo.

O inimigo produz o mesmo tipo de intenção por quadro que o jogador produz a
partir do teclado. É isso que faz o movimento ser um só no projeto inteiro — e é
o que vai permitir, sem código novo, que ele pule e avance com dash.
