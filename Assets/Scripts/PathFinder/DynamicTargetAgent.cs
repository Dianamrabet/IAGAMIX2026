using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(KnifeComboController))]
public class DynamicTargetAgent : MonoBehaviour
{
    [Header("Agent Settings")]
    public Transform target;
    public float speed = 3f;
    public float rotationSpeed = 10f;
    public float waypointThreshold = 0.1f;
    public float stopDistance = 1.5f;

    [Header("Proximity Detection")]
    public string playerTag = "Player"; // Tag for player objects
    public float proximityRadius = 3f; // Radius to detect/confirm player proximity for dodging
    public LayerMask playerLayer = -1; // Layer for player detection (default all)

    [Header("Path Update Settings")]
    public float pathUpdateInterval = 0.3f;
    public float minTargetMoveDistance = 1f; // Only update path if target moved this much

    [Header("Physics")]
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.1f;

    [Header("Dodge")]
    public float dodgeChance = 0.2f; // 20% chance
    public float dodgeCooldown = 2f; // Cooldown after dodge to prevent spam
    public float dodgeDuration = 1f; // Duration to hold 'dodge' bool (anim exit time should match)

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool enableDebugLogs = true; // Set to true to debug stopping issue

    private AStarPathfinding pathfinder;
    private List<Node> path;
    private int currentWaypointIndex = 0;
    private CharacterController controller;
    private Animator animator;
    private KnifeComboController comboController; 

    // New: Proximity and Dodge
    private KnifeComboController playerComboController; // Reference to player's attack state
    private float lastDodgeTime = -Mathf.Infinity; // For cooldown
    private bool isDodging = false;

    private Vector3 velocity;
    private bool isMoving = false;
    public bool contact = false;
    private float idleStartTime;
    private Vector3 lastTargetPosition; // For efficient path updates
    private Coroutine updatePathCoroutine; // To manage the routine

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        comboController = GetComponent<KnifeComboController>();
        pathfinder = FindFirstObjectByType<AStarPathfinding>(); // Use FindObjectOfType for safety

        if (pathfinder == null)
            Debug.LogError("DynamicTargetAgent: No AStarPathfinding found in scene!");

        if (comboController != null)
            comboController.OnComboFinished += HandleComboFinished; // Subscribe to combo finish

        // New: Setup player reference for proximity/dodge
        SetupPlayerReference();

        lastTargetPosition = target != null ? target.position : Vector3.zero;
        
        updatePathCoroutine = StartCoroutine(UpdatePathRoutine());
        if (enableDebugLogs) Debug.Log("DynamicTargetAgent: Initialized and initial path updated.");
    }

    void OnDestroy()
    {
        // Unsubscribe to avoid leaks
        if (comboController != null)
            comboController.OnComboFinished -= HandleComboFinished;

        // Stop coroutine
        if (updatePathCoroutine != null)
            StopCoroutine(updatePathCoroutine);
    }

    // New: Find and cache player combo controller
    private void SetupPlayerReference()
    {
        if (target == null)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
            Transform nearestPlayer = null;
            float nearestDist = Mathf.Infinity;
            foreach (var col in players)
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPlayer = col.transform;
                }
            }
            if (nearestPlayer != null)
            {
                target = nearestPlayer;
                if (enableDebugLogs) Debug.Log($"DynamicTargetAgent: Auto-assigned nearest player target: {target.name}");
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning("DynamicTargetAgent: No player target found or assigned!");
                return;
            }
        }

        // Get player's combo controller
        playerComboController = target.GetComponentInChildren<KnifeComboController>();
        if (playerComboController == null)
            Debug.LogWarning("DynamicTargetAgent: No KnifeComboController found on player target!");
    }

    // New: Check proximity to player and handle dodge
    private bool CheckProximityAndDodge()
    {
        if (target == null || playerComboController == null || isDodging || Time.time < lastDodgeTime + dodgeCooldown)
            return false;

        float distToTarget = Vector3.Distance(transform.position, target.position);
        if (distToTarget > proximityRadius)
            return false;

        // Player is attacking and we're in proximity
        if (playerComboController.IsAttacking())
        {
            // 20% chance to dodge
            if (Random.value < dodgeChance)
            {
                StartCoroutine(PerformDodge());
                return true;
            }
        }
        return false;
    }

    // New: Dodge coroutine
    private IEnumerator PerformDodge()
    {
        isDodging = true;
        lastDodgeTime = Time.time;
        contact = true; // Pause normal attack/movement

        animator.SetBool("dodge", true);
        if (enableDebugLogs) Debug.Log("DynamicTargetAgent: Dodging player attack!");

        // Hold for duration (align with anim exit time)
        yield return new WaitForSeconds(dodgeDuration);

        animator.SetBool("dodge", false);
        isDodging = false;
        contact = false; // Resume

        if (enableDebugLogs) Debug.Log("DynamicTargetAgent: Dodge finished, resuming.");
        UpdatePathNow(); // Fresh path post-dodge
    }

    // New: Force immediate path update (call after resume to fix stopping)
    private void UpdatePathNow()
    {
        if (target == null || contact || pathfinder == null) 
        {
            if (enableDebugLogs && pathfinder == null) Debug.LogError("DynamicTargetAgent: Pathfinder is null—check scene setup!");
            return;
        }

        // ✅ Extra safety: Wait one frame if grid might not be ready (rare, but covers edge cases)
        StartCoroutine(DelayedPathUpdate());

        lastTargetPosition = target.position;
    }

    private IEnumerator DelayedPathUpdate()
    {
        yield return null; // Wait one frame (ensures GridManager.Start() has run)

        path = pathfinder.FindPath(transform.position, target.position);

        if (path != null && path.Count > 0)
        {
            currentWaypointIndex = 0;
            // Skip initial waypoints if already past them
            while (currentWaypointIndex < path.Count)
            {
                Vector3 waypointPos = path[currentWaypointIndex].worldPosition;
                Vector3 direction = waypointPos - transform.position;
                direction.y = 0;
                float dist = direction.magnitude;
                float threshold = (currentWaypointIndex == path.Count - 1) ? stopDistance : waypointThreshold;
                if (dist < threshold)
                    currentWaypointIndex++;
                else
                    break;
            }
            if (enableDebugLogs) Debug.Log($"DynamicTargetAgent: Path updated NOW ({path.Count} nodes, index={currentWaypointIndex}).");
        }
        else
        {
            if (enableDebugLogs) Debug.LogWarning("DynamicTargetAgent: No valid path found on immediate update!");
            path = null;
            currentWaypointIndex = 0;
        }
    }

    private void HandleComboFinished()
    {
        if (target == null)
        {
            if (enableDebugLogs) Debug.LogError("DynamicTargetAgent: Target is not assigned!");
            target = null;
            StartCoroutine(ResumeAfterDelay(0));
            return;
        }

        if (target.childCount == 0)
        {
            if (enableDebugLogs) Debug.LogError("DynamicTargetAgent: Target has no children!");
            return;
        }

        Animator anim = target.GetComponent<Animator>();
        if (anim == null)
        {
            if (enableDebugLogs) Debug.LogWarning("DynamicTargetAgent: No Animation component found on target's first child!");

        }
        else
        {
            anim.SetBool("stun", true);
            if (enableDebugLogs) Debug.Log("DynamicTargetAgent: Triggered enemy reaction animation.");
            Health h = target.GetComponent<Health>();
            if (h != null)
            {
                h.DoDamage(25f);
            }
        }

        
        StartCoroutine(ResumeAfterDelay(0.5f));
    }

    private IEnumerator ResumeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        contact = false; // Resume movement
        if (enableDebugLogs) Debug.Log("DynamicTargetAgent: Resuming movement after combo.");

        // Fix: Force immediate path update to prevent stopping
        UpdatePathNow();
    }

    IEnumerator UpdatePathRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        UpdatePathNow();
        while (true)
        {
            if (target != null && !contact)
            {
                // Only update if target moved significantly
                if (Vector3.Distance(target.position, lastTargetPosition) > minTargetMoveDistance)
                {
                    UpdatePathNow(); // Reuse the method for consistency
                }
            }
            else
            {
                if (enableDebugLogs && path != null) Debug.Log("DynamicTargetAgent: Skipping path update (contact or no target).");
                path = null;
                currentWaypointIndex = 0;
            }

            yield return new WaitForSeconds(pathUpdateInterval);
        }
    }

    void Update()
    {
        // New: Check for dodge before other logic
        bool dodged = CheckProximityAndDodge();
        if (dodged) return; // Skip movement during dodge

        // Grounded check and gravity
        if (controller.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance))
        {
            velocity.y = -2f; // Small downward force to stick to ground
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        if (path == null || path.Count == 0)
        {
            if (!contact) // Only stop anim if not in attack
                StopMoving();
            return;
        }

        MoveAlongPath();

        // Conditional debug log (not every frame)
        if (enableDebugLogs && Time.time % 1f < Time.deltaTime) // Log ~once per second
        {
            Debug.Log($"DynamicTargetAgent: State - Contact:{contact}, Dodging:{isDodging}, Moving:{isMoving}, PathLen:{(path?.Count ?? 0)}, Index:{currentWaypointIndex}, Idle:{GetIdleTime():F1}s, DistToTarget:{(target ? Vector3.Distance(transform.position, target.position) : -1):F1}");
        }
    }

    void MoveAlongPath()
    {
        if (contact)
        {
            if (enableDebugLogs) Debug.Log("DynamicTargetAgent: In contact - skipping move.");
            return;
        }

        if (currentWaypointIndex >= path.Count)
        {
            if (enableDebugLogs) Debug.LogWarning("DynamicTargetAgent: Waypoint index out of bounds - forcing path update.");
            UpdatePathNow();
            return;
        }

        if (target != null)
        {
            float distToTarget = Vector3.Distance(transform.position, target.position);
            if (distToTarget <= stopDistance)
            {
                StopMoving();
                contact = true;

                if (comboController != null)
                {
                    comboController.PlayRandomCombo(target);
                    if (enableDebugLogs) Debug.Log($"DynamicTargetAgent: (dist={distToTarget:F1}).");
                }
                return;
            }
        }

        Vector3 targetPos = path[currentWaypointIndex].worldPosition;
        Vector3 direction = targetPos - transform.position;
        direction.y = 0;

        float threshold = (currentWaypointIndex == path.Count - 1) ? stopDistance : waypointThreshold;

        bool willMove = direction.magnitude >= threshold;
        if (!willMove)
        {
            if (currentWaypointIndex < path.Count - 1)
                currentWaypointIndex++;
            StopMoving();
            return;
        }

        // Rotation and movement
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }

        Vector3 moveVector = direction.normalized * speed * Time.deltaTime;
        controller.Move(moveVector);

        // Apply vertical velocity (gravity/jump if added later)
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);

        if (!isMoving)
        {
            animator.SetBool("walk", true);
            isMoving = true;
            idleStartTime = 0f;
        }
    }

    private void StopMoving()
    {
        if (isMoving)
        {
            animator.SetBool("walk", false);
            isMoving = false;
            idleStartTime = Time.time;
        }
    }

    public float GetIdleTime()
    {
        return isMoving ? 0f : (Time.time - idleStartTime);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        if (path != null && path.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 start = path[i].worldPosition + Vector3.up * 0.1f;
                Vector3 end = path[i + 1].worldPosition + Vector3.up * 0.1f;
                Gizmos.DrawLine(start, end);
            }

            // Highlight current waypoint
            if (currentWaypointIndex < path.Count)
            {
                Gizmos.color = Color.yellow;
                Vector3 currentWP = path[currentWaypointIndex].worldPosition + Vector3.up * 0.5f;
                Gizmos.DrawWireSphere(currentWP, 0.2f);
            }
        }

        // Draw stop distance sphere around target
        if (target != null && drawGizmos)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, stopDistance);
        }

        // New: Draw proximity radius for dodge detection
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, proximityRadius);

        // Draw agent vision/stop radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}