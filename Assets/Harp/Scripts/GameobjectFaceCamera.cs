using UnityEngine;

public class GameobjectFaceCamera : MonoBehaviour
{
    public enum BillboardMode
    {
        LookAtCamera,        // Fully rotate to face the camera
        LookAtCameraYAxis,   // Only rotate around Y (like WoW nameplates)
        InverseLook,         // Face away from the camera (silhouette effects)
        CopyCameraForward,   // Match camera forward direction
        CopyCameraRotation   // Exact camera rotation (world-space UI)
    }

    [Header("References")]
    public Camera targetCamera;

    [Header("Settings")]
    public BillboardMode mode = BillboardMode.LookAtCameraYAxis;
    public bool freezeX = false;
    public bool freezeY = false;
    public bool freezeZ = false;

    [Tooltip("Rotate smoothly instead of snapping instantly.")]
    public bool useSmoothing = false;

    [Tooltip("How quickly the object rotates when smoothing is enabled.")]
    public float rotationSpeed = 10f;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        Quaternion targetRotation = transform.rotation;

        Vector3 cameraForward = targetCamera.transform.forward;
        Vector3 cameraUp = targetCamera.transform.up;
        Vector3 cameraPos = targetCamera.transform.position;

        switch (mode)
        {
            case BillboardMode.LookAtCamera:
                targetRotation = Quaternion.LookRotation(transform.position - cameraPos);
                break;

            case BillboardMode.LookAtCameraYAxis:
                Vector3 flatPos = new Vector3(cameraPos.x, transform.position.y, cameraPos.z);
                targetRotation = Quaternion.LookRotation(transform.position - flatPos);
                break;

            case BillboardMode.InverseLook:
                targetRotation = Quaternion.LookRotation(cameraPos - transform.position);
                break;

            case BillboardMode.CopyCameraForward:
                targetRotation = Quaternion.LookRotation(cameraForward, cameraUp);
                break;

            case BillboardMode.CopyCameraRotation:
                targetRotation = targetCamera.transform.rotation;
                break;
        }

        // Apply axis freezes
        if (freezeX || freezeY || freezeZ)
        {
            Vector3 euler = targetRotation.eulerAngles;
            Vector3 current = transform.rotation.eulerAngles;

            if (freezeX) euler.x = current.x;
            if (freezeY) euler.y = current.y;
            if (freezeZ) euler.z = current.z;

            targetRotation = Quaternion.Euler(euler);
        }

        // Apply smoothing
        if (useSmoothing)
        {
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }
}
