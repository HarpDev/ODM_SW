using UnityEngine;

public class GameobjectRotationFollow : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform bodyTransform;
    [Header("Rotation Settings")]
    public float maxYaw = 60f; // Left-right limit
    public float maxPitch = 40f; // Up-down limit
    public float rotationSpeed = 5f; // How smoothly the head turns
    public float YrotAdditive = 0f; // Normal camera follow offset (vertical/pitch)
    public float XrotAdditive = 0f; // Normal camera follow offset (horizontal/yaw)
    [Header("Invert Settings")]
    public bool invertX = false; // Invert horizontal (yaw) direction
    public bool invertY = false; // Invert vertical (pitch) direction
    [Header("Lock-On Settings")]
    public float LockOnYrotAdditive = 0f; // Offset when locked on VERTICAL
    public float LockOnXrotAdditive = 15f; // Offset when locked on HORIZONTAL
    [Header("Lock-On Override")]
    public bool LockOnOverride = false;
    public Transform LockOnTarget = null;
    private Quaternion _initialLocalRotation;

    void Start()
    {
        _initialLocalRotation = transform.localRotation;
        if (!cameraTransform)
            cameraTransform = Camera.main?.transform;
        if (!cameraTransform)
        {
            Debug.LogError("No camera found! Assign cameraTransform manually.");
            enabled = false;
            return;
        }
        if (!bodyTransform)
            bodyTransform = transform.parent; // Changed to parent; adjust if needed
    }

    void LateUpdate()
    {
        Vector3 lookDirection;
        if (LockOnOverride && LockOnTarget != null)
        {
            lookDirection = (LockOnTarget.position - transform.position).normalized;
        }
        else
        {
            lookDirection = cameraTransform.forward;
        }
        Vector3 localDir = bodyTransform.InverseTransformDirection(lookDirection);
        Vector3 localUp = bodyTransform.InverseTransformDirection(Vector3.up); // Added for consistency
        Quaternion targetLocalRotation = Quaternion.LookRotation(localDir, localUp);
        Vector3 angles = targetLocalRotation.eulerAngles;
        float additiveVert = LockOnOverride ? LockOnYrotAdditive : YrotAdditive;
        float additiveHori = LockOnOverride ? LockOnXrotAdditive : XrotAdditive; // Fixed to use XrotAdditive
        angles.x = NormalizeAngle(angles.x + additiveVert);
        angles.y = NormalizeAngle(angles.y + additiveHori);
        // Apply inversion
        float xMultiplier = invertX ? -1f : 1f;
        float yMultiplier = invertY ? -1f : 1f;
        angles.x *= xMultiplier;
        angles.y *= yMultiplier;
        angles.z = 0f;
        bool withinBounds = Mathf.Abs(angles.x) <= maxPitch && Mathf.Abs(angles.y) <= maxYaw;
        if (withinBounds)
        {
            angles.x = Mathf.Clamp(angles.x, -maxPitch, maxPitch);
            angles.y = Mathf.Clamp(angles.y, -maxYaw, maxYaw);
            targetLocalRotation = Quaternion.Euler(angles);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                _initialLocalRotation * targetLocalRotation,
                Time.deltaTime * rotationSpeed
            );
        }
        else
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                _initialLocalRotation,
                Time.deltaTime * rotationSpeed
            );
        }
    }

    private float NormalizeAngle(float angle)
    {
        angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
        return angle;
    }
}