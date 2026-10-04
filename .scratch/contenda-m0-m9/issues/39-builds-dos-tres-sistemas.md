# 39: As builds rodam em máquina limpa — versão 0.1

**What to build:** o jogo sai do repositório e vira um arquivo que outra pessoa
baixa, descompacta e joga, sem instalar nada.

**Blocked by:** None — ticket 38 encerrado por decisão de escopo do usuário em 2026-10-04, com as limitações de desempenho aceitas e documentadas

**Status:** in progress — presets e pipelines de CI/release implementados; os pacotes finais dependem da decisão sobre `LICENSE`, da instalação dos templates de export e de validação nas três plataformas

- [ ] Builds para Windows, Linux e macOS
- [ ] **Cada uma testada em máquina sem ferramenta de desenvolvimento instalada**
- [x] O pacote não leva documentação, testes nem ferramentas internas (filtros dos presets e auditoria no empacotador)
- [ ] O pacote leva a licença e os avisos de terceiros (`LICENSE` depende de decisão do usuário; `THIRD-PARTY-NOTICES.md` e o copyright do Godot entram no pacote)
- [x] A versão é a mesma no jogo, no projeto e na marcação do repositório (o CI compara a tag `vX.Y.Z` com `project.godot` e `Contenda.csproj`; o assembly imprime a versão)
- [x] Nenhum registro de depuração e nenhum atalho de debug ativo na build final (mensagens `[boot]` e benchmark só em debug; presets de export release)
- [ ] As configurações persistem nos três sistemas, cada um no seu lugar próprio
- [x] O CI gera as builds a partir da marcação de versão (workflow `release.yml` dispara em tag `v*` e envia os três ZIPs como artifact)
- [x] As notas de versão declaram o escopo e as limitações, **incluindo a
      ausência de versão para navegador**

## Comments

O projeto já mantém a mesma versão `0.1.0` em `project.godot` e
`Contenda.csproj`; o pipeline deve recusar uma tag que divirja dessas fontes.
O ticket 02 deixou a licença do código pendente de decisão do usuário. Não
escolher licença automaticamente: até a decisão, a etapa de empacotamento deve
falhar claramente em vez de produzir um pacote incompleto. Os pacotes também
incluem `GODOT_COPYRIGHT.txt` da versão exata do engine, com as licenças de suas
bibliotecas de terceiros.

O usuário encerrou o ticket 38 em 2026-10-04 para avançar ao release; seus
resultados abaixo do orçamento permanecem como limitação conhecida da versão.
As builds e smoke tests automatizados não substituem o critério de rodar os
pacotes numa máquina limpa de cada sistema operacional.

Validação local em 2026-10-04: Godot 4.7.2 importou o projeto; as builds
`Debug` e `ExportRelease` compilaram sem avisos; os 442 testes unitários passaram.
O empacotador passou com exports sintéticos, mas os arquivos de release reais
continuam bloqueados pela licença ainda não escolhida e pelos templates Godot
ausentes nesta máquina. Nenhuma validação em Windows, Linux ou macOS limpos foi
realizada.

Declarar a ausência de versão web nas notas evita a pergunta que virá de todo
mundo. É consequência de rodar C# no Godot, decidida no início do projeto, e não
esquecimento.

Testar em máquina limpa é o único critério que pega dependência esquecida. Numa
máquina de desenvolvimento tudo funciona por acidente.
