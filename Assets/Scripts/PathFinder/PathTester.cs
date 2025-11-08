using UnityEngine;

public class PathTester : MonoBehaviour
{
    public Transform startPoint;
    public Transform targetPoint;
    private AStarPathfinding pathfinder;

    void Start()
    {
        pathfinder = FindFirstObjectByType<AStarPathfinding>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            pathfinder.FindPath(startPoint.position, targetPoint.position);
        }
    }
}
