# 10: Mana com consumo atômico e regeneração atrasada

**What to build:** o personagem tem mana. Gastar mana desconta o valor e
**pausa** a regeneração por um instante, para que gastar tenha custo de verdade;
depois ela volta a subir sozinha. Tentar gastar sem saldo não gasta nada e não
deixa o recurso num estado intermediário.

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] Gastar mana desconta o valor e pausa a regeneração por cerca de um segundo
- [ ] Passada a pausa, a mana volta a subir até o máximo
- [ ] Tentar gastar sem saldo suficiente **não altera nada**
- [ ] Existe um consumo parcial, que leva o que houver e avisa ao zerar
- [ ] Existe um jeito de devolver a mana ao estado de recém-criado
- [ ] Testes cobrindo consumo atômico, consumo parcial e o atraso da regeneração

## Comments

Duas formas de gastar, e a distinção **não** é detalhe de implementação:

- **Atômico** (tudo ou nada) — usado por habilidades no M3. Se não há mana, a
  habilidade simplesmente não sai, e nada é alterado.
- **Parcial** (leva o que houver e avisa ao zerar) — usado por transformações no
  M4. O dreno contínuo não pode "falhar": ele esgota e força a reversão.

Escolher errado aqui aparece só no M4, como uma transformação que não reverte.

Independente dos tickets 08 e 09: só precisa da base do ticket 07. É o segundo
trilho, se houver duas pessoas.
