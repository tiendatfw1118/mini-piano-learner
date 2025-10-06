using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HoldNoteView – renders hold notes as a single uniform bar, applies lane colors,
/// handles held/disabled states, and displays the note name centered in the visible bar.
/// </summary>
public class HoldNoteView : MonoBehaviour
{
    public RectTransform rect;
    public Image mainImage;      // The original note image (hidden for uniform bar)
    public Image bodyImage;      // The uniform hold bar (entire hold note)
    public float hitLineY;
    public float glowDistance = 60f;
    
    [Header("Visual Settings")]
    public Color headColor = Color.white; // Unused for uniform bar
    public Color bodyColor = new Color(0f, 0.8f, 1f, 0.7f); // Semi-transparent cyan
    public Color heldColor = Color.yellow;
    public Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Grey for disabled notes
    public float barWidth = 80f; // Width of the uniform bar
    
    [Header("Note Text")]
    public Text noteText; // Text component for displaying note names
    public Color textColor = Color.white;
    public int fontSize = 24;
    
    private NoteData noteData;
    private bool isHeld = false;
    private bool isDisabled = false;
    private bool visualsSetup = false;
    
    void Start()
    {
        noteData = GetComponent<NoteData>();
        if (!noteData || !noteData.IsHoldNote)
        {
            return;
        }
        
        SetupHoldNoteVisuals();
    }
    
    void SetupHoldNoteVisuals()
    {
        if (visualsSetup) return;
        
        // Get the main image component (the original note image)
        mainImage = GetComponent<Image>();
        if (!mainImage)
        {
            return;
        }
        
        // For uniform bar appearance, we'll create a single bar that extends from head to tail
        // The top of the bar will align with the top of the note (head position)
        mainImage.color = bodyColor;
        
        // Hide the original note image since we'll make the main rect the entire bar
        mainImage.enabled = false;
        
        // Create a single uniform bar image
        if (!bodyImage)
        {
            var bodyGO = new GameObject("HoldBar");
            bodyGO.transform.SetParent(transform, false);
            bodyImage = bodyGO.AddComponent<Image>();
            bodyImage.color = bodyColor;
            
            var bodyRect = bodyImage.rectTransform;
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.sizeDelta = new Vector2(barWidth, 100f); // Start with some height for visibility
            bodyRect.anchoredPosition = new Vector2(0f, 0f);
            
            // Ensure the bar image is visible
            bodyImage.enabled = true;
        }
        
        // Create text component for note name
        if (!noteText)
        {
            var textGO = new GameObject("NoteText");
            textGO.transform.SetParent(transform, false);
            noteText = textGO.AddComponent<Text>();
            
            // Set up text component
            noteText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            noteText.fontSize = fontSize;
            noteText.color = textColor;
            noteText.alignment = TextAnchor.MiddleCenter; // Center-aligned text
            noteText.text = "C"; // Default text
            
            // Add outline for better contrast
            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = true;
            
            // Position text in the middle of the hold bar
            var textRect = noteText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(barWidth - 10f, 30f); // Slightly smaller than bar width
            textRect.anchoredPosition = new Vector2(0f, 0f); // Center in the middle of the bar
            
            // Ensure text is visible
            noteText.enabled = true;
        }
        
        visualsSetup = true;
    }
    
    void Update()
    {
        if (!noteData || !noteData.IsHoldNote || !visualsSetup) return;
        
        // Update note name from NoteData if it has changed
        if (noteText && noteData.noteName != noteText.text)
        {
            noteText.text = noteData.noteName;
        }
        
        UpdateHoldNoteVisuals();
        UpdateGlowEffect();
        
    }
    
    void UpdateHoldNoteVisuals()
    {
        if (!rect || !bodyImage) return;
        
        // Calculate the length of the hold note in pixels
        var conductor = FindFirstObjectByType<Conductor>();
        if (!conductor) return;
        
        float unitsPerBeat = 220f; // Should match NoteSpawner
        float holdLengthPixels = (float)(noteData.durationBeats * unitsPerBeat);
        
        // Update the uniform bar size - same width throughout
        var bodyRect = bodyImage.rectTransform;
        bodyRect.sizeDelta = new Vector2(barWidth, holdLengthPixels);
        
        // Position the bar so the top of the bar aligns with the top of the note (head)
        // The bar extends downward from the head to the tail
        // Since the bar's pivot is at center, we offset it by half the height downward
        float yOffset = -holdLengthPixels * 0.5f;
        bodyRect.anchoredPosition = new Vector2(0f, yOffset);
        
        // Update text position to be centered within the visible hold bar
        if (noteText)
        {
            noteText.rectTransform.anchoredPosition = new Vector2(0f, yOffset);
        }
        
        // Update colors based on hold state - uniform color for entire bar
        if (isDisabled)
        {
            bodyImage.color = disabledColor;
        }
        else if (noteData.isBeingHeld)
        {
            bodyImage.color = heldColor;
        }
        else
        {
            bodyImage.color = bodyColor;
        }
    }
    
    void UpdateGlowEffect()
    {
        if (!bodyImage) return;
        
        // Calculate distance to hit line
        float dist = Mathf.Abs(rect.anchoredPosition.y - hitLineY);
        float glowAlpha = Mathf.Clamp01(1f - dist / glowDistance);
        
        // Apply glow effect to the uniform bar
        Color barColorWithGlow = bodyImage.color;
        barColorWithGlow.a = 0.6f + 0.4f * glowAlpha;
        bodyImage.color = barColorWithGlow;
        
        // Scale effect for the entire bar
        float scale = 0.9f + 0.2f * glowAlpha;
        bodyImage.rectTransform.localScale = Vector3.one * scale;
    }
    
    public void SetHeld(bool held)
    {
        isHeld = held;
        if (noteData) noteData.isBeingHeld = held;
    }
    
    public void SetDisabled(bool disabled)
    {
        isDisabled = disabled;
    }
    
    /// <summary>
    /// Set the lane-specific color for this hold note
    /// </summary>
    public void SetLaneColor(Color laneColor)
    {
        // Set the base body color
        bodyColor = laneColor;
        
        // Generate held color (brighter version)
        heldColor = new Color(
            Mathf.Min(1f, laneColor.r + 0.5f),
            Mathf.Min(1f, laneColor.g + 0.5f),
            Mathf.Min(1f, laneColor.b + 0.5f),
            laneColor.a
        );
        
        // Generate disabled color (darker, more transparent version)
        disabledColor = new Color(
            laneColor.r * 0.6f,
            laneColor.g * 0.6f,
            laneColor.b * 0.6f,
            laneColor.a * 0.5f
        );
        
        // Update text color to have high contrast against the lane color
        // Use white text with a dark outline for maximum readability
        textColor = Color.white; // Always use white text for maximum contrast
        
        // Update the visual if already set up
        if (visualsSetup && bodyImage)
        {
            bodyImage.color = bodyColor;
        }
        
        // Update text color if text component exists
        if (noteText)
        {
            noteText.color = textColor;
        }
    }
    
    /// <summary>
    /// Set the note name text
    /// </summary>
    public void SetNoteName(string noteName)
    {
        if (noteText)
        {
            noteText.text = noteName;
        }
    }
}
