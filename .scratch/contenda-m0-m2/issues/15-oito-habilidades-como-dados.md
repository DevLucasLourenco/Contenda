# 15: Os dois personagens têm quatro habilidades cada, todas como dados

**What to build:** o repertório completo do MVP. Investida, anti-aéreo, área e
finalizador, para espadachim e pistoleira — com efeitos genuinamente diferentes:
corte em cone, avanço com dano no trajeto, lançamento para cima, tiro
instantâneo, rajada e projétil explosivo.

**Blocked by:** 14, 09

**Status:** ready-for-agent

- [ ] As oito habilidades existem e funcionam contra o manequim
- [ ] As quatro sequências significam a mesma coisa nos dois personagens, para
      que a memória muscular seja transferível
- [ ] Nenhuma habilidade é referenciada por nome dentro do código
- [ ] Um novo tipo de efeito entra sem alterar quem executa habilidades
- [ ] Uma nona habilidade hipotética exige **apenas** um arquivo de dados e uma
      entrada na lista do personagem
- [ ] Nenhuma sequência de um personagem é prefixo de outra dele que já dispare
- [ ] O projétil explosivo é reaproveitado de um pool, não criado e destruído

## Comments

O critério que separa sucesso de fracasso aqui não é "as oito funcionam" — é a
ausência de código específico por habilidade. Se `DashSlash` aparecer como nome
de classe ou de `case`, o desenho data-driven falhou mesmo com o jogo rodando.
