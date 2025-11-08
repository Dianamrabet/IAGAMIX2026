using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerAgent : MonoBehaviour
{
    public enum State { Roam, Pursue, Attack }

    [Header("Perception")]
    public string enemyTag = "Enemy"; // Tag for enemies
    public float visionRadius = 15f;
    [Range(0, 180)] public float fov = 90f; // Field of view for detection

    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 5f;
    public float rotationSpeed = 10f;
    public LayerMask obstacleMask = -1; // For raycasts/visibility & avoidance

    [Header("Roam")]
    public float roamDistance = 10f; // Max distance for random roam targets
    public float minRoamInterval = 2f; // Min time before new roam target
    public float maxRoamInterval = 5f; // Max time before new roam target

    [Header("Pursue & Attack")]
    public float attackDistance = 2f; // Distance to switch to attack
    public float loseTargetDistance = 20f; // Distance to lose pursuit

    [Header("Avoidance")]
    public float avoidanceDistance = 1.5f; // For local obstacle dodging

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool enableDebugLogs = true;

    // Components
    CharacterController controller;
    Animator animator;
    KnifeComboController comboController; // From your previous script—handles attack combos

    // State
    State currentState = State.Roam;
    Transform nearestEnemy;
    Vector3 roamTarget;
    float nextRoamTime;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        comboController = GetComponent<KnifeComboController>(); // Assumes this exists from your prior code

        if (comboController == null)
            Debug.LogError("PlayerAgent: Attach KnifeComboController for attacks!");

        SetNewRoamTarget();
        nextRoamTime = Time.time + Random.Range(minRoamInterval, maxRoamInterval);
        if (enableDebugLogs) Debug.Log("Player: Started in Roam state (no A* needed).");
    }

    void Update()
    {
        // State transitions
        DetectNearestEnemy();
        bool enemyInRange = nearestEnemy != null;

        switch (currentState)
        {
            case State.Roam:
                if (enemyInRange && IsEnemyVisible(nearestEnemy))
                    ChangeState(State.Pursue);
                else if (Time.time >= nextRoamTime)
                    SetNewRoamTarget();
                break;

            case State.Pursue:
                if (!enemyInRange || !IsEnemyVisible(nearestEnemy) || Vector3.Distance(transform.position, nearestEnemy.position) > loseTargetDistance)
                    ChangeState(State.Roam);
                else if (Vector3.Distance(transform.position, nearestEnemy.position) <= attackDistance)
                    ChangeState(State.Attack);
                break;

            case State.Attack:
                if (!enemyInRange || Vector3.Distance(transform.position, nearestEnemy.position) > attackDistance * 1.5f)
                    ChangeState(State.Pursue);
                break;
        }

        // Execute state behavior
        ExecuteState();
    }

    void ExecuteState()
    {
        float currentSpeed = walkSpeed;
        bool isMoving = false;
        Vector3 moveDirection = Vector3.zero;

        switch (currentState)
        {
            case State.Roam:
                // Direct seek to roam target with avoidance
                if (Vector3.Distance(transform.position, roamTarget) > 1f)
                {
                    moveDirection = Seek(roamTarget);
                    isMoving = true;
                    currentSpeed = walkSpeed; // Casual roam
                }
                break;

            case State.Pursue:
                if (nearestEnemy != null)
                {
                    moveDirection = Seek(nearestEnemy.position);
                    isMoving = true;
                    currentSpeed = runSpeed; // Faster pursuit
                }
                break;

            case State.Attack:
            if (nearestEnemy != null)
            {
                // Face the enemy
                Vector3 dirToEnemy = (nearestEnemy.position - transform.position).normalized;
                dirToEnemy.y = 0;
                if (dirToEnemy.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToEnemy), rotationSpeed * Time.deltaTime);

                // Trigger random knife combo attack
                if (comboController != null && !comboController.IsAttacking()) // Now exists!
                {
                    comboController.PlayRandomCombo(nearestEnemy); // Now exists: randomly selects Combo1/2/3
                }
            }
            isMoving = false;
            break;
        }

        // Apply avoidance on top of seek direction
        Vector3 avoidance = AvoidObstacles();
        moveDirection += avoidance;

        // Normalize and apply speed
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            moveDirection = moveDirection.normalized * currentSpeed;
            moveDirection.y = -9.81f * Time.deltaTime; // Basic gravity (adjust if using full gravity)
        }

        // Move with CharacterController
        controller.Move(moveDirection * Time.deltaTime);

        // Face movement direction
        if (isMoving)
        {
            Vector3 horizontalMove = new Vector3(moveDirection.x, 0, moveDirection.z);
            if (horizontalMove.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horizontalMove.normalized), rotationSpeed * Time.deltaTime);
        }

        // Animation (using 'walk' bool from your prior setup)
        animator.SetBool("walk", isMoving);

        if (enableDebugLogs && Time.time % 1f < Time.deltaTime)
        {
            Debug.Log($"[Player] State: {currentState} | Enemy: {(nearestEnemy ? nearestEnemy.name : "None")}");
        }
    }

    #region Steering Behaviors
    Vector3 Seek(Vector3 target)
    {
        Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z); // Keep Y level
        Vector3 desired = (flatTarget - transform.position).normalized * walkSpeed; // Use base speed here
        Vector3 steer = desired - controller.velocity; // Approximate current velocity
        return Vector3.ClampMagnitude(steer, runSpeed); // Cap for pursuit
    }

    Vector3 AvoidObstacles()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1f; // Eye/chest height
        int rayCount = 5;
        float halfFOV = 45f;
        Vector3 bestDir = transform.forward;
        float bestClearance = -1f;
        bool foundObstacle = false;

        for (int i = 0; i < rayCount; i++)
        {
            float t = (float)i / (rayCount - 1);
            float angle = Mathf.Lerp(-halfFOV, halfFOV, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;

            if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, avoidanceDistance, obstacleMask))
            {
                foundObstacle = true;
                if (hit.distance > bestClearance)
                {
                    bestClearance = hit.distance;
                    bestDir = -hit.normal; // Steer away from surface normal
                }
            }
            else
            {
                bestDir = dir;
                break; // Clear path found
            }
        }

        if (!foundObstacle) return Vector3.zero;

        bestDir.y = 0;
        return bestDir.normalized * walkSpeed * 0.5f; // Gentle avoidance force
    }
    #endregion

    #region Perception & Helpers
    void DetectNearestEnemy()
    {
        nearestEnemy = null;
        float nearestDist = visionRadius;
        Collider[] potentialEnemies = Physics.OverlapSphere(transform.position, visionRadius, LayerMask.GetMask("Enemy")); // Assume enemies on 'Enemy' layer

        foreach (var col in potentialEnemies)
        {
            if (!col.CompareTag(enemyTag)) continue;
            Transform enemy = col.transform;
            float dist = Vector3.Distance(transform.position, enemy.position);
            if (dist < nearestDist && IsEnemyVisible(enemy))
            {
                nearestDist = dist;
                nearestEnemy = enemy;
            }
        }
    }

    bool IsEnemyVisible(Transform enemy)
    {
        Vector3 dirToEnemy = (enemy.position - transform.position).normalized;
        Vector3 flatDir = new Vector3(dirToEnemy.x, 0, dirToEnemy.z);
        if (flatDir.magnitude < 0.01f) return false;

        // FOV check
        if (Vector3.Angle(transform.forward, flatDir.normalized) > fov * 0.5f) return false;

        // Line of sight (raycast)
        Vector3 rayStart = transform.position + Vector3.up * 1f; // Eye height
        if (Physics.Raycast(rayStart, flatDir, out RaycastHit hit, visionRadius, obstacleMask))
        {
            return hit.collider.transform == enemy; // Visible if ray hits the enemy directly
        }
        return false;
    }

    void SetNewRoamTarget()
    {
        // Generate random point within roamDistance
        Vector3 randomDir = Random.insideUnitSphere;
        randomDir.y = 0;
        if (randomDir.sqrMagnitude < 0.01f) randomDir = transform.forward;
        roamTarget = transform.position + randomDir.normalized * Random.Range(3f, roamDistance);

        nextRoamTime = Time.time + Random.Range(minRoamInterval, maxRoamInterval);
        if (enableDebugLogs) Debug.Log($"[Player] New roam target: {roamTarget}");
    }
    #endregion

    void ChangeState(State next)
    {
        if (next == currentState) return;
        if (enableDebugLogs)
            Debug.Log($"Player: {currentState} -> {next}");
        currentState = next;

        // Optional: State entry actions
        if (next == State.Roam) SetNewRoamTarget();
    }

    #region Gizmos
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // Vision sphere and FOV
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, visionRadius);
        Vector3 fwd = transform.forward * visionRadius;
        Quaternion left = Quaternion.AngleAxis(-fov * 0.5f, Vector3.up);
        Quaternion right = Quaternion.AngleAxis(fov * 0.5f, Vector3.up);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + left * fwd);
        Gizmos.DrawLine(transform.position, transform.position + right * fwd);

        // Roam target
        if (currentState == State.Roam)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(roamTarget, 0.5f);
            Gizmos.DrawLine(transform.position, roamTarget);
        }

        // Nearest enemy
        if (nearestEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, nearestEnemy.position);
        }

        // Avoidance rays
        Vector3 rayOrigin = transform.position + Vector3.up * 1f;
        int rayCount = 5;
        float halfFOV = 45f;
        for (int i = 0; i < rayCount; i++)
        {
            float t = (float)i / (rayCount - 1);
            float angle = Mathf.Lerp(-halfFOV, halfFOV, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(rayOrigin, dir * avoidanceDistance);
        }
    }
    #endregion
}