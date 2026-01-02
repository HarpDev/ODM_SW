
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class Laser : MonoBehaviour
{
    public Transform laser;
    public TrailRenderer laserTrail;
    public LineRenderer lineRenderer;
    public MeshRenderer meshRenderer;
    public Light plight;
    public Color laserColor;
    public LayerMask mask;

    private void Start()
    {
        updateLaserColor();
    }

 
    public void updateLaserColor()
    {
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;
        laserTrail.startColor = laserColor;
        meshRenderer.material.color = laserColor;
        plight.color = laserColor;
        laserTrail.endColor = laserColor;
    }

    private void LateUpdate()
    {
        lineRenderer.SetPosition(0, transform.position);
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.right, out hit, float.PositiveInfinity, mask))
        {
            lineRenderer.SetPosition(1, hit.point);
            laser.position = hit.point;
            laser.localScale = new Vector3(1, 1, 1);
            plight.enabled = true;
            laserTrail.enabled = true;
            lineRenderer.startColor = new Color(laserColor.r, laserColor.g, laserColor.b, Random.Range(.1f, .5f));
            lineRenderer.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, Random.Range(.1f, .5f));
            laser.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            float distance = Mathf.Clamp(hit.distance / 10, 1, Mathf.Infinity);
            laser.localScale = new Vector3(distance, distance, distance);
            laserTrail.startWidth = distance / 10;
        }
        else
        {
            lineRenderer.SetPosition(1, transform.right * 500);
            laser.localScale = Vector3.zero;
            laser.position = transform.position;
            plight.enabled = false;
            laserTrail.enabled = false;
            laserTrail.startWidth = 0;
        }
    }
}
