using UnityEngine;

public sealed class BpmHud : MonoBehaviour
{
    public Conductor conductor;
    public TempoController tempo;
    public TMPro.TMP_Text bpmText;
    public TMPro.TMP_Text bannerText;

    void OnEnable(){ if (tempo) tempo.OnBpmChanged += HandleBpmChanged; }
    void OnDisable(){ if (tempo) tempo.OnBpmChanged -= HandleBpmChanged; }
    void Update(){ if (conductor && bpmText) bpmText.text = $"BPM: {(int)conductor.bpm}"; }
    void HandleBpmChanged(float newBpm, int delta)
    {
        if (!bannerText) return;
        bannerText.text = delta > 0 ? "+10 BPM" : "-10 BPM";
        bannerText.gameObject.SetActive(true);
        CancelInvoke(nameof(Hide)); Invoke(nameof(Hide), 0.6f);
    }
    void Hide(){ if (bannerText) bannerText.gameObject.SetActive(false); }
}
