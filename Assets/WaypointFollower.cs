using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class WaypointFollower : MonoBehaviour
{
    public List<Transform> waypoints = new List<Transform>();

    void OnValidate()
    {
        // auto collect children as waypoints
        waypoints.Clear();
        foreach (Transform t in transform)
            waypoints.Add(t);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform t = transform.GetChild(i);
            Gizmos.DrawSphere(t.position, 0.3f);
            if (i + 1 < transform.childCount)
                Gizmos.DrawLine(t.position, transform.GetChild(i + 1).position);
            else if (transform.childCount > 1)
                Gizmos.DrawLine(t.position, transform.GetChild(0).position); // loop
        }
    }
}
