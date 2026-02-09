using UnityEngine;


public class FootstepManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Array of footstep sounds - one will be chosen randomly")]
    public AudioClip[] footstepSounds;
    
    [Tooltip("Volume of footstep sounds (0-1)")]
    [Range(0f, 1f)]
    public float volume = 0.5f;
    
    [Tooltip("Random pitch variation amount")]
    [Range(0f, 0.3f)]
    public float pitchVariation = 0.1f;
    
    [Header("Optional")]
    [Tooltip("If null, will create an AudioSource automatically")]
    public AudioSource audioSource;
    
    private void Start()
    {
        // If no audio source is assigned, create one
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound by default
        }
    }
    
    /// <summary>
    /// Play a random footstep sound. Call this from animation events.
    /// </summary>
    public void PlayFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0)
        {
            Debug.LogWarning("No footstep sounds assigned to " + gameObject.name);
            return;
        }
        
        // Pick a random sound
        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        
        if (clip == null)
        {
            Debug.LogWarning("Null footstep sound in array on " + gameObject.name);
            return;
        }
        
        // Apply random pitch variation
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        
        // Play the sound
        audioSource.PlayOneShot(clip, volume);
    }
    
    /// <summary>
    /// Play footstep with custom volume. Useful for different surfaces.
    /// </summary>
    public void PlayFootstep(float customVolume)
    {
        float originalVolume = volume;
        volume = customVolume;
        PlayFootstep();
        volume = originalVolume;
    }
}