using UnityEngine;

public class GameobjectRotationFollow : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform bodyTransform;

    [Header("Rotation Settings")]
    public float maxYaw = 60f;          // Left-right limit
    public float maxPitch = 40f;        // Up-down limit
    public float rotationSpeed = 5f;    // How smoothly the head turns
    public float YrotAdditive = 0f;     // Normal camera follow offset
    public float XrotAdditive = 0f;     // Normal camera follow offset

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
            cameraTransform = Camera.main.transform;

        if (!bodyTransform)
            bodyTransform = transform.root;
    }

    void LateUpdate()
    {
        Vector3 lookDirection;

        // Choose which direction to face
        if (LockOnOverride && LockOnTarget != null)
        {
            // Look directly at the lock-on target
            lookDirection = (LockOnTarget.position - transform.position).normalized;
        }
        else
        {
            // Default: follow where the camera is looking
            lookDirection = cameraTransform.forward;
        }

        // Convert direction to local space relative to the body
        Vector3 localDir = bodyTransform.InverseTransformDirection(lookDirection);
        Quaternion targetLocalRotation = Quaternion.LookRotation(localDir, Vector3.up);

        // Apply additive offset depending on state
        Vector3 angles = targetLocalRotation.eulerAngles;
        float additiveVert = LockOnOverride ? LockOnYrotAdditive : YrotAdditive;
        float additiveHori = LockOnOverride ? LockOnXrotAdditive : 0f;

        angles.x = NormalizeAngle(angles.x + additiveVert);
        angles.y = NormalizeAngle(angles.y + additiveHori);
        angles.z = 0f;

        // Determine if rotation is within allowed range
        bool withinBounds = Mathf.Abs(angles.x) <= maxPitch && Mathf.Abs(angles.y) <= maxYaw;

        if (withinBounds)
        {
            // Clamp and smoothly rotate toward target
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
            // Return to neutral if target is out of range
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
