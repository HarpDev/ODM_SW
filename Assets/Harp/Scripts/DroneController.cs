using UnityEngine;

public class DroneController : MonoBehaviour
{
    public enum FlankSide { Left, Right, Oscillate }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody playerRb;

    [Header("Flank Settings")]
    [SerializeField] private float radius = 2.0f; // Distance from player
    [SerializeField] private float heightOffset = 1.2f;
    [SerializeField] private float orbitSpeed = 120f; // Oscillation speed (deg/sec) for Oscillate mode
    [SerializeField] private float phaseOffset = 0f; // Starting phase for multi-drones
    [SerializeField] private FlankSide flankSide = FlankSide.Right; // Left/Right: fixed flank | Oscillate: patrol left-right behind
    [Range(0f, 180f)]
    [SerializeField] private float maxFlankAngle = 90f; // 90= pure side, <90= more behind

    [Header("Smoothness & Prediction")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float predictionTime = 0.15f;
    [SerializeField] private float followSpeed = 50f; // High for tight following

    [Header("Avoidance Settings")]
    [SerializeField] private LayerMask obstacleLayer; // Layers for obstacles to avoid
    [SerializeField] private float avoidanceRadius = 2f; // Detection radius for obstacles
    [SerializeField] private float avoidanceStrength = 5f; // Strength of repulsion force

    [Header("Aiming Settings")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Controls")]
    [SerializeField] private bool enableSideSwitching = false; // Toggle to enable Q/E key switching

    private Vector3 targetPosition;
    private float currentAngle;

    private void Start()
    {
       
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        currentAngle = phaseOffset;
    }

    private void Update()
    {
        if (enableSideSwitching)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                flankSide = FlankSide.Left;
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                flankSide = FlankSide.Right;
            }
        }
    }

    private void LateUpdate()
    {
        UpdatePosition();
        UpdateRotation();
    }

    private void UpdatePosition()
    {
        if (player == null || mainCamera == null) return;

        // Predict position for fast Rigidbody movement
        Vector3 predictedPos = player.position;
        if (playerRb != null)
        {
            predictedPos += playerRb.velocity * predictionTime;
        }

        // Use camera facing direction for flanking (fixed to camera look dir, not player input/velocity)
        Vector3 facingDir = mainCamera.transform.forward.normalized;
        Vector3 behindDir = -facingDir;

        // Calculate relative angle theta (locked for Left/Right to always target exact flank point)
        float theta;
        switch (flankSide)
        {
            case FlankSide.Left:
                theta = maxFlankAngle;
                break;
            case FlankSide.Right:
                theta = -maxFlankAngle;
                break;
            default: // Oscillate
                currentAngle += orbitSpeed * Time.deltaTime;
                currentAngle = Mathf.Repeat(currentAngle, 360f);
                theta = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * maxFlankAngle;
                break;
        }

        // Local offset rotated by theta from behind (constant radius circle arc)
        Vector3 localOffset = Quaternion.Euler(0f, theta, 0f) * Vector3.forward * radius;

        // Transform to world space aligned to behindDir
        Quaternion worldRot = Quaternion.LookRotation(behindDir, Vector3.up);
        Vector3 offset = worldRot * localOffset;
        offset += Vector3.up * heightOffset;

        // Ideal target = predicted + offset (drone always aims for exact flank point)
        targetPosition = predictedPos + offset;

        // Calculate avoidance repulsion from nearby obstacles
        Vector3 avoidance = Vector3.zero;
        Collider[] hits = Physics.OverlapSphere(transform.position, avoidanceRadius, obstacleLayer);
        foreach (Collider hit in hits)
        {
            Vector3 closest = hit.ClosestPoint(transform.position);
            float dist = Vector3.Distance(transform.position, closest);
            if (dist > 0.001f && dist < avoidanceRadius)
            {
                Vector3 repelDir = (transform.position - closest).normalized;
                avoidance += repelDir * (avoidanceStrength * (avoidanceRadius - dist) / avoidanceRadius);
            }
        }

        // Apply avoidance nudge first (push away from obstacles)
        transform.position += avoidance * Time.deltaTime;

        // Then smooth lerp to target (high speed for tight adherence)
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
    }

    private void UpdateRotation()
    {
        Vector3 aimPoint = GetScreenCenterAimPoint();
        Vector3 aimDirection = (aimPoint - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private Vector3 GetScreenCenterAimPoint()
    {
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, ~playerLayer))
        {
            return hit.point;
        }
        return ray.GetPoint(100f);
    }

    private void OnDrawGizmos()
    {
        if (player == null || mainCamera == null) return;

        Vector3 center = player.position + Vector3.up * heightOffset;

        // Draw the orbit circle (XZ plane)
        Gizmos.color = Color.white;
        const int segments = 64; // Smooth circle
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * 360f * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        // Use camera facing for flank points (not player input/velocity)
        Vector3 facingDir = mainCamera.transform.forward.normalized;
        Vector3 behindDir = -facingDir;
        Quaternion worldRot = Quaternion.LookRotation(behindDir, Vector3.up);

        // Left flank point (theta = maxFlankAngle)
        Vector3 localLeft = Quaternion.Euler(0f, maxFlankAngle, 0f) * Vector3.forward * radius;
        Vector3 offsetLeft = worldRot * localLeft + Vector3.up * heightOffset;
        Vector3 leftPos = player.position + offsetLeft;

        // Right flank point (theta = -maxFlankAngle)
        Vector3 localRight = Quaternion.Euler(0f, -maxFlankAngle, 0f) * Vector3.forward * radius;
        Vector3 offsetRight = worldRot * localRight + Vector3.up * heightOffset;
        Vector3 rightPos = player.position + offsetRight;

        // Draw flank points
        Gizmos.color = Color.green; // Left
        Gizmos.DrawSphere(leftPos, 0.2f);
        Gizmos.color = Color.red; // Right
        Gizmos.DrawSphere(rightPos, 0.2f);
    }
}