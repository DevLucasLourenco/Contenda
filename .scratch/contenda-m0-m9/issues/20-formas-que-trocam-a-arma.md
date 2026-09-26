# 20: As duas transformações mudam o que o ataque básico faz

**What to build:** transformar-se deixa de ser só multiplicador. O espadachim
vira uma ameaça vertical — critica quase metade dos golpes e ganha um segundo
pulo. A pistoleira larga o revólver: o braço vira canhão e cada tiro explode em
área.

**Blocked by:** 18, 19, e a transformação básica funcionando

**Status:** concluído

- [x] Uma forma pode **substituir a arma equipada**, e não só a aparência
- [x] Berserker: chance de crítico salta para perto da metade dos golpes
- [x] Berserker: concede um pulo extra, e o telhado da arena passa a ser alcançável
- [x] Berserker: acrescenta um golpe à cadeia aérea e encurta a recuperação do mergulho
- [x] Overdrive: o revólver some e o braço direito vira canhão, visualmente (placeholder geométrico até os modelos do ticket 34)
- [x] Overdrive: cada acerto do tiro causa dano em área, com dano reduzido linearmente até 60% na borda
- [x] Overdrive: **acaba a munição e a recarga** — a arma é energia
- [x] Overdrive: a cadência é mais **lenta** que a do revólver, não mais rápida
- [x] Reverter devolve arma, atributos, pulos e aparência ao estado base, exatos
- [x] Morrer transformado não deixa o canhão preso no braço no respawn

## Implementação

O repositório não tinha ainda o pré-requisito descrito em `Blocked by`: a
transformação básica. Foi incluída a base necessária do M4: seleção com scroll,
ativação com M3, custo e dreno de mana, duração mínima, modificadores por fonte,
reversão ao zerar mana ou antes do evento de morte e seletor no HUD.

`TransformationDefinition` e as formas vivem em `.tres`. `CombatComponent`
mantém instâncias das armas base e de forma, para voltar ao revólver com seu
estado preservado. `arm_cannon.tres` dispara projéteis do pool existente, a
20 m/s, que explodem no contato com um alvo, no ponto mirado, na parede ou no
alcance máximo. O alvo direto recebe 100% do dano; a explosão cai linearmente
para 60% a 2,5 m. O placeholder geométrico do canhão aparece no lado direito do avatar; o modelo
articulado continua sendo trabalho do ticket 34.

`TransformationProbe` valida Berserker, Overdrive, a troca/reversão das armas,
os atributos e a reversão síncrona da forma antes do evento de morte.

## Comments

A troca de arma é a peça que faltava: até aqui uma forma podia trocar malha e
material, mas não **o que o botão de ataque faz**. Com ela, transformar-se muda o
verbo do personagem.

O bônus de cadência que o Overdrive tinha antes **sai de propósito**: somado a
dano em área, tornaria a forma dominante e o revólver base irrelevante. Se em
teste o Overdrive parecer fraco, aumente o dano ou o raio — não a cadência.

Revisão posterior (`dee615a`): a sonda agora verifica dano integral no alvo
direto, queda no alvo vizinho e explosão no ponto escolhido quando não há alvo.
Também foram reforçadas as referências dos visuais e a assinatura do seletor.
