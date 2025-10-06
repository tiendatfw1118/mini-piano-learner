using UnityEngine;

/// <summary>
/// Helper script to set up background audio stem
/// Can be attached to any GameObject for easy setup
/// </summary>
public class AudioStemSetup : MonoBehaviour
{
    [Header("Audio Setup")]
    public AudioClip backgroundMusicClip;
    public float volume = 0.5f;
    public bool playOnAwake = false; // Don't play on awake - will start on first note hit
    public bool loop = true;
    
    [Header("Auto-Assign to Boot")]
    public bool autoAssignToBoot = true;
    
    private AudioSource audioSource;
    
    void Start()
    {
        SetupAudioSource();
        
        if (autoAssignToBoot)
        {
            AssignToBoot();
        }
    }
    
    private void SetupAudioSource()
    {
        // Get or create AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configure AudioSource
        audioSource.clip = backgroundMusicClip;
        audioSource.volume = volume;
        audioSource.playOnAwake = playOnAwake;
        audioSource.loop = loop;
        
        // Start playing if configured
        if (playOnAwake && backgroundMusicClip != null)
        {
            audioSource.Play();
        }
    }
    
    private void AssignToBoot()
    {
        // Find Boot component and assign this AudioSource
        var boot = FindFirstObjectByType<Boot>();
        if (boot != null)
        {
            boot.backgroundStem = audioSource;
            // Background stem assigned to Boot
        }
        else
        {
            // Boot component not found - this is expected in some cases
        }
    }
    
    /// <summary>
    /// Manually assign this AudioSource to Boot
    /// </summary>
    [ContextMenu("Assign to Boot")]
    public void ManualAssignToBoot()
    {
        AssignToBoot();
    }
}
