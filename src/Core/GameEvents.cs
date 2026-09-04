using Godot;

namespace Contenda.Core;

/// <summary>
/// Barramento para eventos entre sistemas sem relação direta — o abate de um
/// inimigo chegando ao placar, por exemplo.
/// </summary>
/// <remarks>
/// Regra do projeto: este barramento transporta **apenas dados por valor**
/// (<c>readonly record struct</c>), nunca referências a nós. Um evento que
/// carrega um nó mantém vivo algo que o pool achou que tinha reciclado.
///
/// Comunicação dentro do mesmo personagem NÃO passa por aqui: usa chamada direta
/// pelo <c>CharacterContext</c> ou <c>event</c> do próprio componente.
/// Ver docs/specs/01-arquitetura-tecnica.md §5.
///
/// Os eventos concretos entram conforme os sistemas que os disparam — o de
/// abate chega no M6, com o modo horda. Adicioná-los antes seria abstração para
/// necessidade que a spec ainda não tem.
/// </remarks>
public sealed partial class GameEvents : Node
{
    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] GameEvents pronto");
    }
}
