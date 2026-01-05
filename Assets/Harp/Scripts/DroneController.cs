using UnityEngine;
public class DroneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask playerLayer;
    [Header("Aiming Settings")]
    [SerializeField] private float rotationSpeed = 5f;
    private void LateUpdate()
    {
        UpdateRotation();
    }
    private void UpdateRotation()
    {
        // Aim towards screen center
        Vector3 aimPoint = GetScreenCenterAimPoint();
        Vector3 aimDirection = (aimPoint - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    private Vector3 GetScreenCenterAimPoint()
    {
        // Raycast from camera through screen center
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, ~playerLayer)) // Ignore player layer
        {
            return hit.point;
        }
        // Fallback to far point in direction
        return ray.GetPoint(100f);
    }
}