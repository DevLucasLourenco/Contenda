# 39: As builds rodam em máquina limpa — versão 0.1

**What to build:** o jogo sai do repositório e vira um arquivo que outra pessoa
baixa, descompacta e joga, sem instalar nada.

**Blocked by:** 38

**Status:** ready-for-agent

- [ ] Builds para Windows, Linux e macOS
- [ ] **Cada uma testada em máquina sem ferramenta de desenvolvimento instalada**
- [ ] O pacote não leva documentação, testes nem ferramentas internas
- [ ] O pacote leva a licença e os avisos de terceiros
- [ ] A versão é a mesma no jogo, no projeto e na marcação do repositório
- [ ] Nenhum registro de depuração e nenhum atalho de debug ativo na build final
- [ ] As configurações persistem nos três sistemas, cada um no seu lugar próprio
- [ ] O CI gera as builds a partir da marcação de versão
- [ ] As notas de versão declaram o escopo e as limitações, **incluindo a
      ausência de versão para navegador**

## Comments

Declarar a ausência de versão web nas notas evita a pergunta que virá de todo
mundo. É consequência de rodar C# no Godot, decidida no início do projeto, e não
esquecimento.

Testar em máquina limpa é o único critério que pega dependência esquecida. Numa
máquina de desenvolvimento tudo funciona por acidente.
