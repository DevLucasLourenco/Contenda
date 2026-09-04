# 34: Os dois personagens deixam de ser cápsulas

**What to build:** espadachim e pistoleira ganham corpo, rosto e animação. Andar,
correr, atacar, pular, levar dano e morrer passam a ser vistos, não deduzidos
pela posição de uma cápsula.

**Blocked by:** 33

**Status:** ready-for-agent

- [ ] Os dois usam modelos de licença livre para uso comercial, com origem e
      licença registradas
- [ ] Locomoção mistura parado, andando e correndo sem salto visível
- [ ] A cadeia de golpes de espada é vista como três golpes distintos
- [ ] Tiro, recarga e o braço-canhão da forma têm animação própria
- [ ] Pulo, queda, aterrissagem, dash e mergulho são distinguíveis
- [ ] Levar dano e morrer têm animação
- [ ] As armas ficam presas corretamente à mão, e acompanham o esqueleto
- [ ] O golpe **conecta visualmente no instante em que o dano acontece**
- [ ] O deslocamento continua vindo do código, não da animação
- [ ] Trocar o modelo de um personagem não exige tocar em código de gameplay

## Comments

O penúltimo critério é a armadilha clássica: deixar a animação mover o
personagem parece bom até o primeiro empurrão, quando ele escorrega pelo chão.

Se a animação e a janela de dano não baterem, ajuste a **janela**, não a
animação — trocar o modelo depois traria animações com tempos diferentes e o
ajuste se perderia.
