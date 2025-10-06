using UnityEngine;

/// <summary>
/// NoteMovement – positions notes relative to the hit line using beats.
/// For horizontal mode, X would be used; current version moves vertically (Y).
/// </summary>
public class NoteMovement : MonoBehaviour
{
    public RectTransform rect;
    public Conductor conductor;
    public float unitsPerBeat = 220f;
    public double targetBeat;

    [Header("Visual-only tweak")]
    public float visualOffsetMs = 0f;
    
    [Header("BPM Compensation")]
    public bool useBPMCompensation = false; // Intentionally off; visual timing handled elsewhere

    float zeroYInLane;
    public float ZeroYInLane => zeroYInLane;

    public void BindToHitLine(RectTransform hitLine)
    {
        var laneRect = rect ? rect.parent as RectTransform : null;
        if (!laneRect || !hitLine) return;
        var laneCanvas = laneRect.GetComponentInParent<Canvas>();
        var cam = laneCanvas ? laneCanvas.worldCamera : null;
        var hitScreen = RectTransformUtility.WorldToScreenPoint(cam, hitLine.position);
        Vector2 hitLocalInLane;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(laneRect, hitScreen, cam, out hitLocalInLane);
        zeroYInLane = hitLocalInLane.y;
    }

    public bool HasCrossedLine(float eps = 2f) 
    {
        if (!rect) return false;
        
        // Check if note has actually crossed the hit line (not just reached target beat)
        // A note has crossed when it's past the hit line by a small epsilon
        return rect.anchoredPosition.y <= zeroYInLane - eps;
    }

    void Update()
    {
        if (!rect || conductor == null || !gameObject.activeInHierarchy) return;
        
        double B = conductor.SongBeats - (visualOffsetMs / 1000.0) * (conductor.bpm / 60.0);
        float y = zeroYInLane + (float)((targetBeat - B) * unitsPerBeat);
        var p = rect.anchoredPosition; p.y = y; rect.anchoredPosition = p;
    }
}
