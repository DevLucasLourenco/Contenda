# Avisos de terceiros

Todo asset ou biblioteca de terceiro usado no Contenda é registrado aqui, com
autor, origem, licença e data de obtenção.

**Regra do projeto:** só entra material cuja licença permita uso comercial sem
restrição viral. `CC0`, `MIT` e `Apache-2.0` são aceitos; **`CC BY-NC` e
`CC BY-NC-SA` são proibidos**. Ver
[docs/specs/13-assets-animacao-e-licencas.md](docs/specs/13-assets-animacao-e-licencas.md).

Cada pasta de asset também carrega um `SOURCE.md` próprio. Um asset sem
`SOURCE.md` reprova o pull request.

---

## Motor e bibliotecas

### Godot Engine

Godot Foundation e colaboradores · https://godotengine.org · **MIT**
Uso: motor do jogo (edição .NET, 4.7.x). Distribuído junto com a build.

### Godot.NET.Sdk

Godot Foundation · https://www.nuget.org/packages/Godot.NET.Sdk · **MIT**
Uso: SDK de build do projeto C#.

---

## Assets incorporados

| Fonte | Autor | Licença | Obtido em | Uso |
|---|---|---|---|---|
| [KayKit Adventurers Character Pack 2.0](https://kaylousberg.itch.io/kaykit-adventurers) | Kay Lousberg | CC0 1.0 | 2026-10-02 | Knight do Swordsman e Rogue da Gunslinger |
| [KayKit Character Animations 1.1](https://kaylousberg.itch.io/kaykit-character-animations) | Kay Lousberg | CC0 1.0 | 2026-10-02 | Animações dos dois personagens jogáveis |

Os arquivos originais e as licenças também estão descritos nos `SOURCE.md` de
cada pasta. As armas atuais são cenas de primitivas do Godot e não incluem
modelos de terceiros.

## Fontes planejadas, ainda não incorporadas

| Fonte | Licença prevista | Uso planejado |
|---|---|---|
| Quaternius — Ultimate Animated Character Pack | CC0 | possíveis modelos de inimigos |
| Quaternius — Animated Guns Pack | CC0 | possível substituição do revólver procedural |
| Mixamo (Adobe) | uso permitido em projetos pessoais e comerciais | alternativa futura de animações |

Essas fontes não estão representadas por assets no repositório.

---

## Referências estudadas, não incorporadas

Projetos cujo raciocínio arquitetural informou o desenho, mas dos quais **nenhum
código ou arte foi copiado**. Listados por transparência, não por obrigação
legal.

- Isometric 3D Toolkit for Godot C# — CC BY 4.0 — matemática de câmera
- Godot Top-Down Template — composição por componentes
- GDAbilitySystem — MIT — filosofia Ability/Attribute/Effect
- Godot4 3D Characters — MIT — fluxo de estados de IA
- RoboBlast / GDQuest — código MIT; **a arte é CC BY-NC-SA e foi deliberadamente
  descartada**
