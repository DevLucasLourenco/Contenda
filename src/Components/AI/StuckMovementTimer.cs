using System;

namespace Contenda.Components.AI;

/// <summary>Accumula quanto tempo um inimigo pede movimento sem de fato avançar.</summary>
public sealed class StuckMovementTimer
{
    private readonly float _minimumProgressSpeed;
    private bool _removalMarked;

    public StuckMovementTimer(float minimumProgressSpeed)
    {
        if (minimumProgressSpeed < 0f)
            throw new ArgumentOutOfRangeException(nameof(minimumProgressSpeed));

        _minimumProgressSpeed = minimumProgressSpeed;
    }

    public float ElapsedSeconds { get; private set; }

    public bool TryMarkForRemoval(float timeoutSeconds)
    {
        if (_removalMarked || ElapsedSeconds < timeoutSeconds)
            return false;

        _removalMarked = true;
        return true;
    }

    public void Advance(float delta, bool isChasing, bool hasMoveIntent, float horizontalSpeed)
    {
        if (delta <= 0f)
            return;

        if (!isChasing || !hasMoveIntent || horizontalSpeed >= _minimumProgressSpeed)
        {
            Reset();
            return;
        }

        ElapsedSeconds += delta;
    }

    public void Reset()
    {
        ElapsedSeconds = 0f;
        _removalMarked = false;
    }
}
