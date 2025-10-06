using UnityEngine;
using UnityEngine.UI;

public class PianoKeysUI : MonoBehaviour
{
    public TapInput input;
    public NoteSpawner spawner;
    public Button[] keys = new Button[7];

    public bool previewOnPress = true;
    public bool sustainPreview = false; // Disabled to prevent audio looping
    public ScaleNoteAudio noteAudio;

    public Color normal = new Color(1,1,1,0.25f);
    public Color highlight = new Color(1,1,1,1f);
    
    [Header("Lane Colors")]
    public Color[] laneColors = new Color[7] 
    {
        new Color(1f, 0.2f, 0.2f, 0.8f), // Red for lane 0
        new Color(1f, 0.6f, 0.2f, 0.8f), // Orange for lane 1
        new Color(1f, 1f, 0.2f, 0.8f),   // Yellow for lane 2
        new Color(0.2f, 1f, 0.2f, 0.8f), // Green for lane 3
        new Color(0.2f, 0.8f, 1f, 0.8f), // Cyan for lane 4
        new Color(0.4f, 0.2f, 1f, 0.8f), // Blue for lane 5
        new Color(1f, 0.2f, 1f, 0.8f)    // Magenta for lane 6
    };
    
    // Generated colors for normal and highlight states
    private Color[] normalColors = new Color[7];
    private Color[] highlightColors = new Color[7];

    public float aheadBeats = 2.0f;
    public float behindBeats = 0.15f;

    void Awake()
    {
        // Initialize lane colors
        InitializeLaneColors();
        
        for (int i = 0; i < keys.Length; i++)
        {
            int degree = i + 1;
            var btn = keys[i];
            if (!btn) continue;

            var hold = btn.gameObject.AddComponent<UIButtonHold>();
            hold.onDown += () =>
            {
                if (sustainPreview && noteAudio) noteAudio.SustainStart(degree);
                input?.DegreeDown(degree);
            };
            hold.onUp += () =>
            {
                if (sustainPreview && noteAudio) noteAudio.SustainStop(degree);
                input?.DegreeUp(degree);
            };

            var img = btn.GetComponent<Image>();
            if (img) img.color = normalColors[i]; // Use lane-specific normal color
        }
    }
    
    /// <summary>
    /// Initialize lane colors for normal and highlight states
    /// </summary>
    void InitializeLaneColors()
    {
        for (int i = 0; i < laneColors.Length && i < 7; i++)
        {
            // Normal color: semi-transparent version of lane color
            normalColors[i] = new Color(
                laneColors[i].r,
                laneColors[i].g,
                laneColors[i].b,
                0.4f // Semi-transparent
            );
            
            // Highlight color: brighter, full opacity version of lane color
            highlightColors[i] = new Color(
                Mathf.Min(1f, laneColors[i].r + 0.3f),
                Mathf.Min(1f, laneColors[i].g + 0.3f),
                Mathf.Min(1f, laneColors[i].b + 0.3f),
                1f // Full opacity
            );
        }
    }
    
    /// <summary>
    /// Sync colors with NoteSpawner lane colors
    /// </summary>
    public void SyncColorsWithSpawner()
    {
        if (spawner && spawner.laneColors != null)
        {
            for (int i = 0; i < Mathf.Min(laneColors.Length, spawner.laneColors.Length); i++)
            {
                laneColors[i] = spawner.laneColors[i];
            }
            InitializeLaneColors();
        }
    }
    
    void Start()
    {
        // Sync colors with NoteSpawner if available
        SyncColorsWithSpawner();
    }

    void Update()
    {
        if (!spawner || spawner.LiveNotes.Count == 0) return;

        double B = spawner.conductor.SongBeats;
        int cand = -1; double best = double.MaxValue;
        foreach (var n in spawner.LiveNotes)
        {
            double dB = n.targetBeat - B;
            if (dB < -behindBeats || dB > aheadBeats) continue;
            var d = n.GetComponent<NoteData>();
            if (!d) continue;
            double ad = System.Math.Abs(dB);
            if (ad < best) { best = ad; cand = Mathf.Clamp(d.degree, 1, 7); }
        }
        for (int i = 0; i < keys.Length; i++)
        {
            var img = keys[i]?.GetComponent<Image>();
            if (!img) continue;
            
            // Use lane-specific colors
            if (i < normalColors.Length && i < highlightColors.Length)
            {
                img.color = (i == cand - 1) ? highlightColors[i] : normalColors[i];
            }
            else
            {
                // Fallback to default colors if lane colors not available
                img.color = (i == cand - 1) ? highlight : normal;
            }
        }
    }
}
