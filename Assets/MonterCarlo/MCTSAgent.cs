// MCTSAgent.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Représente un nœud de l'arbre MCTS
/// </summary>
public class MCTSNode
{
    public IGameState state;
    public IAction actionFromParent;
    public MCTSNode parent;
    public List<MCTSNode> children = new List<MCTSNode>();

    public int visits = 0;
    public float totalValue = 0f;

    public bool IsFullyExpanded => children.Count == state.GetAvailableActions().Count;
}

/// <summary>
/// Agent qui exécute un algorithme Monte Carlo Tree Search
/// </summary>
public class MCTSAgent : MonoBehaviour
{
    [Header("Paramètres MCTS")]
    public int iterationsPerDecision = 200;   // nombre d'itérations par décision
    public float uctC = 1.414f;               // constante UCT (√2 par défaut)
    public int maxRolloutDepth = 10;          // profondeur max des simulations
    public int playerId = 0;                  // identifiant du joueur (0 ou 1)

    private MCTSNode root;

    /// <summary>
    /// Définit l'état initial de la recherche (snapshot)
    /// </summary>
    public void SetRootState(IGameState rootState)
    {
        root = new MCTSNode
        {
            state = rootState.Clone(),
            parent = null,
            actionFromParent = null
        };
    }

    /// <summary>
    /// Choisit la meilleure action à partir de l'état courant via MCTS
    /// </summary>
    public IAction ChooseBestAction()
    {
        if (root == null || root.state == null)
        {
            Debug.LogError("[MCTSAgent] Aucun état racine défini !");
            return null;
        }

        for (int i = 0; i < iterationsPerDecision; i++)
        {
            MCTSNode node = Select(root);
            if (node == null) continue;

            MCTSNode expanded = Expand(node);
            float reward = Simulate(expanded.state);
            Backpropagate(expanded, reward);
        }

        // Choisir le fils le plus visité
        MCTSNode bestChild = null;
        float bestVisits = -1;
        foreach (var child in root.children)
        {
            if (child.visits > bestVisits)
            {
                bestVisits = child.visits;
                bestChild = child;
            }
        }

        if (bestChild == null)
        {
            Debug.LogWarning("[MCTSAgent] Aucun enfant trouvé après recherche !");
            return null;
        }

        Debug.Log($"[MCTSAgent] Action choisie : {bestChild.actionFromParent.Name}");
        return bestChild.actionFromParent;
    }

    // Sélection : descend dans l’arbre selon la formule UCT
    private MCTSNode Select(MCTSNode node)
    {
        while (!node.state.IsTerminal() && node.IsFullyExpanded)
        {
            node = BestUCTChild(node);
            if (node == null) return null;
        }
        return node;
    }

    private MCTSNode BestUCTChild(MCTSNode node)
    {
        MCTSNode best = null;
        float bestValue = float.MinValue;

        foreach (var child in node.children)
        {
            float avgValue = child.totalValue / (child.visits + 1e-6f);
            float uctValue = avgValue + uctC * Mathf.Sqrt(Mathf.Log(node.visits + 1) / (child.visits + 1e-6f));
            if (uctValue > bestValue)
            {
                bestValue = uctValue;
                best = child;
            }
        }
        return best;
    }

    // Expansion : créer un nouveau nœud enfant
    private MCTSNode Expand(MCTSNode node)
    {
        var actions = node.state.GetAvailableActions();
        foreach (var action in actions)
        {
            bool alreadyExpanded = node.children.Exists(c => Equals(c.actionFromParent, action));
            if (!alreadyExpanded)
            {
                var nextState = node.state.Clone();
                nextState.ApplyAction(action);
                var newNode = new MCTSNode
                {
                    state = nextState,
                    parent = node,
                    actionFromParent = action
                };
                node.children.Add(newNode);
                return newNode;
            }
        }
        return node;
    }

    // Simulation : exécution aléatoire de coups jusqu’à la fin ou limite
    private float Simulate(IGameState simState)
    {
        IGameState state = simState.Clone();
        int depth = 0;
        while (!state.IsTerminal() && depth < maxRolloutDepth)
        {
            var actions = state.GetAvailableActions();
            if (actions.Count == 0) break;
            var randomAction = actions[UnityEngine.Random.Range(0, actions.Count)];
            state.ApplyAction(randomAction);
            depth++;
        }
        return state.GetResult(playerId);
    }

    // Rétropropagation du résultat
    private void Backpropagate(MCTSNode node, float reward)
    {
        while (node != null)
        {
            node.visits++;
            node.totalValue += reward;
            node = node.parent;
        }
    }
}
