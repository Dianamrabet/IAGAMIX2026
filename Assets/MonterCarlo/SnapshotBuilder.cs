// SnapshotBuilder.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crée un état de jeu (snapshot) basé sur la scène actuelle.
/// À adapter à ton système d'agents (positions, points de vie, rôles, etc.)
/// </summary>
public class SnapshotBuilder : MonoBehaviour
{
    [Header("Taille de la grille (approximative)")]
    public int gridWidth = 20;
    public int gridHeight = 20;

    [Header("Agents à inclure dans le snapshot")]
    public List<GameObject> agentGameObjects;

    public GameStateSimple BuildSnapshot()
    {
        var state = new GameStateSimple
        {
            width = gridWidth,
            height = gridHeight,
            agents = new List<AgentData>()
        };

        int nextId = 1;
        foreach (var go in agentGameObjects)
        {
            var ad = new AgentData();
            ad.id = nextId++;

            // Position (convertie en grille)
            ad.x = Mathf.RoundToInt(go.transform.position.x);
            ad.y = Mathf.RoundToInt(go.transform.position.z);

            // Points de vie simulés (si pas de composant spécifique)
            ad.health = 1;

            // Distinction allié / ennemi par tag
            ad.isEnemy = go.CompareTag("Enemy");

            state.agents.Add(ad);
        }

        // Définir le joueur courant (le premier allié)
        var first = state.agents.Find(a => !a.isEnemy && a.health > 0);
        if (first != null) state.currentPlayerAgentId = first.id;
        else if (state.agents.Count > 0) state.currentPlayerAgentId = state.agents[0].id;

        return state;
    }
}
