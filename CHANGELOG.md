# Contenda — histórico de versões

## 0.1.0 — primeira versão

Primeira versão jogável do MVP: modo Horde numa arena urbana, Swordsman e
Gunslinger, ataques básicos, habilidades por sequência, transformações,
progressão por ondas, chefe, HUD, menus, pausa e configurações persistentes.

### Limitações conhecidas

- Não há versão para navegador. O projeto usa Godot .NET/C#, sem exportação Web.
- A meta de 60 fps/p99 com 40 inimigos não foi atingida na medição local da
  GeForce MX110; o desempenho no hardware de referência não foi validado. A
  otimização adicional foi adiada por decisão de escopo.
- A sessão contínua de 30 minutos e os testes manuais de troca de janela,
  resolução, monitor e arquivos de configuração corrompidos continuam pendentes.
- A build macOS não está assinada nem notarizada; o Gatekeeper pode exigir
  confirmação para abrir o aplicativo.
