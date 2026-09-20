# Contenda

Contenda é um jogo de ação em arena para PC, desenvolvido com Godot 4.7 .NET e C#. O jogador escolhe um personagem, enfrenta ondas de inimigos e combina ataques, habilidades e transformações para sobreviver.

O projeto está sendo construído como um jogo single-player. A primeira versão pretende oferecer uma partida completa no modo Horde, com uma arena urbana, dois personagens jogáveis e uma progressão de ondas que culmina em um chefe.

## Como o jogo funciona

O jogador se movimenta pela arena, enfrenta grupos de inimigos e administra os recursos do personagem durante cada onda. A câmera acompanha a ação a partir de um ângulo superior fixo, deixando a leitura do espaço e dos inimigos no centro da experiência.

As principais mecânicas planejadas são:

- **Ataques básicos:** o Swordsman usa uma espada e encadeia um combo corpo a corpo; o Gunslinger usa um revólver com tiros hitscan, munição e recarga.
- **Habilidades por sequência:** WASD movimenta o personagem e também registra sequências curtas de comandos. Ao confirmar com M2, a sequência correspondente executa uma habilidade. Por exemplo, `W W` pode executar um avanço ofensivo e `S W` pode lançar um inimigo para o alto.
- **Mana e transformações:** o jogador seleciona uma transformação com o scroll e a ativa com M3. A transformação consome mana na ativação e continua drenando mana enquanto estiver ativa, criando uma decisão entre manter o poder ou guardar recurso para habilidades.
- **Mobilidade e combate aéreo:** o pulo e o dash ajudam a atravessar a arena, escapar de ataques e continuar combos no ar. O dash possui recarga e uma breve janela de invulnerabilidade.
- **Críticos e impacto:** ataques podem causar golpes críticos, com feedback visual e sonoro próprio. Inimigos lançados podem ser perseguidos no ar, mas o sistema limita a duração desses combos para manter o combate controlado.
- **Ondas de inimigos:** cada onda define sua composição, ritmo de surgimento e limite de inimigos ativos. O jogador precisa lidar com inimigos que perseguem, atacam, cercam e ocupam diferentes áreas da arena.

## Personagens

O MVP começa com dois arquétipos que compartilham o mesmo vocabulário de comandos, mas exigem estilos de jogo diferentes:

| Personagem | Estilo | Ataque básico | Transformação |
|---|---|---|---|
| **Swordsman** | combate próximo, dano alto e resistência maior | combo de espada | Berserker |
| **Gunslinger** | distância, mobilidade e controle de espaço | tiros de revólver | Overdrive |

As mesmas sequências podem representar funções semelhantes nos dois personagens. Assim, o jogador aprende uma linguagem de combate que permanece familiar ao trocar de arquétipo, enquanto as armas e os efeitos mudam a forma de jogar.

## Estrutura do projeto

O conteúdo do jogo é organizado em recursos editáveis (`.tres`), incluindo personagens, armas, habilidades, transformações, inimigos e ondas. A lógica de gameplay é separada em componentes de movimento, vida, mana, combate, habilidades, IA e targeting.

Essa estrutura permite testar regras importantes fora da engine e ajustar conteúdo sem espalhar valores de balanceamento pelo código. A pasta `tests/` contém os testes automatizados; as cenas em `scenes/debug/` servem para validar sistemas isolados durante o desenvolvimento.

## Estado atual

Contenda está em desenvolvimento ativo. O repositório já contém a fundação do projeto, a arena e a câmera, os componentes principais de personagem, armas, habilidades, vida e mana, além de sistemas para inimigos, ondas, HUD e cenas de teste.

A prioridade atual é transformar esses sistemas em um ciclo de jogo completo e consistente: combate, mobilidade, inimigos, progressão de ondas, interface e polimento. O andamento detalhado está no [roadmap](docs/plans/roadmap.md).

## O que o projeto pode se tornar

A primeira entrega é o MVP 0.1: uma partida completa de Horde com cinco ondas, dois personagens, transformações, chefes, HUD, menus, configurações e uma arena urbana com diferentes níveis de altura.

Depois do MVP, a base do projeto pode crescer em várias direções:

- Horde infinito com escalonamento de dificuldade;
- novas habilidades, transformações, personagens, inimigos e arenas;
- modos como Survival, Boss Rush e 1v1;
- itens, buffs, progressão entre partidas e desafios de dificuldade;
- suporte a gamepad, replay, modding baseado em dados e, em uma etapa mais distante, multiplayer online.

Essas possibilidades ainda são parte do backlog. O objetivo imediato é validar e polir o núcleo: movimentar-se bem, executar habilidades por sequência e tomar boas decisões durante uma horda.

## Desenvolvimento

É necessário usar a edição **.NET** do Godot 4.7, pois o projeto utiliza C#. Abra o projeto pelo editor Godot .NET ou pelo atalho `tools/abrir-editor.cmd`.

Para compilar a solution:

```bash
dotnet build Contenda.sln -c ExportRelease
```

Para executar os testes:

```bash
dotnet test Contenda.sln
```

Convenções de código, critérios de qualidade e organização do trabalho estão em [`docs/plans/convencoes-de-codigo.md`](docs/plans/convencoes-de-codigo.md).

## Documentação

Comece pela [visão geral do projeto](docs/00-visao-geral.md) ou pelo índice em [docs/README.md](docs/README.md).

- [Especificações](docs/specs/): regras de câmera, input, combate, habilidades, personagens, IA, Horde, UI e dados.
- [Roadmap](docs/plans/roadmap.md): milestones e entregas planejadas.
- [Riscos e decisões](docs/plans/riscos-e-decisoes.md): decisões técnicas e limites conhecidos.
- [Testes](tests/): testes automatizados e testes de cenas.
