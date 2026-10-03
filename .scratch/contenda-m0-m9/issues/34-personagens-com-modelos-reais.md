# 34: Os dois personagens deixam de ser cápsulas

**What to build:** espadachim e pistoleira ganham corpo, rosto e animação. Andar,
correr, atacar, pular, levar dano e morrer passam a ser vistos, não deduzidos
pela posição de uma cápsula.

**Blocked by:** 33

**Status:** concluído

- [x] Os dois usam modelos de licença livre para uso comercial, com origem e
      licença registradas
- [x] Locomoção mistura parado, andando e correndo sem salto visível
- [x] A cadeia de golpes de espada é vista como três golpes distintos
- [x] Tiro, recarga e o braço-canhão da forma têm animação própria
- [x] Pulo, queda, aterrissagem, dash e mergulho são distinguíveis
- [x] Levar dano e morrer têm animação
- [x] As armas ficam presas corretamente à mão, e acompanham o esqueleto
- [x] O golpe **conecta visualmente no instante em que o dano acontece**
- [x] O deslocamento continua vindo do código, não da animação
- [x] Trocar o modelo de um personagem não exige tocar em código de gameplay

## Comments

O penúltimo critério é a armadilha clássica: deixar a animação mover o
personagem parece bom até o primeiro empurrão, quando ele escorrega pelo chão.

Se a animação e a janela de dano não baterem, ajuste a **janela**, não a
animação — trocar o modelo depois traria animações com tempos diferentes e o
ajuste se perderia.

- Validação final em 2026-10-02: build sem avisos; 436 testes xUnit passaram;
  `CharacterSelectProbe` e `TransformationProbe` passaram no Godot 4.7.2.
