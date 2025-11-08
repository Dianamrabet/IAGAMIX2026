using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize;
    public float nodeRadius = 0.5f;
    public float obstacleBuffer = 1.0f; // Distance around obstacles to avoid

    [Header("Dynamic Update Settings")]
    public bool enableDynamicUpdates = true;
    public float updateInterval = 0.5f; // How often to update the grid (seconds)
    [Tooltip("If true, updates only when obstacles move significantly (requires obstacles to have ObstacleMovement script)")]
    public bool smartUpdates = false;

    [Header("Debug Settings")]
    public bool displayGridGizmos = true;
    public bool displayPathLine = true;
    public Color walkableColor = Color.white;
    public Color nearObstacleColor = new Color(1f, 0.8f, 0.3f); // yellowish
    public Color unwalkableColor = Color.red;
    public Color pathColor = Color.black;
    public Color lineColor = Color.green;
    public float lineHeightOffset = 0.2f;

    [HideInInspector] public List<Node> path;

    Node[,] grid;
    float nodeDiameter;
    int gridSizeX, gridSizeY;
    private Coroutine updateCoroutine;
    private List<ObstacleMovement> trackedObstacles = new List<ObstacleMovement>();

    void Start()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();

        if (enableDynamicUpdates)
        {
            if (smartUpdates)
            {
                // Find and track moving obstacles at start
                TrackMovingObstacles();
                StartCoroutine(SmartUpdateCoroutine());
            }
            else
            {
                StartCoroutine(PeriodicUpdateCoroutine());
            }
        }
    }

    void OnEnable()
    {
        if (enableDynamicUpdates)
        {
            if (smartUpdates)
            {
                TrackMovingObstacles();
                if (updateCoroutine != null) StopCoroutine(updateCoroutine);
                updateCoroutine = StartCoroutine(SmartUpdateCoroutine());
            }
            else
            {
                if (updateCoroutine != null) StopCoroutine(updateCoroutine);
                updateCoroutine = StartCoroutine(PeriodicUpdateCoroutine());
            }
        }
    }

    void OnDisable()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }

    void TrackMovingObstacles()
    {
        trackedObstacles.Clear();
        ObstacleMovement[] obstacles = FindObjectsByType<ObstacleMovement>(FindObjectsSortMode.None);
        foreach (var obs in obstacles)
        {
            if (obs.gameObject.layer == Mathf.RoundToInt(Mathf.Log(unwalkableMask.value, 2)))
            {
                trackedObstacles.Add(obs);
                obs.RegisterGridManager(this);
            }
        }
    }

    // Call this from external scripts when a new obstacle is added
    public void RegisterNewObstacle(ObstacleMovement obstacle)
    {
        if (!trackedObstacles.Contains(obstacle))
        {
            trackedObstacles.Add(obstacle);
            obstacle.RegisterGridManager(this);
            // Trigger immediate update
            if (smartUpdates)
            {
                StartCoroutine(UpdateAffectedNodes(obstacle));
            }
        }
    }

    // Call this when an obstacle is removed
    public void UnregisterObstacle(ObstacleMovement obstacle)
    {
        trackedObstacles.Remove(obstacle);
        // Trigger update for affected area
        StartCoroutine(UpdateAffectedNodes(obstacle));
    }

    IEnumerator PeriodicUpdateCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            CreateGrid(); // Full rebuild
        }
    }

    IEnumerator SmartUpdateCoroutine()
    {
        while (true)
        {
            bool needsUpdate = false;
            foreach (var obs in trackedObstacles)
            {
                if (obs.HasMovedSignificantly())
                {
                    needsUpdate = true;
                    yield return StartCoroutine(UpdateAffectedNodes(obs));
                    obs.ResetMovementTracking();
                }
            }
            if (!needsUpdate)
            {
                yield return new WaitForSeconds(updateInterval);
            }
        }
    }

    public IEnumerator UpdateAffectedNodes(ObstacleMovement obstacle)
    {
        // Get the bounding area around the obstacle
        Bounds bounds = obstacle.GetComponent<Collider>().bounds;
        bounds.Expand(obstacleBuffer + nodeRadius);

        // Find affected nodes
        List<Node> affectedNodes = new List<Node>();
        Vector3 min = NodeFromWorldPoint(bounds.min).worldPosition;
        Vector3 max = NodeFromWorldPoint(bounds.max).worldPosition;

        int startX = Mathf.Max(0, Mathf.RoundToInt((min.x - nodeRadius - transform.position.x + gridWorldSize.x / 2) / nodeDiameter));
        int endX = Mathf.Min(gridSizeX - 1, Mathf.RoundToInt((max.x + nodeRadius - transform.position.x + gridWorldSize.x / 2) / nodeDiameter));
        int startY = Mathf.Max(0, Mathf.RoundToInt((min.z - nodeRadius - transform.position.z + gridWorldSize.y / 2) / nodeDiameter));
        int endY = Mathf.Min(gridSizeY - 1, Mathf.RoundToInt((max.z + nodeRadius - transform.position.z + gridWorldSize.y / 2) / nodeDiameter));

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                affectedNodes.Add(grid[x, y]);
            }
        }

        // Update only these nodes
        foreach (Node node in affectedNodes)
        {
            Vector3 worldPoint = node.worldPosition;
            bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));

            float proximityCost = 0f;
            if (walkable)
            {
                Collider[] nearbyObstacles = Physics.OverlapSphere(worldPoint, obstacleBuffer, unwalkableMask);
                if (nearbyObstacles.Length > 0)
                {
                    proximityCost = 1f - (Vector3.Distance(worldPoint, nearbyObstacles[0].ClosestPoint(worldPoint)) / obstacleBuffer);
                    proximityCost = Mathf.Clamp01(proximityCost);
                }
            }

            node.walkable = walkable;
            node.proximityPenalty = proximityCost;
        }

        yield return null; // Yield to avoid frame drop
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position
            - Vector3.right * gridWorldSize.x / 2
            - Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft
                    + Vector3.right * (x * nodeDiameter + nodeRadius)
                    + Vector3.forward * (y * nodeDiameter + nodeRadius);

                bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));

                // Compute proximity to obstacles
                float proximityCost = 0f;
                if (walkable)
                {
                    Collider[] nearbyObstacles = Physics.OverlapSphere(worldPoint, obstacleBuffer, unwalkableMask);
                    if (nearbyObstacles.Length > 0)
                    {
                        // Closer = higher cost
                        proximityCost = 1f - (Vector3.Distance(worldPoint, nearbyObstacles[0].ClosestPoint(worldPoint)) / obstacleBuffer);
                        proximityCost = Mathf.Clamp01(proximityCost);
                    }
                }

                Node newNode = new Node(walkable, worldPoint, x, y);
                newNode.proximityPenalty = proximityCost;
                grid[x, y] = newNode;
            }
        }
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.z + gridWorldSize.y / 2) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);

        return grid[x, y];
    }

    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (checkX >= 0 && checkX < gridSizeX &&
                    checkY >= 0 && checkY < gridSizeY)
                {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }

        return neighbours;
    }

    // Public method to force a full grid update (e.g., after adding/removing static obstacles)
    public void ForceUpdateGrid()
    {
        CreateGrid();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.grey;
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1, gridWorldSize.y));

        if (grid != null && displayGridGizmos)
        {
            foreach (Node n in grid)
            {
                if (!n.walkable)
                    Gizmos.color = unwalkableColor;
                else if (n.proximityPenalty > 0.01f)
                    Gizmos.color = Color.Lerp(walkableColor, nearObstacleColor, n.proximityPenalty);
                else
                    Gizmos.color = walkableColor;

                if (path != null && path.Contains(n))
                    Gizmos.color = pathColor;

                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
            }
        }

        // Optional: Draw smooth line for path
        if (path != null && displayPathLine && path.Count > 1)
        {
            Gizmos.color = lineColor;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 start = path[i].worldPosition + Vector3.up * lineHeightOffset;
                Vector3 end = path[i + 1].worldPosition + Vector3.up * lineHeightOffset;
                Gizmos.DrawLine(start, end);
            }
        }
    }
}

// Add this script to moving obstacles to enable smart tracking
public class ObstacleMovement : MonoBehaviour
{
    [Header("Movement Tracking")]
    public float movementThreshold = 0.1f; // Distance to consider "moved significantly"
    
    private GridManager gridManager;
    private Vector3 lastPosition;
    private bool isTracked = false;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        if (isTracked && gridManager != null && Vector3.Distance(transform.position, lastPosition) > movementThreshold)
        {
            // Notify grid manager of movement
            gridManager.StartCoroutine(gridManager.UpdateAffectedNodes(this));
            lastPosition = transform.position;
        }
    }

    public void RegisterGridManager(GridManager manager)
    {
        gridManager = manager;
        isTracked = true;
        lastPosition = transform.position;
    }

    public bool HasMovedSignificantly()
    {
        return Vector3.Distance(transform.position, lastPosition) > movementThreshold;
    }

    public void ResetMovementTracking()
    {
        lastPosition = transform.position;
    }
}