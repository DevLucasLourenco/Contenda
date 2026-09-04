# 06: Verificador anti-2D no CI

**What to build:** o CI reprova automaticamente qualquer tentativa de introduzir
um nó 2D numa cena de gameplay. A regra que define o projeto deixa de depender de
disciplina humana e passa a ser garantida por construção.

**Blocked by:** 03

**Status:** concluído

- [x] Adicionar um nó 2D a uma cena de gameplay reprova o CI
- [x] Cenas de UI continuam livres para usar nós 2D, sem falso positivo
- [x] A mensagem de erro diz qual arquivo, qual nó, e aponta a spec 02
- [x] O verificador roda em todo push e PR
- [x] Também acusa classe C# herdando de nó 2D — a outra porta de entrada, que
      não aparece em cena nenhuma até alguém instanciá-la, inclusive com base
      qualificada (`Godot.Node2D`) e declaração quebrada em várias linhas
- [x] Também acusa cena de interface **instanciada dentro do mundo**, que não
      declara `type=` e por isso escaparia da varredura de nós
- [x] Reconhece 2D por **padrão** (`*2D`), não por lista à mão — um
      `AudioStreamPlayer2D` é pego mesmo sem estar em lista nenhuma
- [x] **Falha fechado:** se nada for varrido, sai com erro em vez de dizer que
      está tudo bem
- [x] O próprio verificador tem autoteste com 8 casos, isolado num diretório
      temporário, rodando antes dele no CI

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

---

**A aresta para o ticket 04 foi removida na implementação.** Ela partia da ideia
de que não haveria cenas para verificar antes da arena existir. Mas uma trava que
existe **antes** do que ela protege é estritamente melhor: assim a arena não pode
nascer com nó 2D dentro. O verificador não precisa da arena — precisa apenas de
uma cena qualquer, e `scenes/Main.tscn` já existe desde o ticket 02.

Guarda antes do guardado é a ordem certa.

**Ressalva honesta:** o primeiro critério está provado contra cenas sintéticas do
autoteste e contra `scenes/Main.tscn`, não contra a arena de verdade, que ainda
não existe. Vale reconferir quando o ticket 04 entregar a cidade.
