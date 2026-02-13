using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Main coordinator for Titan AI system. Manages component initialization and event delegation.
/// Follows single responsibility principle by delegating behavior to specialized components.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TitanAI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Components")]
    [SerializeField] private NavMeshAgent agent;
    
    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 30f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Patrol Settings")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private float patrolSpeed = 3f;
    [SerializeField] private float chaseSpeed = 5f;
    
    [Header("Agent Configuration")]
    [SerializeField] private float agentRadius = 2f;
    [SerializeField] private float agentHeight = 10f;
    [SerializeField] private ObstacleAvoidanceType avoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    #endregion

    #region Properties
    public float PatrolSpeed => patrolSpeed;
    public float ChaseSpeed => chaseSpeed;
    public NavMeshAgent Agent => agent;
    #endregion

    #region Components
    private TitanPathfinder pathfinder;
    private TitanDetector detector;
    private TitanBrain brain;
    #endregion

    #region Events
    public event Action<GameObject> OnPlayerDetected;
    public event Action OnPlayerLost;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        ConfigureAgent();
        InitializeDependencies();
        WireEvents();
    }

    private void Update()
    {
        if (brain != null)
        {
            brain.UpdateBrain();
        }
    }

    private void OnDestroy()
    {
        UnwireEvents();
    }

    private void OnValidate()
    {
        // Clamp values in editor
        detectionRadius = Mathf.Max(1f, detectionRadius);
        patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
        chaseSpeed = Mathf.Max(patrolSpeed, chaseSpeed);
        agentRadius = Mathf.Max(0.1f, agentRadius);
        agentHeight = Mathf.Max(0.1f, agentHeight);
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Visualize patrol waypoints
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] != null)
                {
                    Gizmos.DrawWireSphere(patrolWaypoints[i].position, 1f);
                    
                    // Draw lines between waypoints
                    if (i < patrolWaypoints.Length - 1 && patrolWaypoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(patrolWaypoints[i].position, patrolWaypoints[i + 1].position);
                    }
                }
            }
        }
    }
    #endif
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Get or add required components
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"[TitanAI] NavMeshAgent component missing on {gameObject.name}. Adding component.", this);
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

        pathfinder = GetComponent<TitanPathfinder>();
        if (pathfinder == null)
        {
            pathfinder = gameObject.AddComponent<TitanPathfinder>();
        }

        detector = GetComponent<TitanDetector>();
        if (detector == null)
        {
            detector = gameObject.AddComponent<TitanDetector>();
        }

        brain = GetComponent<TitanBrain>();
        if (brain == null)
        {
            brain = gameObject.AddComponent<TitanBrain>();
        }
    }

    private void ConfigureAgent()
    {
        if (agent == null) return;

        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.obstacleAvoidanceType = avoidanceType;
        agent.speed = patrolSpeed;
        
        // Additional safe defaults
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;
    }

    private void InitializeDependencies()
    {
        // Validate patrol waypoints
        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
        {
            Debug.LogWarning($"[TitanAI] No patrol waypoints assigned to {gameObject.name}. AI will idle.", this);
        }

        // Initialize components with dependencies
        pathfinder?.Initialize(agent, patrolWaypoints);
        detector?.Initialize(detectionRadius, playerLayer, transform);
        brain?.Initialize(this, pathfinder, detector);
    }

    private void WireEvents()
    {
        if (detector != null)
        {
            detector.OnPlayerEntered += HandlePlayerDetected;
            detector.OnPlayerExited += HandlePlayerLost;
        }
    }

    private void UnwireEvents()
    {
        if (detector != null)
        {
            detector.OnPlayerEntered -= HandlePlayerDetected;
            detector.OnPlayerExited -= HandlePlayerLost;
        }
    }
    #endregion

    #region Event Handlers
    private void HandlePlayerDetected(GameObject player)
    {
        OnPlayerDetected?.Invoke(player);
    }

    private void HandlePlayerLost()
    {
        OnPlayerLost?.Invoke();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Updates the detection radius at runtime and propagates to detector component.
    /// </summary>
    public void SetDetectionRadius(float newRadius)
    {
        if (newRadius <= 0)
        {
            Debug.LogWarning($"[TitanAI] Attempted to set invalid detection radius: {newRadius}. Must be > 0.", this);
            return;
        }

        detectionRadius = newRadius;
        detector?.UpdateRadius(newRadius);
    }

    /// <summary>
    /// Updates patrol speed at runtime.
    /// </summary>
    public void SetPatrolSpeed(float newSpeed)
    {
        patrolSpeed = Mathf.Max(0.1f, newSpeed);
    }

    /// <summary>
    /// Updates chase speed at runtime.
    /// </summary>
    public void SetChaseSpeed(float newSpeed)
    {
        chaseSpeed = Mathf.Max(0.1f, newSpeed);
    }

    /// <summary>
    /// Forces Titan to specific state (for external control/debugging).
    /// </summary>
    public void ForceState(TitanBrain.TitanState state)
    {
        brain?.ForceState(state);
    }

    /// <summary>
    /// Gets current AI state.
    /// </summary>
    public TitanBrain.TitanState GetCurrentState()
    {
        return brain?.CurrentState ?? TitanBrain.TitanState.Idle;
    }
    #endregion
}
