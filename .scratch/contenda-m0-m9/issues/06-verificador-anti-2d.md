# 06: Verificador anti-2D no CI

**What to build:** o CI reprova automaticamente qualquer tentativa de introduzir
um nó 2D numa cena de gameplay. A regra que define o projeto deixa de depender de
disciplina humana e passa a ser garantida por construção.

**Blocked by:** 03, 04

**Status:** ready-for-agent

- [ ] Adicionar um nó 2D a uma cena de gameplay reprova o CI
- [ ] Cenas de UI continuam livres para usar nós 2D, sem falso positivo
- [ ] A mensagem de erro diz qual arquivo, qual nó, e aponta a spec 02
- [ ] O verificador roda em todo push e PR

## Comments

Não é fatia vertical — é uma trava. Está cedo na ordem de propósito.

O problema histórico das versões anteriores deste projeto foi justamente a
deriva para 2D. As specs proíbem `Node2D`, `CharacterBody2D`, `Camera2D`,
`Sprite2D`, `Area2D` e `TileMap` no gameplay (ver spec 02, §2), mas proibição em
documento não impede ninguém. Este ticket transforma a proibição em erro de
build.

Cuidado com o falso positivo: `Control` e `CanvasLayer` são 2D por natureza e
são **permitidos** para interface. O verificador precisa distinguir cena de
gameplay de cena de UI.

Barras de vida sobre inimigos, no M5, também são um caso a não quebrar: elas são
resolvidas com nós 3D ou projeção de coordenada, nunca com nó 2D no mundo.
