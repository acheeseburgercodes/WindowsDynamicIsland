namespace DynamicIsland.Core;

public sealed class IslandManager
{
    public IslandState State { get; private set; } = IslandState.Collapsed;

    public event EventHandler<IslandState>? StateChanged;

    public void TransitionTo(IslandState next)
    {
        if (State == next)
        {
            return;
        }

        State = next;
        StateChanged?.Invoke(this, next);
    }

    public void Toggle() => TransitionTo(State == IslandState.Collapsed
        ? IslandState.Expanded
        : IslandState.Collapsed);
}
