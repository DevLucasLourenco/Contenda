# Playtest de balanceamento — ticket 37

## Configuração da rodada

- Build/commit:
- Data:
- Resolução e limite de quadros:
- CPU/GPU/RAM:
- Controles e configurações fora do padrão:
- Jogador(es):

Faça dez partidas completas com cada personagem usando os mesmos dados do jogo.
Não mude os recursos durante uma série; se houver uma mudança, registre outro
commit e reinicie os dez registros daquele personagem.

## Valores de partida

| Personagem | Base | Arma/form | Transformação |
|---|---|---|---|
| Swordsman | 140 HP; 100 mana; regen 5/s; crítico 10% ×2 | espada: 20/22/32 dano | Berserker: custo 20, dreno 4/s, dano ×1,6, crítico +35%, pulo extra |
| Gunslinger | 100 HP; 120 mana; regen 7/s; crítico 5% ×1,8 | revólver: 18 dano, 0,30 s, 6 tiros, recarga 1,6 s | Overdrive: custo 25, dreno 5/s; canhão: 26 dano, 0,50 s, explosão de 2,5 m |

Sequências de habilidade: Swordsman — 1 Dash Slash (`WW`), 2 Rising Slash
(`SW`), 3 Spin Slash (`AD`), 4 Heavy Lunge (`WAW`); Gunslinger — 1 Quick Step
Shot (`WW`), 2 Explosive Shot (`SW`), 3 Fan The Hammer (`AD`), 4 Deadeye
(`WAW`). Os dados completos estão em `data/characters/`, `data/abilities/`,
`data/weapons/` e `data/transformations/`.

## Swordsman — dez partidas

Em “Curva”, anote conforto da onda 1, tensão da onda 3, quase-morte na onda 4
e se a onda 5 exigiu forma. Em “Mobilidade”, marque se pulo, dash e ataques
aéreos foram intencionais.

| Jogo | Resultado/onda | Duração | Curva W1/W3/W4/W5 | Habilidades usadas (1–4) | Forma (onda/tempo) | Crítico (fraco/ok/dominante) | Mobilidade intencional | Observações |
|---|---|---|---|---|---|---|---|---|
| S1 | | | | | | | | |
| S2 | | | | | | | | |
| S3 | | | | | | | | |
| S4 | | | | | | | | |
| S5 | | | | | | | | |
| S6 | | | | | | | | |
| S7 | | | | | | | | |
| S8 | | | | | | | | |
| S9 | | | | | | | | |
| S10 | | | | | | | | |

## Gunslinger — dez partidas

| Jogo | Resultado/onda | Duração | Curva W1/W3/W4/W5 | Habilidades usadas (1–4) | Forma (onda/tempo) | Crítico (fraco/ok/dominante) | Mobilidade intencional | Observações |
|---|---|---|---|---|---|---|---|---|
| G1 | | | | | | | | |
| G2 | | | | | | | | |
| G3 | | | | | | | | |
| G4 | | | | | | | | |
| G5 | | | | | | | | |
| G6 | | | | | | | | |
| G7 | | | | | | | | |
| G8 | | | | | | | | |
| G9 | | | | | | | | |
| G10 | | | | | | | | |

## Decisões após as partidas

| Dado alterado (arquivo/campo) | Antes | Depois | Evidência nos registros | Resultado da repetição |
|---|---|---|---|---|
| | | | | |

Não atualize as tabelas das specs 04, 08, 09 e 10 até fechar os 20 registros e
decidir os valores finais. Faça as alterações nos `.tres` e nas tabelas de
specs no mesmo commit.
