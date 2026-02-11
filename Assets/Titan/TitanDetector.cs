using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles player detection within radius and field of view.
/// Uses coroutine-based detection with configurable intervals for performance.
/// </summary>
public class TitanDetector : MonoBehaviour
{
    #region Serialized Fields
    [Header("Detection Settings")]
    [SerializeField] private float detectionInterval = 0.2f;
    [SerializeField] private float fovAngle = 120f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask obstacleMask = -1; // For line of sight checks
    
    [Header("Debug")]
    [SerializeField] private bool debugVisualization = false;
    #endregion

    #region Properties
    public GameObject CurrentTarget => currentTarget;
    public float DetectionRadius => radius;
    public float FieldOfView => fovAngle;
    #endregion

    #region Private Fields
    private float radius;
    private LayerMask layerMask;
    private Transform titanTransform;
    private GameObject currentTarget;
    private Coroutine detectionCoroutine;
    
    // Reusable arrays to reduce GC allocation
    private readonly Collider[] detectionBuffer = new Collider[16]; // Max players to detect at once
    #endregion

    #region Events
    public event Action<GameObject> OnPlayerEntered;
    public event Action OnPlayerExited;
    #endregion

    #region Initialization
    public void Initialize(float detectRadius, LayerMask playerMask, Transform trans)
    {
        if (trans == null)
        {
            Debug.LogError("[TitanDetector] Cannot initialize with null transform!", this);
            return;
        }

        radius = Mathf.Max(1f, detectRadius);
        layerMask = playerMask;
        titanTransform = trans;

        StartDetection();
    }

    private void OnDestroy()
    {
        StopDetection();
    }

    private void OnDisable()
    {
        StopDetection();
    }

    private void OnEnable()
    {
        if (titanTransform != null)
        {
            StartDetection();
        }
    }
    #endregion

    #region Detection Control
    private void StartDetection()
    {
        if (detectionCoroutine == null)
        {
            detectionCoroutine = StartCoroutine(DetectCoroutine());
        }
    }

    private void StopDetection()
    {
        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
            detectionCoroutine = null;
        }
    }
    #endregion

    #region Detection Logic
    private IEnumerator DetectCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(detectionInterval);

        while (true)
        {
            PerformDetection();
            yield return wait;
        }
    }

    private void PerformDetection()
    {
        if (titanTransform == null)
        {
            return;
        }

        GameObject closestPlayer = FindClosestValidTarget();

        HandleTargetChange(closestPlayer);
    }

    private GameObject FindClosestValidTarget()
    {
        // Use NonAlloc to reduce garbage collection
        int hitCount = Physics.OverlapSphereNonAlloc(
            titanTransform.position,
            radius,
            detectionBuffer,
            layerMask
        );

        if (hitCount == 0)
        {
            return null;
        }

        GameObject closestPlayer = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            GameObject potentialTarget = detectionBuffer[i].gameObject;
            
            // Skip if target is self or inactive
            if (potentialTarget == gameObject || !potentialTarget.activeInHierarchy)
            {
                continue;
            }

            // Check field of view
            if (!IsWithinFieldOfView(potentialTarget.transform))
            {
                continue;
            }

            // Check line of sight if required
            if (requireLineOfSight && !HasLineOfSight(potentialTarget.transform))
            {
                continue;
            }

            // Use squared distance to avoid expensive sqrt operation
            float distanceSqr = (potentialTarget.transform.position - titanTransform.position).sqrMagnitude;
            
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestPlayer = potentialTarget;
            }
        }

        return closestPlayer;
    }

    private bool IsWithinFieldOfView(Transform target)
    {
        Vector3 directionToTarget = (target.position - titanTransform.position).normalized;
        float angleToTarget = Vector3.Angle(titanTransform.forward, directionToTarget);
        
        return angleToTarget <= fovAngle * 0.5f;
    }

    private bool HasLineOfSight(Transform target)
    {
        Vector3 origin = titanTransform.position + Vector3.up * 2f; // Eye level
        Vector3 direction = (target.position - origin).normalized;
        float distance = Vector3.Distance(origin, target.position);

        // Cast ray to check for obstacles
        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, obstacleMask))
        {
            // If we hit the target itself, we have line of sight
            return hit.transform == target;
        }

        // No obstacle hit, clear line of sight
        return true;
    }

    private void HandleTargetChange(GameObject newTarget)
    {
        // New target acquired
        if (newTarget != null && newTarget != currentTarget)
        {
            currentTarget = newTarget;
            OnPlayerEntered?.Invoke(currentTarget);
        }
        // Target lost
        else if (newTarget == null && currentTarget != null)
        {
            currentTarget = null;
            OnPlayerExited?.Invoke();
        }
        // No change - do nothing
    }
    #endregion

    #region Public API
    /// <summary>
    /// Updates the detection radius at runtime.
    /// </summary>
    public void UpdateRadius(float newRadius)
    {
        radius = Mathf.Max(1f, newRadius);
    }

    /// <summary>
    /// Updates the field of view angle at runtime.
    /// </summary>
    public void UpdateFieldOfView(float newFov)
    {
        fovAngle = Mathf.Clamp(newFov, 0f, 360f);
    }

    /// <summary>
    /// Updates detection interval for performance tuning.
    /// </summary>
    public void UpdateDetectionInterval(float newInterval)
    {
        detectionInterval = Mathf.Max(0.05f, newInterval);
        
        // Restart coroutine with new interval
        StopDetection();
        StartDetection();
    }

    /// <summary>
    /// Gets the current target (null if no target).
    /// </summary>
    public GameObject GetCurrentTarget()
    {
        return currentTarget;
    }

    /// <summary>
    /// Manually clears the current target.
    /// </summary>
    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget = null;
            OnPlayerExited?.Invoke();
        }
    }

    /// <summary>
    /// Checks if a specific target is currently detected.
    /// </summary>
    public bool IsTargetDetected(GameObject target)
    {
        return currentTarget == target;
    }
    #endregion

    #region Debug Visualization
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!debugVisualization || titanTransform == null)
        {
            return;
        }

        // Draw detection radius
        Gizmos.color = currentTarget != null ? new Color(1f, 0f, 0f, 0.3f) : new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(titanTransform.position, radius);

        // Draw field of view cone
        DrawFieldOfViewArc();

        // Draw line to current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(titanTransform.position + Vector3.up * 2f, currentTarget.transform.position);
            Gizmos.DrawWireSphere(currentTarget.transform.position, 0.5f);
        }
    }

    private void DrawFieldOfViewArc()
    {
        Vector3 viewAngleA = DirectionFromAngle(-fovAngle * 0.5f, false);
        Vector3 viewAngleB = DirectionFromAngle(fovAngle * 0.5f, false);

        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawLine(titanTransform.position, titanTransform.position + viewAngleA * radius);
        Gizmos.DrawLine(titanTransform.position, titanTransform.position + viewAngleB * radius);

        // Draw arc
        int segments = 20;
        float angleStep = fovAngle / segments;
        Vector3 previousPoint = titanTransform.position + viewAngleA * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = -fovAngle * 0.5f + angleStep * i;
            Vector3 nextPoint = titanTransform.position + DirectionFromAngle(angle, false) * radius;
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

    private Vector3 DirectionFromAngle(float angleInDegrees, bool isGlobal)
    {
        if (!isGlobal)
        {
            angleInDegrees += titanTransform.eulerAngles.y;
        }

        return new Vector3(
            Mathf.Sin(angleInDegrees * Mathf.Deg2Rad),
            0,
            Mathf.Cos(angleInDegrees * Mathf.Deg2Rad)
        );
    }
    #endif
    #endregion
}
