# 03: Testes, CI verde e regras de estilo

**What to build:** um push abre um PR e o CI diz sozinho se o trabalho presta:
compila, os testes passam, e um warning introduzido de propósito reprova o
build. A partir daqui todo ticket seguinte tem onde colocar teste.

**Blocked by:** 01

**Status:** concluído

- [x] Existe um projeto de testes unitários na solução, com pelo menos um teste
      real passando
- [x] Existe um projeto separado para testes de cena, ainda que vazio
- [x] O CI roda build e testes em todo push e PR
- [x] Um warning introduzido de propósito reprova o build no CI
- [x] Existe `.editorconfig` com as regras de estilo do projeto
- [x] A checagem de estilo em build é reativada junto com o `.editorconfig`

## Comments

Escopo ampliado por dois órfãos que o eixo Spec do `/code-review` apontou:

- **projeto de testes de cena** — a spec de arquitetura §2 lista dois projetos
  de teste, e o segundo não estava em nenhum ticket. Ele sumiria em silêncio.
- **`.editorconfig`** — o ticket 01 **desligou** a checagem de estilo em build
  de propósito: ligada sem ruleset, ela passa despercebida, e no dia em que o
  `.editorconfig` entrasse as regras de estilo virariam erro de build de uma vez.
  Os dois têm que entrar juntos, e é aqui.

O projeto de testes unitários **não** referencia o GodotSharp para lógica pura.
A regra de projeto é que classes com lógica testável não herdam de `Node` —
componentes são cascas finas sobre objetos comuns. Ver spec 15, §1.

O job do Godot headless pode ficar não-bloqueante por enquanto; ele vira
obrigatório no M9.
