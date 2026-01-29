using UnityEngine;

/// <summary>
/// Makes a character's head follow the camera look direction with realistic constraints.
/// Attach this to the head bone of your character.
/// </summary>
public class HeadFollowSystem : MonoBehaviour
{
    [Header("References")]
    public Transform targetToFollow;

    [Tooltip("The character's body transform (for calculating relative angles)")]
    public Transform bodyTransform;

    [Header("Rotation Constraints")]
    [Tooltip("Maximum horizontal rotation in degrees (left/right)")]
    [Range(0f, 90f)]
    public float maxHorizontalAngle = 60f;

    [Tooltip("Maximum upward rotation in degrees")]
    [Range(0f, 90f)]
    public float maxUpwardAngle = 45f;

    [Tooltip("Maximum downward rotation in degrees")]
    [Range(0f, 90f)]
    public float maxDownwardAngle = 60f;

    [Header("Movement Settings")]
    [Tooltip("How smoothly the head follows (lower = smoother but slower)")]
    [Range(0.01f, 1f)]
    public float smoothSpeed = 0.15f;

    [Tooltip("Enable this to make the head movements more natural")]
    public bool useSpringDamping = true;

    [Tooltip("Spring stiffness for natural motion")]
    [Range(1f, 20f)]
    public float springStiffness = 8f;

    [Header("Debug")]
    public bool showDebugGizmos = false;

    // Private variables
    private Quaternion originalLocalRotation;
    private Vector3 currentVelocity;
    private Quaternion targetRotation;
    private Quaternion currentRotation;

    void Start()
    {
        // Store the original local rotation of the head
        originalLocalRotation = transform.localRotation;
        currentRotation = transform.rotation;

        // Auto-find camera if not assigned
        if (targetToFollow == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                targetToFollow = mainCam.transform;
            }
            else
            {
                Debug.LogWarning("HeadFollowSystem: No target assigned and no main camera found!");
            }
        }

        // Auto-find body if not assigned (look for parent with "spine" or "body" in name)
        if (bodyTransform == null)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.name.ToLower().Contains("spine") ||
                    current.name.ToLower().Contains("body") ||
                    current.name.ToLower().Contains("hips"))
                {
                    bodyTransform = current;
                    break;
                }
                current = current.parent;
            }

            if (bodyTransform == null)
            {
                Debug.LogWarning("HeadFollowSystem: Body transform not assigned. Using character root.");
                bodyTransform = transform.root;
            }
        }
    }

    void LateUpdate()
    {
        if (targetToFollow == null || bodyTransform == null)
            return;

        // Calculate the direction to look at
        Vector3 directionToTarget = (targetToFollow.position + targetToFollow.forward * 10f) - transform.position;
        directionToTarget.Normalize();

        // Convert to local space relative to body
        Vector3 localDirection = bodyTransform.InverseTransformDirection(directionToTarget);

        // Calculate angles
        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(localDirection.y) * Mathf.Rad2Deg;

        // Apply constraints
        yaw = Mathf.Clamp(yaw, -maxHorizontalAngle, maxHorizontalAngle);
        pitch = Mathf.Clamp(pitch, -maxDownwardAngle, maxUpwardAngle);

        // Create target rotation
        Quaternion constrainedLocalRotation = Quaternion.Euler(pitch, yaw, 0f);
        targetRotation = bodyTransform.rotation * constrainedLocalRotation * originalLocalRotation;

        // Apply smoothing
        if (useSpringDamping)
        {
            // Spring damping for more natural motion
            currentRotation = SmoothDampQuaternion(currentRotation, targetRotation, ref currentVelocity, smoothSpeed, springStiffness);
        }
        else
        {
            // Simple lerp
            currentRotation = Quaternion.Lerp(currentRotation, targetRotation, smoothSpeed);
        }

        transform.rotation = currentRotation;
    }

    /// <summary>
    /// Smooth damp for quaternions with spring-like motion
    /// </summary>
    private Quaternion SmoothDampQuaternion(Quaternion current, Quaternion target, ref Vector3 velocity, float smoothTime, float stiffness)
    {
        // Convert to euler for damping
        Vector3 currentEuler = current.eulerAngles;
        Vector3 targetEuler = target.eulerAngles;

        // Handle angle wrapping
        for (int i = 0; i < 3; i++)
        {
            float delta = Mathf.DeltaAngle(currentEuler[i], targetEuler[i]);
            targetEuler[i] = currentEuler[i] + delta;
        }

        // Apply smooth damp
        Vector3 result = new Vector3(
            Mathf.SmoothDamp(currentEuler.x, targetEuler.x, ref velocity.x, smoothTime, Mathf.Infinity, Time.deltaTime * stiffness),
            Mathf.SmoothDamp(currentEuler.y, targetEuler.y, ref velocity.y, smoothTime, Mathf.Infinity, Time.deltaTime * stiffness),
            Mathf.SmoothDamp(currentEuler.z, targetEuler.z, ref velocity.z, smoothTime, Mathf.Infinity, Time.deltaTime * stiffness)
        );

        return Quaternion.Euler(result);
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying)
            return;

        // Draw look direction
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        // Draw constraint cone
        if (bodyTransform != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 forward = bodyTransform.forward;
            Vector3 right = bodyTransform.right;
            Vector3 up = bodyTransform.up;

            // Draw horizontal limits
            Vector3 leftLimit = Quaternion.AngleAxis(-maxHorizontalAngle, up) * forward;
            Vector3 rightLimit = Quaternion.AngleAxis(maxHorizontalAngle, up) * forward;
            Gizmos.DrawRay(transform.position, leftLimit * 1.5f);
            Gizmos.DrawRay(transform.position, rightLimit * 1.5f);

            // Draw vertical limits
            Vector3 upLimit = Quaternion.AngleAxis(maxUpwardAngle, right) * forward;
            Vector3 downLimit = Quaternion.AngleAxis(-maxDownwardAngle, right) * forward;
            Gizmos.DrawRay(transform.position, upLimit * 1.5f);
            Gizmos.DrawRay(transform.position, downLimit * 1.5f);
        }
    }

    /// <summary>
    /// Reset the head to its original rotation
    /// </summary>
    public void ResetToOriginalRotation()
    {
        transform.localRotation = originalLocalRotation;
        currentRotation = transform.rotation;
        currentVelocity = Vector3.zero;
    }

    /// <summary>
    /// Temporarily disable head following
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (!enabled)
        {
            ResetToOriginalRotation();
        }
    }
}
