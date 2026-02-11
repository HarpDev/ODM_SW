using System;
using UnityEngine;

/// <summary>
/// Finite State Machine controller for Titan AI behavior.
/// Manages state transitions and delegates actions to appropriate components.
/// </summary>
public class TitanBrain : MonoBehaviour
{
    #region State Definition
    public enum TitanState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        Attack
    }
    #endregion

    #region Serialized Fields
    [Header("State Timing")]
    [SerializeField] private float idleDuration = 5f;
    [SerializeField] private float alertDuration = 10f;
    [SerializeField] private float attackRange = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugStateChanges = false;
    #endregion

    #region Properties
    public TitanState CurrentState => currentState;
    public GameObject TargetPlayer => targetPlayer;
    #endregion

    #region Private Fields
    private TitanAI ai;
    private TitanPathfinder pathfinder;
    private TitanDetector detector;
    
    private TitanState currentState = TitanState.Idle;
    private GameObject targetPlayer;
    private float stateTimer;
    private Vector3 lastKnownTargetPosition;
    #endregion

    #region Initialization
    public void Initialize(TitanAI titanAI, TitanPathfinder pf, TitanDetector det)
    {
        if (titanAI == null || pf == null || det == null)
        {
            Debug.LogError("[TitanBrain] Cannot initialize with null components!", this);
            return;
        }

        ai = titanAI;
        pathfinder = pf;
        detector = det;

        SubscribeToEvents();
        TransitionToState(TitanState.Idle);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (ai != null)
        {
            ai.OnPlayerDetected += HandlePlayerDetected;
            ai.OnPlayerLost += HandlePlayerLost;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (ai != null)
        {
            ai.OnPlayerDetected -= HandlePlayerDetected;
            ai.OnPlayerLost -= HandlePlayerLost;
        }
    }
    #endregion

    #region Event Handlers
    private void HandlePlayerDetected(GameObject player)
    {
        targetPlayer = player;
        TransitionToState(TitanState.Chase);
    }

    private void HandlePlayerLost()
    {
        TransitionToState(TitanState.Alert);
    }
    #endregion

    #region State Machine Update
    public void UpdateBrain()
    {
        if (ai == null || pathfinder == null || detector == null)
        {
            return;
        }

        UpdateStateTimer();
        ExecuteCurrentState();
    }

    private void UpdateStateTimer()
    {
        if (stateTimer > 0f)
        {
            stateTimer -= Time.deltaTime;
        }
    }

    private void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case TitanState.Idle:
                UpdateIdleState();
                break;
                
            case TitanState.Patrol:
                UpdatePatrolState();
                break;
                
            case TitanState.Alert:
                UpdateAlertState();
                break;
                
            case TitanState.Chase:
                UpdateChaseState();
                break;
                
            case TitanState.Attack:
                UpdateAttackState();
                break;
        }
    }
    #endregion

    #region State Updates
    private void UpdateIdleState()
    {
        if (stateTimer <= 0f)
        {
            TransitionToState(TitanState.Patrol);
        }
    }

    private void UpdatePatrolState()
    {
        if (pathfinder.IsDestinationReached())
        {
            pathfinder.Patrol();
        }
    }

    private void UpdateAlertState()
    {
        // Check if player reacquired during alert
        GameObject currentTarget = detector.GetCurrentTarget();
        if (currentTarget != null)
        {
            targetPlayer = currentTarget;
            TransitionToState(TitanState.Chase);
            return;
        }

        // Return to patrol after alert timer expires
        if (stateTimer <= 0f)
        {
            TransitionToState(TitanState.Patrol);
        }
    }

    private void UpdateChaseState()
    {
        if (targetPlayer == null)
        {
            TransitionToState(TitanState.Alert);
            return;
        }

        // Update last known position
        lastKnownTargetPosition = targetPlayer.transform.position;
        
        // Chase the target
        pathfinder.Chase(lastKnownTargetPosition);

        // Check if in attack range
        float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.transform.position);
        if (distanceToTarget <= attackRange)
        {
            TransitionToState(TitanState.Attack);
        }
    }

    private void UpdateAttackState()
    {
        if (targetPlayer == null)
        {
            TransitionToState(TitanState.Alert);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.transform.position);
        
        // If target moves out of attack range, resume chase
        if (distanceToTarget > attackRange)
        {
            TransitionToState(TitanState.Chase);
            return;
        }

        // Face the target while attacking
        Vector3 lookDirection = (targetPlayer.transform.position - transform.position).normalized;
        lookDirection.y = 0f; // Keep rotation on horizontal plane
        
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        // TODO: Trigger attack animation/logic here
        // Example: animator.SetTrigger("Attack");
    }
    #endregion

    #region State Transitions
    private void TransitionToState(TitanState newState)
    {
        if (newState == currentState)
        {
            return;
        }

        // Exit current state
        ExitState(currentState);

        TitanState previousState = currentState;
        currentState = newState;

        // Enter new state
        EnterState(newState);

        // Debug logging
        if (debugStateChanges)
        {
            Debug.Log($"[TitanBrain] {gameObject.name}: {previousState} → {newState}", this);
        }
    }

    private void ExitState(TitanState state)
    {
        // Cleanup logic for exiting states
        switch (state)
        {
            case TitanState.Attack:
                // Stop attack animations/effects
                break;
        }
    }

    private void EnterState(TitanState state)
    {
        // Setup logic for entering states
        switch (state)
        {
            case TitanState.Idle:
                EnterIdleState();
                break;
                
            case TitanState.Patrol:
                EnterPatrolState();
                break;
                
            case TitanState.Alert:
                EnterAlertState();
                break;
                
            case TitanState.Chase:
                EnterChaseState();
                break;
                
            case TitanState.Attack:
                EnterAttackState();
                break;
        }
    }

    private void EnterIdleState()
    {
        pathfinder.Stop();
        stateTimer = idleDuration;
    }

    private void EnterPatrolState()
    {
        if (ai.Agent != null)
        {
            ai.Agent.speed = ai.PatrolSpeed;
        }
        pathfinder.Patrol();
    }

    private void EnterAlertState()
    {
        stateTimer = alertDuration;
        
        if (lastKnownTargetPosition != Vector3.zero)
        {
            pathfinder.SearchLastKnown();
        }
        else
        {
            // If no last known position, just slow down
            if (ai.Agent != null)
            {
                ai.Agent.speed = ai.PatrolSpeed;
            }
        }
    }

    private void EnterChaseState()
    {
        if (ai.Agent != null)
        {
            ai.Agent.speed = ai.ChaseSpeed;
        }
    }

    private void EnterAttackState()
    {
        pathfinder.Stop();
        // TODO: Trigger attack animation
        // Example: animator.SetTrigger("Attack");
    }
    #endregion

    #region Public API
    /// <summary>
    /// Forces a state transition (useful for external control or debugging).
    /// </summary>
    public void ForceState(TitanState state)
    {
        TransitionToState(state);
    }

    /// <summary>
    /// Clears current target and returns to patrol.
    /// </summary>
    public void ResetBehavior()
    {
        targetPlayer = null;
        lastKnownTargetPosition = Vector3.zero;
        TransitionToState(TitanState.Patrol);
    }
    #endregion

    #region Debug Visualization
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!debugStateChanges) return;

        // Draw state indicator
        Color stateColor = GetStateColor();
        Gizmos.color = stateColor;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 12f, 1f);

        // Draw line to target if chasing/attacking
        if ((currentState == TitanState.Chase || currentState == TitanState.Attack) && targetPlayer != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 5f, targetPlayer.transform.position + Vector3.up * 1f);
        }

        // Draw attack range
        if (currentState == TitanState.Attack)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }

    private Color GetStateColor()
    {
        switch (currentState)
        {
            case TitanState.Idle: return Color.white;
            case TitanState.Patrol: return Color.cyan;
            case TitanState.Alert: return Color.yellow;
            case TitanState.Chase: return Color.green;
            case TitanState.Attack: return Color.red;
            default: return Color.gray;
        }
    }
    #endif
    #endregion
}
