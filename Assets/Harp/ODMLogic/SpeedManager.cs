using Player.Movement;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SpeedManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the PlayerMotor script")]
    public PlayerMotor playerMotor;

    [Tooltip("Reference to the Rigidbody")]
    public Rigidbody rb;

    [Header("UI Elements")]
    [Tooltip("Text element to display speed")]
    public TextMeshProUGUI speedText;

    [Tooltip("Optional image fill for speed gauge")]
    public Image speedGaugeImage;

    [Header("Speed Display Settings")]
    [Tooltip("Speed unit to display (KM/H or M/S)")]
    public SpeedUnit speedUnit = SpeedUnit.KMH;

    [Tooltip("Maximum speed for UI gauge")]
    public float maxSpeedForGauge = 200f;

    [Header("Wind Audio")]
    [Tooltip("Audio source for wind sound")]
    public AudioSource windAudioSource;

    [Tooltip("Wind audio clip to play")]
    public AudioClip windClip;

    [Header("Wind Audio Settings")]
    [Tooltip("Minimum velocity squared magnitude to trigger wind")]
    public float minVelocitySqrMagnitude = 10f;

    [Tooltip("Maximum velocity squared magnitude for max volume")]
    public float maxVelocitySqrMagnitude = 1500f;

    [Tooltip("Minimum volume at threshold")]
    [Range(0f, 1f)]
    public float minWindVolume = 0f;

    [Tooltip("Maximum volume at max speed")]
    [Range(0f, 1f)]
    public float maxWindVolume = 0.6f;

    [Tooltip("Minimum pitch at threshold")]
    [Range(0.5f, 2f)]
    public float minWindPitch = 0.8f;

    [Tooltip("Maximum pitch at max speed")]
    [Range(0.5f, 2f)]
    public float maxWindPitch = 1.5f;

    [Header("Lerp Settings")]
    [Tooltip("Volume lerp speed multiplier")]
    public float volumeLerpSpeed = 8f;

    [Tooltip("Pitch lerp speed multiplier")]
    public float pitchLerpSpeed = 4f;

    [Header("Visual Effects (Future)")]
    [Tooltip("Enable motion blur at high speeds")]
    public bool useMotionBlur = false;

    [Tooltip("Enable speed lines at high speeds")]
    public bool useSpeedLines = false;

    [Tooltip("Enable screen shake at high speeds")]
    public bool useScreenShake = false;

    [Header("Speed Threshold Activation")]
    [Tooltip("GameObjects to activate/deactivate based on speed thresholds (in KM/H)")]
    public SpeedThresholdObject[] speedThresholdObjects;

    [System.Serializable]
    public struct SpeedThresholdObject
    {
        [Tooltip("The GameObject to toggle")]
        public GameObject targetObject;

        [Tooltip("Speed threshold in KM/H (activate at or above, deactivate below)")]
        public float speedThresholdKmh;
    }

    public enum SpeedUnit { KMH, MPS }

    private float currentSpeedKmh;
    private float currentSpeedMps;

    void Start()
    {
        InitializeReferences();
        InitializeAudioSource();
    }

    void InitializeReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogWarning("SpeedManager: Rigidbody not found! Please assign it in the Inspector.");
            }
        }

        if (playerMotor == null)
        {
            playerMotor = GetComponent<PlayerMotor>();
            if (playerMotor == null)
            {
                playerMotor = FindObjectOfType<PlayerMotor>();
            }
        }

        if (windAudioSource == null)
        {
            windAudioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void InitializeAudioSource()
    {
        if (windAudioSource != null)
        {
            windAudioSource.clip = windClip;
            windAudioSource.loop = true;
            windAudioSource.playOnAwake = false;
            windAudioSource.volume = 0f;
            windAudioSource.pitch = 1f;
            windAudioSource.spatialBlend = 0f;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        CalculateSpeed();
        UpdateWindAudio();
        UpdateVisualEffects();
        UpdateThresholdObjects();
    }

    void Update()
    {
        UpdateSpeedUI();
    }

    void CalculateSpeed()
    {
        currentSpeedMps = rb.velocity.magnitude;
        currentSpeedKmh = currentSpeedMps * 3.6f;
        currentSpeedKmh = Mathf.Clamp(currentSpeedKmh, 0, maxSpeedForGauge);
    }

    void UpdateSpeedUI()
    {
        if (speedText != null)
        {
            switch (speedUnit)
            {
                case SpeedUnit.KMH:
                    speedText.text = Mathf.Round(currentSpeedKmh).ToString() + " KM/H";
                    break;
                case SpeedUnit.MPS:
                    speedText.text = Mathf.Round(currentSpeedMps).ToString() + " M/S";
                    break;
            }
        }

        if (speedGaugeImage != null)
        {
            speedGaugeImage.fillAmount = MapToRange(currentSpeedKmh, 0, maxSpeedForGauge, 0, 1);
        }
    }

    void UpdateWindAudio()
    {
        if (windAudioSource == null) return;

        if (rb.velocity.sqrMagnitude <= minVelocitySqrMagnitude)
        {
            windAudioSource.volume = 0;
            windAudioSource.pitch = 1;
            if (windAudioSource.isPlaying && windAudioSource.volume <= 0.01f)
            {
                windAudioSource.Stop();
            }
        }
        else
        {
            if (!windAudioSource.isPlaying)
            {
                windAudioSource.Play();
            }

            float targetVolume = MapToRange(
                rb.velocity.sqrMagnitude,
                minVelocitySqrMagnitude,
                maxVelocitySqrMagnitude,
                minWindVolume,
                maxWindVolume
            );

            float targetPitch = MapToRange(
                rb.velocity.sqrMagnitude,
                minVelocitySqrMagnitude,
                maxVelocitySqrMagnitude,
                minWindPitch,
                maxWindPitch
            );

            windAudioSource.volume = Mathf.Lerp(
                windAudioSource.volume,
                targetVolume,
                Time.fixedDeltaTime * volumeLerpSpeed
            );

            windAudioSource.pitch = Mathf.Lerp(
                windAudioSource.pitch,
                targetPitch,
                Time.fixedDeltaTime * pitchLerpSpeed
            );
        }
    }

    void UpdateVisualEffects()
    {
        if (useMotionBlur)
        {
            // Placeholder for motion blur implementation
        }

        if (useSpeedLines)
        {
            // Placeholder for speed lines implementation
        }

        if (useScreenShake)
        {
            // Placeholder for screen shake implementation
        }
    }

    void UpdateThresholdObjects()
    {
        foreach (var item in speedThresholdObjects)
        {
            if (item.targetObject == null) continue;

            bool shouldBeActive = currentSpeedKmh >= item.speedThresholdKmh;

            if (item.targetObject.activeSelf != shouldBeActive)
            {
                item.targetObject.SetActive(shouldBeActive);
            }
        }
    }

    private float MapToRange(float value, float inMin, float inMax, float outMin, float outMax)
    {
        return Mathf.Clamp((value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin, Mathf.Min(outMin, outMax), Mathf.Max(outMin, outMax));
    }

    public float GetSpeedKMH()
    {
        return currentSpeedKmh;
    }

    public float GetSpeedMPS()
    {
        return currentSpeedMps;
    }

    public bool IsWindPlaying()
    {
        return windAudioSource != null && windAudioSource.isPlaying;
    }

    public float GetCurrentWindVolume()
    {
        return windAudioSource != null ? windAudioSource.volume : 0f;
    }

    public float GetCurrentWindPitch()
    {
        return windAudioSource != null ? windAudioSource.pitch : 1f;
    }
}