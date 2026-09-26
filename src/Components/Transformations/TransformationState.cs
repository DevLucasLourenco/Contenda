namespace Contenda.Components.Transformations;

/// <summary>Seleção e relógio da transformação, sem dependência da engine.</summary>
public sealed class TransformationState
{
    private readonly int _slotCount;
    private float _activeDuration;

    public TransformationState(int transformationCount) => _slotCount = transformationCount + 1;

    /// <summary>Índice selecionado; zero representa Normal.</summary>
    public int SelectedIndex { get; private set; }

    /// <summary>Índice ativo; zero representa nenhuma transformação.</summary>
    public int ActiveIndex { get; private set; }

    public float ActiveDuration => _activeDuration;
    public bool IsActive => ActiveIndex != 0;

    public int SelectNext() => SelectedIndex = (SelectedIndex + 1) % _slotCount;

    public int SelectPrevious() => SelectedIndex = (SelectedIndex - 1 + _slotCount) % _slotCount;

    public bool TryActivate(float mana, float cost)
    {
        if (IsActive || SelectedIndex == 0 || mana < cost)
            return false;

        ActiveIndex = SelectedIndex;
        _activeDuration = 0f;
        return true;
    }

    public bool CanRevert(float minimumDuration) => _activeDuration >= minimumDuration;

    public void Advance(float delta)
    {
        if (IsActive)
            _activeDuration += delta;
    }

    public void Revert()
    {
        ActiveIndex = 0;
        _activeDuration = 0f;
    }

    public void Reset()
    {
        SelectedIndex = 0;
        Revert();
    }
}
