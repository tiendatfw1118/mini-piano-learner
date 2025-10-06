using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NoteView – visual for tap notes. Supports lane-based colors and a soft fade-in/glow.
/// </summary>
public sealed class NoteView : MonoBehaviour
{
    public RectTransform rect;
    public float hitLineY;
    public float glowDistance = 60f;
    public Image image;
    
    [Header("Fade In Effect")]
    public float fadeInDuration = 0.3f; // How long the fade-in takes
    private float fadeInStartTime = -1f; // When fade-in started (-1 = not started)
    private bool isFadingIn = false;
    
    [Header("Lane Colors")]
    public Color baseColor = new Color(0.8f, 0.8f, 0.8f, 0.8f); // Default color
    public Color hitColor = Color.yellow; // Color when hit
    public Color missedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Color when missed

    void Reset(){ rect = GetComponent<RectTransform>(); image = GetComponent<Image>(); }
    
    public void StartFadeIn()
    {
        if (!isFadingIn)
        {
            isFadingIn = true;
            fadeInStartTime = Time.time;
        }
    }
    
    /// <summary>
    /// Set the lane-specific color for this tap note
    /// </summary>
    public void SetLaneColor(Color laneColor)
    {
        // Set the base color
        baseColor = laneColor;
        
        // Generate hit color (brighter version)
        hitColor = new Color(
            Mathf.Min(1f, laneColor.r + 0.3f),
            Mathf.Min(1f, laneColor.g + 0.3f),
            Mathf.Min(1f, laneColor.b + 0.3f),
            laneColor.a
        );
        
        // Generate missed color (darker, more transparent version)
        missedColor = new Color(
            laneColor.r * 0.6f,
            laneColor.g * 0.6f,
            laneColor.b * 0.6f,
            laneColor.a * 0.5f
        );
        
        // Update the visual if image exists
        if (image)
        {
            image.color = baseColor;
        }
    }

    void Update()
    {
        if (!rect || !image) return;
        
        // Handle fade-in effect
        float fadeInAlpha = 1f;
        if (isFadingIn && fadeInStartTime > 0f)
        {
            float fadeProgress = (Time.time - fadeInStartTime) / fadeInDuration;
            fadeInAlpha = Mathf.Clamp01(fadeProgress);
            
            if (fadeProgress >= 1f)
            {
                isFadingIn = false; // Fade-in complete
            }
        }
        
        // Handle glow effect based on distance to hit line
        float dist = Mathf.Abs(rect.anchoredPosition.y - hitLineY);
        float glowAlpha = Mathf.Clamp01(1f - dist / glowDistance);
        
        // Combine fade-in and glow effects
        float finalAlpha = fadeInAlpha * (0.6f + 0.4f * glowAlpha);
        image.color = new Color(baseColor.r, baseColor.g, baseColor.b, finalAlpha);
        
        float s = 0.9f + 0.2f * glowAlpha;
        rect.localScale = Vector3.one * s;
    }
}
