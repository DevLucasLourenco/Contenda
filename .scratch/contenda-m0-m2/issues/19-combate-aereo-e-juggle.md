# 19: Golpes no ar mantêm o inimigo lançado no alto

**What to build:** o circuito que fecha o combo vertical do espadachim. Lançar o
inimigo com o anti-aéreo, persegui-lo pulando, mantê-lo no ar com golpes rápidos
e terminar com um mergulho que explode em área ao aterrissar.

**Blocked by:** 17, 15

**Status:** ready-for-agent

- [ ] Atacar no ar dá uma cadeia curta, mais rápida e mais fraca que a de solo
- [ ] Cada acerto aéreo dá um empurrãozinho para cima no alvo **e no atacante**,
      sustentando o combo
- [ ] A gravidade cai durante a janela do golpe — o ataque segura no ar, sem virar voo
- [ ] Segurar o ataque depois do pico faz o personagem mergulhar
- [ ] O mergulho causa dano em área ao aterrissar, com repulsão
- [ ] O mergulho tem recuperação longa ao tocar o chão — é o risco que paga o poder
- [ ] Um inimigo lançado **para de tentar andar** enquanto está no ar
- [ ] Nenhum inimigo passa de quatro acertos aéreos seguidos: depois disso ele cai
- [ ] O anti-aéreo existente encadeia naturalmente com tudo isso

## Comments

O teto de quatro acertos existe para que o modo horda não vire exibição de
malabarismo: sem ele, um jogador habilidoso prende um inimigo no ar
indefinidamente enquanto os outros trinta esperam.

O estado "no ar" do inimigo é novo na máquina de estados e precisa entrar de
qualquer estado — inclusive de perseguição e de ataque.
