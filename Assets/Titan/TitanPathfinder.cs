using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles all pathfinding and navigation for the Titan AI.
/// Manages patrol routes, chase behavior, and dynamic path recalculation.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TitanPathfinder : MonoBehaviour
{
    #region Serialized Fields
    [Header("Pathfinding Settings")]
    [SerializeField] private float pathUpdateInterval = 0.5f;
    [SerializeField] private float stoppingDistance = 0.5f;
    
    [Header("Obstacle Avoidance")]
    [SerializeField] private bool enableDynamicAvoidance = true;
    [SerializeField] private float forwardRayDistance = 10f;
    [SerializeField] private LayerMask obstacleMask = -1;
    [SerializeField] private float obstacleCheckHeight = 2f;
    
    [Header("Patrol Settings")]
    [SerializeField] private bool loopPatrol = true;
    [SerializeField] private bool randomizePatrol = false;
    
    [Header("Debug")]
    [SerializeField] private bool debugPaths = false;
    #endregion

    #region Private Fields
    private NavMeshAgent agent;
    private Transform[] waypoints;
    private int currentWaypointIndex;
    private Vector3 lastKnownPosition;
    private Coroutine pathUpdateCoroutine;
    private bool isInitialized;
    
    // Path calculation optimization
    private NavMeshPath cachedPath;
    private float lastPathCalculationTime;
    private const float MIN_PATH_RECALC_INTERVAL = 0.1f;
    #endregion

    #region Properties
    public bool IsMoving => agent != null && agent.velocity.sqrMagnitude > 0.01f;
    public Vector3 CurrentDestination => agent != null ? agent.destination : Vector3.zero;
    public int CurrentWaypointIndex => currentWaypointIndex;
    public Vector3 LastKnownPosition => lastKnownPosition;
    #endregion

    #region Initialization
    public void Initialize(NavMeshAgent navAgent, Transform[] patrolPoints)
    {
        if (navAgent == null)
        {
            Debug.LogError("[TitanPathfinder] Cannot initialize with null NavMeshAgent!", this);
            return;
        }

        agent = navAgent;
        waypoints = patrolPoints;
        cachedPath = new NavMeshPath();
        
        ConfigureAgent();
        
        isInitialized = true;

        if (enableDynamicAvoidance)
        {
            StartPathUpdating();
        }

        ValidateWaypoints();
    }

    private void ConfigureAgent()
    {
        if (agent == null) return;

        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.autoRepath = true;
    }

    private void ValidateWaypoints()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[TitanPathfinder] {gameObject.name} has no patrol waypoints assigned.", this);
            return;
        }

        // Check for null waypoints
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
            {
                Debug.LogWarning($"[TitanPathfinder] Waypoint {i} is null on {gameObject.name}!", this);
            }
        }
    }

    private void OnDestroy()
    {
        StopPathUpdating();
    }

    private void OnDisable()
    {
        StopPathUpdating();
    }
    #endregion

    #region Path Updating
    private void StartPathUpdating()
    {
        if (pathUpdateCoroutine == null && isInitialized)
        {
            pathUpdateCoroutine = StartCoroutine(UpdatePathCoroutine());
        }
    }

    private void StopPathUpdating()
    {
        if (pathUpdateCoroutine != null)
        {
            StopCoroutine(pathUpdateCoroutine);
            pathUpdateCoroutine = null;
        }
    }

    private IEnumerator UpdatePathCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(pathUpdateInterval);

        while (true)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
            {
                CheckForObstacles();
            }

            yield return wait;
        }
    }

    private void CheckForObstacles()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * obstacleCheckHeight;
        Vector3 rayDirection = transform.forward;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, forwardRayDistance, obstacleMask))
        {
            // Check if we hit an obstacle that's not our destination
            if (!IsDestinationObject(hit.collider.gameObject))
            {
                RecalculatePath();
            }
        }

        if (debugPaths)
        {
            Debug.DrawRay(rayOrigin, rayDirection * forwardRayDistance, Color.yellow);
        }
    }

    private bool IsDestinationObject(GameObject obj)
    {
        // Check if the object we hit is our current destination
        if (agent.destination == Vector3.zero) return false;
        
        float distToDestination = Vector3.Distance(obj.transform.position, agent.destination);
        return distToDestination < 2f;
    }

    private void RecalculatePath()
    {
        // Throttle path recalculation
        if (Time.time - lastPathCalculationTime < MIN_PATH_RECALC_INTERVAL)
        {
            return;
        }

        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 currentDestination = agent.destination;
        
        if (NavMesh.SamplePosition(currentDestination, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, cachedPath))
            {
                if (cachedPath.status == NavMeshPathStatus.PathComplete)
                {
                    agent.SetPath(cachedPath);
                    lastPathCalculationTime = Time.time;
                    
                    if (debugPaths)
                    {
                        Debug.Log($"[TitanPathfinder] Path recalculated for {gameObject.name}", this);
                    }
                }
            }
        }
    }
    #endregion

    #region Navigation Control
    public void SetDestination(Vector3 target, float speed)
    {
        if (!ValidateAgent()) return;

        agent.speed = Mathf.Max(0f, speed);

        // Only set destination if it's different enough from current
        if (Vector3.Distance(agent.destination, target) > 0.1f)
        {
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // Fallback: set directly if sampling fails
                agent.SetDestination(target);
            }
        }
    }

    public void Stop()
    {
        if (!ValidateAgent()) return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    public void Resume()
    {
        if (!ValidateAgent()) return;

        agent.isStopped = false;
    }
    #endregion

    #region Patrol Behavior
    public void Patrol()
    {
        if (!ValidateAgent() || waypoints == null || waypoints.Length == 0)
        {
            return;
        }

        // Get next waypoint
        int nextIndex = GetNextWaypointIndex();
        
        if (waypoints[nextIndex] != null)
        {
            currentWaypointIndex = nextIndex;
            SetDestination(waypoints[currentWaypointIndex].position, agent.speed);
            
            if (debugPaths)
            {
                Debug.Log($"[TitanPathfinder] Patrolling to waypoint {currentWaypointIndex}", this);
            }
        }
        else
        {
            Debug.LogWarning($"[TitanPathfinder] Waypoint {nextIndex} is null!", this);
            // Skip to next valid waypoint
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            Patrol();
        }
    }

    private int GetNextWaypointIndex()
    {
        if (randomizePatrol)
        {
            // Random patrol: pick a different waypoint
            int randomIndex;
            do
            {
                randomIndex = Random.Range(0, waypoints.Length);
            }
            while (randomIndex == currentWaypointIndex && waypoints.Length > 1);
            
            return randomIndex;
        }
        else
        {
            // Sequential patrol
            int nextIndex = currentWaypointIndex + 1;
            
            if (loopPatrol)
            {
                return nextIndex % waypoints.Length;
            }
            else
            {
                // Ping-pong patrol
                if (nextIndex >= waypoints.Length)
                {
                    return waypoints.Length - 2;
                }
                return nextIndex;
            }
        }
    }

    public void SetPatrolWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
        currentWaypointIndex = 0;
        ValidateWaypoints();
    }
    #endregion

    #region Chase Behavior
    public void Chase(Vector3 targetPosition)
    {
        if (!ValidateAgent()) return;

        lastKnownPosition = targetPosition;
        SetDestination(targetPosition, agent.speed);
    }

    public void SearchLastKnown()
    {
        if (!ValidateAgent()) return;

        if (lastKnownPosition != Vector3.zero)
        {
            SetDestination(lastKnownPosition, agent.speed);
        }
    }
    #endregion

    #region State Queries
    public bool IsDestinationReached()
    {
        if (!ValidateAgent()) return true;

        // Check if agent has reached destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // Additional check: is velocity near zero?
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasPath()
    {
        return ValidateAgent() && agent.hasPath;
    }

    public float GetRemainingDistance()
    {
        if (!ValidateAgent() || !agent.hasPath) return 0f;
        return agent.remainingDistance;
    }
    #endregion

    #region Validation
    private bool ValidateAgent()
    {
        if (agent == null)
        {
            Debug.LogError("[TitanPathfinder] NavMeshAgent is null!", this);
            return false;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[TitanPathfinder] {gameObject.name} is not on NavMesh!", this);
            return false;
        }

        return true;
    }
    #endregion

    #region Debug Visualization
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!debugPaths) return;

        // Draw waypoints
        if (waypoints != null && waypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    // Draw waypoint sphere
                    Gizmos.color = i == currentWaypointIndex ? Color.green : Color.cyan;
                    Gizmos.DrawWireSphere(waypoints[i].position, 1f);
                    
                    // Draw connections
                    if (loopPatrol || i < waypoints.Length - 1)
                    {
                        int nextIndex = loopPatrol ? (i + 1) % waypoints.Length : i + 1;
                        if (nextIndex < waypoints.Length && waypoints[nextIndex] != null)
                        {
                            Gizmos.color = new Color(0, 1, 1, 0.5f);
                            Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
                        }
                    }
                }
            }
        }

        // Draw current path
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.yellow;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }

        // Draw last known position
        if (lastKnownPosition != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.5f);
        }

        // Draw forward ray
        if (enableDynamicAvoidance)
        {
            Gizmos.color = Color.yellow;
            Vector3 rayOrigin = transform.position + Vector3.up * obstacleCheckHeight;
            Gizmos.DrawRay(rayOrigin, transform.forward * forwardRayDistance);
        }
    }
    #endif
    #endregion
}
