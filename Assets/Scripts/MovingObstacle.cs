using UnityEngine;

public class MovingObstacle : MonoBehaviour
{
    public int a = 30;
    public int b = -40;
    public float speed = 2f;
    private bool go = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.position = new Vector3(a, transform.position.y, transform.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        float step = speed * Time.deltaTime;
        if (go)
        {
            // Moving towards b (left, assuming b < a)
            float remaining = transform.position.x - b;
            float moveDistance = Mathf.Min(step, remaining);
            transform.position -= Vector3.right * moveDistance;
            if (transform.position.x <= b)
            {
                transform.position = new Vector3(b, transform.position.y, transform.position.z);
                go = false;
            }
        }
        else
        {
            // Moving towards a (right)
            float remaining = a - transform.position.x;
            float moveDistance = Mathf.Min(step, remaining);
            transform.position += Vector3.right * moveDistance;
            if (transform.position.x >= a)
            {
                transform.position = new Vector3(a, transform.position.y, transform.position.z);
                go = true;
            }
        }
    }
}