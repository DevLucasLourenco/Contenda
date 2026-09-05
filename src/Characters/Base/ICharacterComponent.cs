namespace Contenda.Characters.Base;

/// <summary>
/// Contrato de todo componente de personagem.
/// </summary>
/// <remarks>
/// As duas passadas existem porque componentes dependem uns dos outros e a ordem
/// de <c>_Ready</c> não é confiável para isso. O <see cref="CharacterController"/>
/// chama <c>Bind</c> em todos primeiro — quando toda a árvore já existe — e só
/// depois <c>Configure</c>, na ordem de dependência de dados.
///
/// Nenhum componente procura outro por caminho. Tudo vem do
/// <see cref="CharacterContext"/>. Ver docs/specs/01-arquitetura-tecnica.md §4.
/// </remarks>
public interface ICharacterComponent
{
    /// <summary>Recebe o acesso aos componentes irmãos.</summary>
    void Bind(CharacterContext contexto);

    /// <summary>Aplica os dados do personagem. Chamado depois de todo <c>Bind</c>.</summary>
    void Configure(CharacterDefinition definicao);
}
