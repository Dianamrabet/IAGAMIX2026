// MCTSIntegrationTutorial.cs
using UnityEngine;

public class MCTSIntegrationTutorial : MonoBehaviour
{
    public SnapshotBuilder snapshotBuilder;
    public MCTSAgent mctsAgent;

    void Update()
    {
        // Exemple : à chaque X secondes on demande la décision
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var snapshot = snapshotBuilder.BuildSnapshot();
            mctsAgent.SetRootState(snapshot);
            // le MCTSAgent (dans le script que tu as importé) lancera sa recherche automatique
        }
    }
}
