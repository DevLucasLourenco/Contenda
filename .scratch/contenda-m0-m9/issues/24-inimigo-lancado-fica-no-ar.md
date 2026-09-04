# 24: Inimigo lançado fica no ar e volta a lutar ao cair

**What to build:** o outro lado do combate aéreo. Um inimigo atingido pelo
anti-aéreo para de tentar andar, sobe, pode ser mantido no alto por golpes
aéreos, e ao tocar o chão se recompõe e volta a perseguir.

**Blocked by:** 22, 19

**Status:** ready-for-agent

- [ ] Um inimigo lançado **para de tentar andar** enquanto está no ar
- [ ] Ele não ataca nem recalcula rota enquanto está no ar
- [ ] Ao tocar o chão ele se recompõe e volta a perseguir normalmente
- [ ] Golpes aéreos o mantêm no alto, até o limite de acertos
- [ ] Passado esse limite, ele cai mesmo que continue apanhando
- [ ] Cair não o mata nem o machuca por queda
- [ ] O estado aéreo pode ser entrado a partir de perseguição, ataque ou descanso
- [ ] Reciclar um inimigo que morreu no ar não o traz de volta flutuando

## Comments

Sem este ticket o anti-aéreo lança um inimigo que continua andando no ar, o que
parece bug mesmo sendo consequência de não haver estado para isso.

O limite de acertos aéreos existe para o modo horda: sem ele, um jogador
habilidoso prende um inimigo no alto indefinidamente enquanto os outros trinta
esperam a vez.
