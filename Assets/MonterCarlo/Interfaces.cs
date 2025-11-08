// Interfaces.cs
using System.Collections.Generic;

public interface IAction
{
    // Friendly debug name for the action
    string Name { get; }
}

public interface IGameState
{
    // Return a deep clone used for simulation (must not affect real game state)
    IGameState Clone();

    // Return available legal actions in this state
    List<IAction> GetAvailableActions();

    // Apply an action to this game state (modify in-place)
    void ApplyAction(IAction a);

    // Is the state terminal (game over for the simulated game)?
    bool IsTerminal();

    // Return result from the perspective of the player running MCTS (1 = win, 0 = loss, 0.5 draw)
    float GetResult(int playerId);

    // (Optional) debugging string
    string DebugString();
}
