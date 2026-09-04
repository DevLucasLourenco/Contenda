# 17 — Arena urbana

Substitui a arena de bloqueio genérica da [spec 02 §9](02-camera-e-mundo-25d.md)
por um **cruzamento de cidade**. Não é troca de decoração: a cidade existe para
dar função ao pulo, ao dash e ao combate aéreo da
[spec 16](16-mobilidade-criticos-e-combate-aereo.md).

## 1. O problema que a arena resolve

Uma arena plana com caixas espalhadas não pede pulo nem dash — pede só andar em
volta. Com câmera fixa e ângulo travado, o que torna um espaço interessante é
**altura legível** e **linhas de visão que se abrem e fecham**. Uma cidade dá as
duas coisas de graça, com vocabulário que o jogador já entende: rua, calçada,
praça rebaixada, telhado.

## 2. Planta

```
   ╔═══════════════════════════════════════════════════════╗
   ║  PRÉDIO          ┊  telhado +3.0  ┊         PRÉDIO     ║
   ║  (limite)     ╔══╧════════════════╧══╗   (limite)      ║
   ║               ║   andaime  +1.5     ║                  ║
   ║   ┌───────────╨─────────────────────╨───────────┐      ║
   ║   │              R U A   N O R T E              │      ║
   ║   │   [ônibus]                    [contêiner]   │      ║
   ║   ├──────────┐   ╭───────────────╮   ┌──────────┤      ║
   ║   │ calçada  │   │               │   │  calçada │      ║
   ║   │  +0.2    │   │    PRAÇA      │   │   +0.2   │      ║
   ║ R │          │   │    -1.0       │   │          │ R    ║
   ║ U │  [banca] │   │   (fonte)     │   │  [carro] │ U    ║
   ║ A │          │   │               │   │          │ A    ║
   ║   ├──────────┘   ╰───────────────╯   └──────────┤      ║
   ║ O │              R U A   S U L                  │ L    ║
   ║ E │   [barricada]              [caçamba +1.2]   │ E    ║
   ║ S └─────────────────────────────────────────────┘ S    ║
   ║ T        ┊  viela estreita  ┊                     T    ║
   ╚═══════════════════════════════════════════════════════╝
              ▲ spawn          ▲ spawn         ▲ spawn
```

Área jogável ~**70 × 70 m**. Prédios são o limite intransponível — não são
cenário decorativo com colisão duvidosa.

## 3. Os três níveis

A verticalidade é o ponto. Três patamares, todos alcançáveis:

| Nível | Altura | Como se chega | Papel |
|---|---|---|---|
| **Praça** | −1.0 m | escadas nos quatro lados, ou pulo para baixo | funil: bom para área, ruim para fugir |
| **Rua** | 0.0 m | padrão | circulação principal |
| **Calçada** | +0.2 m | degrau, atravessável andando | quebra a leitura plana sem virar obstáculo |
| **Caçamba / ônibus** | +1.2 a +1.5 m | **pulo** | ponto de reposicionamento e de mergulho |
| **Andaime** | +1.5 m | pulo em dois tempos, ou dash aéreo | rota alternativa sobre a rua norte |
| **Telhado baixo** | +3.0 m | andaime → pulo (duplo, no Berserker) | posição de sniper da Gunslinger e origem de estocadas de queda |

**Regra de ouro:** todo ponto elevado é alcançável **sem** pulo duplo, exceto o
telhado, que exige o Berserker ou uma rota mais longa pelo andaime. Um lugar que
só uma forma alcança dá razão para transformar.

## 4. Restrição da câmera — a que a cidade ameaça

A [spec 02](02-camera-e-mundo-25d.md) dizia que a arena não tem teto nem paredes
altas entre câmera e jogador. Uma cidade viola isso por natureza. As regras que
mantêm o enquadramento honesto, com pitch −55° e yaw 45°:

1. **Nada acima de 4 m dentro da área jogável.** Prédios ficam **fora** dela,
   funcionando como limite e pano de fundo.
2. **O quadrante da câmera é livre.** Como o yaw é fixo em 45°, a câmera está
   sempre a nordeste do jogador — geometria alta só é permitida nos outros três
   quadrantes.
3. **Fade dithered obrigatório** em qualquer objeto entre câmera e jogador
   (andaime, marquise). A câmera **não se move** para desviar; o objeto é que
   desaparece. Isso preserva o pilar de que o enquadramento nunca muda.
4. **Vielas com no máximo 3 m de altura de parede**, senão o jogador some dentro
   delas.

## 5. Navegação

- Navmesh cobrindo praça, rua, calçadas e o topo dos objetos escaláveis.
- **Ligações de navegação** (`NavigationLink3D`) para saltos que os inimigos
  podem fazer: rua → caçamba, caçamba → andaime.
- Telhado **fora** da navmesh: é um refúgio real do jogador, alcançável só por
  ele. Inimigos à distância (`shooter`) continuam sendo ameaça lá em cima, então
  não vira posição invencível.
- Escadas da praça são rampas de verdade (≤ 45°), não degraus — inimigo travado
  em quina é a fonte número um de onda que não termina.

## 6. Composição por onda

A cidade permite o que a arena vazia não permitia: **o espaço muda de função
conforme a onda**.

| Onda | Uso do espaço |
|---|---|
| 1–2 | rua norte, aberta e legível — ensina o básico |
| 3 | spawns nas duas ruas laterais: o jogador é pego em pinça |
| 4 | `brute` na praça, que é funil — recompensa dano em área e o Overdrive |
| 5 | boss na praça, reforços descendo das vielas; o telhado vira o único respiro |

## 7. Orçamento e construção

| Item | Alvo |
|---|---|
| Blocos modulares distintos | ≤ 25 |
| Triângulos totais do cenário | ≤ 180 k |
| Draw calls com a arena cheia | ≤ 900 |
| Materiais únicos | ≤ 12 |

Construção em duas etapas: **bloqueio com primitivas** no M1 (formas e alturas
corretas, sem arte), e substituição por assets modulares CC0 no M8. O bloqueio já
tem que ser divertido de percorrer — se não for, arte não conserta.

Fontes previstas (CC0), a confirmar no M8: Kenney *City Kit* e *Modular
Buildings*, Quaternius *Ultimate Modular Ruins*. Sujeitas à mesma regra de
licença da [spec 13](13-assets-animacao-e-licencas.md).

## 8. Critérios de aceite

- [ ] Os três níveis são distinguíveis a olho sob a câmera fixa, sem HUD
- [ ] Pular da rua para a caçamba e daí para o andaime funciona sem frustração
- [ ] O telhado só é alcançável com pulo duplo ou pela rota longa
- [ ] Nenhuma geometria acima de 4 m dentro da área jogável
- [ ] Nada no quadrante nordeste bloqueia a visão do jogador
- [ ] Objetos entre câmera e jogador desaparecem por fade; a câmera nunca se move
- [ ] Inimigos sobem na caçamba pelas ligações de navegação, sem travar em quina
- [ ] A praça funciona como funil: lutar nela é mais perigoso e mais lucrativo
- [ ] 40 inimigos na cidade mantêm ≥ 60 fps
