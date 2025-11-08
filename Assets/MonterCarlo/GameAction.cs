// GameAction.cs
using System;

[Serializable]
public enum GameActionType { MoveUp, MoveDown, MoveLeft, MoveRight, Wait, Attack }

[Serializable]
public class GameAction : IAction
{
    public GameActionType Type;
    public int TargetAgentId = -1;

    public string Name => ToString();

    public GameAction(GameActionType type, int targetAgentId = -1)
    {
        Type = type;
        TargetAgentId = targetAgentId;
    }

    public override string ToString()
    {
        if (Type == GameActionType.Attack) return $"Attack->{TargetAgentId}";
        return Type.ToString();
    }
}
