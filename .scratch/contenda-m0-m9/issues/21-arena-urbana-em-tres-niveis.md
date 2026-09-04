# 21: A arena vira um cruzamento de cidade em três níveis

**What to build:** o espaço deixa de ser um campo com caixas e vira um
cruzamento urbano com praça rebaixada, calçadas, ruas, objetos escaláveis e um
telhado. A altura passa a ser jogável, e não cenário.

**Blocked by:** 17, 04

**Status:** ready-for-agent

- [ ] Praça rebaixada ao centro, com rampas nos quatro lados — **rampas, não
      degraus**, para que inimigo não trave em quina
- [ ] Ruas em cruz, calçadas com desnível pequeno, atravessável andando
- [ ] Objetos escaláveis por pulo simples: caçamba, ônibus, contêiner
- [ ] Um andaime alcançável a partir deles
- [ ] Um telhado alcançável **só** com pulo duplo ou pela rota longa — é o que dá
      motivo para se transformar
- [ ] Prédios delimitam a área e são intransponíveis; nada de parede invisível solta
- [ ] **Nenhuma geometria acima de 4 m dentro da área jogável**
- [ ] Nada alto no quadrante de onde a câmera olha
- [ ] Objeto que fique entre a câmera e o jogador **desaparece por transparência**;
      a câmera não se move para desviar
- [ ] Inimigos sobem nos objetos escaláveis sem travar
- [ ] O telhado fica fora da navegação inimiga, mas continua sob fogo dos
      atiradores — não é posição invencível
- [ ] Os três níveis são distinguíveis a olho, sem interface

## Comments

Este ticket é o que justifica o pulo. Sem verticalidade real, Espaço vira um
botão que faz o personagem subir vinte centímetros e descer.

A regra de nada acima de 4 m é o que concilia "cidade" com "câmera fixa
travada". Uma cidade fotorrealista com prédios altos ao redor tornaria o
enquadramento impossível — os prédios ficam **fora** da área jogável, como
limite e pano de fundo.

Bloqueio com primitivas agora; assets modulares só no milestone de arte. Se o
bloqueio não for divertido de percorrer, arte não conserta.
