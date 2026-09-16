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
///
/// <see cref="ResetForSpawn"/> existe pelo mesmo motivo: o pool de inimigos
/// (ticket 25, spec 09 §8) nunca destrói um nó, só desativa e reaproveita —
/// `_ExitTree` não roda entre uma vida e outra. Sem um método obrigatório
/// aqui, "todo componente reseta o próprio estado transiente" viraria
/// convenção de memória em vez de contrato verificável pelo compilador, e é
/// exatamente esse tipo de estado esquecido que o teste de 100 ciclos do
/// ticket 25 existe para pegar. Quem nunca é reciclado (componentes só do
/// jogador, como <c>PlayerInputController</c>/<c>TargetingComponent</c>)
/// ainda implementa, vazio -- o contrato vale para todo mundo, não só quem
/// precisa dele hoje.
/// </remarks>
public interface ICharacterComponent
{
    /// <summary>Recebe o acesso aos componentes irmãos.</summary>
    void Bind(CharacterContext contexto);

    /// <summary>Aplica os dados do personagem. Chamado depois de todo <c>Bind</c>.</summary>
    void Configure(CharacterDefinition definicao);

    /// <summary>
    /// Devolve ao estado de recém-criado, para reaproveitar sem vazar nada de
    /// uma vida anterior. Chamado pelo <c>CharacterController</c> a pedido do
    /// pool, na reciclagem (ticket 25) -- nunca em resposta a <c>_ExitTree</c>,
    /// que não roda para um nó pooled.
    /// </summary>
    void ResetForSpawn();
}
