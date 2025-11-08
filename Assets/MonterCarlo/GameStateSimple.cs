// GameStateSimple.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AgentData
{
    public int id;
    public int x;
    public int y;
    public int health;
    public bool isEnemy;
}

[Serializable]
public class GameStateSimple : IGameState
{
    public int width;
    public int height;
    public List<AgentData> agents = new List<AgentData>();
    public int currentPlayerAgentId;

    public IGameState Clone()
    {
        var clone = new GameStateSimple
        {
            width = this.width,
            height = this.height,
            currentPlayerAgentId = this.currentPlayerAgentId,
            agents = new List<AgentData>(this.agents.Count)
        };
        foreach (var a in this.agents)
        {
            clone.agents.Add(new AgentData { id = a.id, x = a.x, y = a.y, health = a.health, isEnemy = a.isEnemy });
        }
        return clone;
    }

    public List<IAction> GetAvailableActions()
    {
        var list = new List<IAction>();
        var me = agents.Find(a => a.id == currentPlayerAgentId && a.health > 0);
        if (me == null) return list;

        if (IsFree(me.x, me.y - 1)) list.Add(new GameAction(GameActionType.MoveUp));
        if (IsFree(me.x, me.y + 1)) list.Add(new GameAction(GameActionType.MoveDown));
        if (IsFree(me.x - 1, me.y)) list.Add(new GameAction(GameActionType.MoveLeft));
        if (IsFree(me.x + 1, me.y)) list.Add(new GameAction(GameActionType.MoveRight));
        list.Add(new GameAction(GameActionType.Wait));

        foreach (var other in agents)
        {
            if (other.isEnemy == me.isEnemy) continue;
            if (other.health <= 0) continue;
            if (Mathf.Abs(other.x - me.x) + Mathf.Abs(other.y - me.y) == 1)
            {
                list.Add(new GameAction(GameActionType.Attack, other.id));
            }
        }
        return list;
    }

    private bool IsFree(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return false;
        foreach (var a in agents)
            if (a.x == x && a.y == y && a.health > 0) return false;
        return true;
    }

    public void ApplyAction(IAction a)
    {
        if (!(a is GameAction ga)) throw new Exception("Unknown action type");
        var me = agents.Find(x => x.id == currentPlayerAgentId && x.health > 0);
        if (me == null) return;

        switch (ga.Type)
        {
            case GameActionType.MoveUp: me.y -= 1; break;
            case GameActionType.MoveDown: me.y += 1; break;
            case GameActionType.MoveLeft: me.x -= 1; break;
            case GameActionType.MoveRight: me.x += 1; break;
            case GameActionType.Wait: break;
            case GameActionType.Attack:
                var target = agents.Find(x => x.id == ga.TargetAgentId && x.health > 0);
                if (target != null) target.health -= 1;
                break;
        }

        // Advance turn: find next alive agent in list order
        int currentIndex = agents.FindIndex(a => a.id == currentPlayerAgentId);
        if (currentIndex == -1) return;
        for (int offset = 1; offset <= agents.Count; offset++)
        {
            var candidate = agents[(currentIndex + offset) % agents.Count];
            if (candidate.health > 0)
            {
                currentPlayerAgentId = candidate.id;
                break;
            }
        }
    }

    public bool IsTerminal()
    {
        bool anyAllies = agents.Exists(a => a.health > 0 && !a.isEnemy);
        bool anyEnemies = agents.Exists(a => a.health > 0 && a.isEnemy);
        return !anyAllies || !anyEnemies;
    }

    public float GetResult(int playerId)
    {
        var player = agents.Find(a => a.id == playerId);
        if (player == null) return 0.5f;
        bool alliesAlive = agents.Exists(a => a.health > 0 && !a.isEnemy);
        bool enemiesAlive = agents.Exists(a => a.health > 0 && a.isEnemy);

        bool playerIsEnemy = player.isEnemy;
        if (playerIsEnemy)
        {
            if (enemiesAlive && !alliesAlive) return 1f;
            if (!enemiesAlive && alliesAlive) return 0f;
        }
        else
        {
            if (alliesAlive && !enemiesAlive) return 1f;
            if (!alliesAlive && enemiesAlive) return 0f;
        }
        return 0.5f;
    }

    public string DebugString()
    {
        return $"Agents: {agents.Count}, Current: {currentPlayerAgentId}";
    }
}
