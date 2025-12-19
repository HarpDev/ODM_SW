using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    

    [Header("Projectile / Shooting")]
    public GameObject projectilePrefab;
    public AudioClip fireSound;
    public GameObject muzzleFlashPrefab;
   
    public float fireInterval = 0.2f;
    public float burstInterval = 0.05f;//delay between shots in a burst

    [Header("Shot Pattern")]
    public int roundsPerShot = 1;
  

    [Header("Damage")]
    public float damageMultiplier = 1f;
    public float armorPenetration = 0f;
    
    [Header("Magazine / Reloading")]
    public AudioClip reloadSound;
    public int magazineMaxCount = 0;       // total ammo the weapon can hold
    public int magazineCapacity = 0;       // max rounds per magazine
    public int magazineDrainPerShot = 1;   // how many rounds are used per shot

    public int magazineRegenAmount = 0;
    public float magazineRegenRate = 0f;

    public float reloadTime = 2f;

  
}
