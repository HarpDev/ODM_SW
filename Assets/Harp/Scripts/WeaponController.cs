using SwiftKraft.Gameplay.Projectiles;
using System.Collections;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [Header("References")]
    public WeaponData weaponData;
    public Transform muzzlePoint;
    public Transform cameraReference;
    public AudioSource audioSource;

    [Header("Weapon Spread")]
    public Rigidbody playerRb;
    [Tooltip("Standing still accuracy (in degrees).")]
    public float baseSpreadDegrees = 0f;

    [Tooltip("Movement speed multiplies this value to increase spread.")]
    public float movementSpreadMultiplier = 0.2f;

    [Tooltip("Maximum spread allowed (in degrees).")]
    public float maxSpreadDegrees = 8f;

    private float _currentSpreadDegrees;
    private float _playerSpeed;

    [SerializeField]
    private float nextFireTime = 0f;
    private bool isReloading = false;

    private int currentMagazine;
    private int totalAmmo;

    public float GetFireIntervalPercent() => weaponData.fireInterval;

    private void Start()
    {
        currentMagazine = weaponData.magazineCapacity;
        totalAmmo = weaponData.magazineMaxCount - currentMagazine;
    }

    // ---------------------------------------------------------
    // SPREAD & PLAYER MOVEMENT
    // ---------------------------------------------------------
    public void CalculatePlayerMagnitude()
    {
        if (playerRb == null) return;

        _playerSpeed = playerRb.velocity.magnitude;

        float movementSpread = _playerSpeed * movementSpreadMultiplier;
        _currentSpreadDegrees = Mathf.Clamp(
            baseSpreadDegrees + movementSpread,
            0f,
            maxSpreadDegrees
        );
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
            TryFire();

        if (Input.GetKeyDown(KeyCode.R))
            TryReload();

        CalculatePlayerMagnitude();
    }

    // ---------------------------------------------------------
    // FIRING SEQUENCE
    // ---------------------------------------------------------
    private void TryFire()
    {
        if (isReloading)
            return;

        if (Time.time < nextFireTime)
            return;

        if (currentMagazine < weaponData.magazineDrainPerShot)
        {
            TryReload();
            return;
        }

        StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        nextFireTime = Time.time + weaponData.fireInterval;

        int rounds = weaponData.roundsPerShot;

        for (int i = 0; i < rounds; i++)
        {
            FireSingleProjectile();

            if (i < rounds - 1)
                yield return new WaitForSeconds(weaponData.burstInterval);
        }
    }

    // ---------------------------------------------------------
    // PROJECTILE FIRE
    // ---------------------------------------------------------
    private void FireSingleProjectile()
    {
        currentMagazine -= weaponData.magazineDrainPerShot;

        if (weaponData.fireSound != null)
            audioSource.PlayOneShot(weaponData.fireSound);

        if (weaponData.muzzleFlashPrefab != null)
        {
            GameObject fx = Instantiate(
                weaponData.muzzleFlashPrefab,
                muzzlePoint.position,
                muzzlePoint.rotation
            );
            Destroy(fx, 1f);
        }

        // Compute spread direction
        Vector3 spreadDirection = GetSpreadDirection();

        // Instantiate projectile
        GameObject projObj = Instantiate(
            weaponData.projectilePrefab,
            muzzlePoint.position,
            Quaternion.identity
        );

        ProjectileHitscan projectile = projObj.GetComponent<ProjectileHitscan>();
        if (projectile == null)
        {
            Debug.LogWarning("Projectile prefab must contain a ProjectileHitscan component.");
            return;
        }

        // Apply firing direction with spread
        projObj.transform.forward = spreadDirection;

        // Hitscan immediate cast
        RaycastHit[] hits = projectile.Cast();
        projectile.Hit(hits);

        Destroy(projObj, 0.01f);
    }

    // ---------------------------------------------------------
    // RELOAD
    // ---------------------------------------------------------
    private void TryReload()
    {
        if (isReloading)
            return;

        if (currentMagazine == weaponData.magazineCapacity)
            return;

        if (totalAmmo <= 0)
            return;

        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        if (weaponData.reloadSound != null)
            audioSource.PlayOneShot(weaponData.reloadSound);

        yield return new WaitForSeconds(weaponData.reloadTime);

        int needed = weaponData.magazineCapacity - currentMagazine;
        int loaded = Mathf.Min(needed, totalAmmo);

        currentMagazine += loaded;
        totalAmmo -= loaded;

        isReloading = false;
    }

    // ---------------------------------------------------------
    // SPREAD FUNCTION 
    // ---------------------------------------------------------
    private Vector3 PickFiringDirection(Vector3 muzzleForward, float spreadRadius)
    {
        Vector3 candidate = Random.insideUnitSphere * spreadRadius + muzzleForward;
        return candidate.normalized;
    }

    private Vector3 GetSpreadDirection()
    {
        float spreadRadius = Mathf.Tan(_currentSpreadDegrees * Mathf.Deg2Rad);
        return PickFiringDirection(muzzlePoint.forward, spreadRadius);
    }

    // ---------------------------------------------------------
    // GIZMOS 
    // ---------------------------------------------------------
    private void OnDrawGizmos()
    {
        if (!muzzlePoint) return;

        Gizmos.color = Color.yellow;

        float spreadDeg = Application.isPlaying ? _currentSpreadDegrees : maxSpreadDegrees;
        float spreadRadius = Mathf.Tan(spreadDeg * Mathf.Deg2Rad);

        // Visualize the cone distribution
        for (int i = 0; i < 32; i++)
        {
            Vector3 dir = PickFiringDirection(muzzlePoint.forward, spreadRadius);
            Gizmos.DrawRay(muzzlePoint.position, dir * 5f);
        }
    }
}
