using UnityEngine;
using System.Collections;

public sealed class Conductor : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource music;
    public float firstBeatOffsetSec = 0f;

    [Header("Tempo")]
    [Range(1f, 300f)]
    public float bpm = 120f;

    [HideInInspector] public double dspStartTime; // giữ cho tương thích (beat 0 mốc ban đầu)

    // ---- Anchor/Pivot để đảm bảo liên tục ----
    private double anchorBeat = 0.0;  // beat tại thời điểm neo gần nhất
    private double anchorDsp = 0.0;  // dspTime tại thời điểm neo gần nhất
    private bool hasStarted = false;

    public event System.Action<double> OnTempoChanged;

    public double SecPerBeat => 60.0 / Mathf.Max(1f, bpm);

    /// <summary> Beat hiện tại, liên tục theo thời gian DSP, không “nhảy” khi đổi BPM. </summary>
    public double SongBeats
    {
        get
        {
            if (!hasStarted) return 0.0;
            double t = AudioSettings.dspTime - anchorDsp;
            return anchorBeat + t * (bpm / 60.0);
        }
    }

    /// <summary> Thời gian (giây) đã trôi tính từ beat 0 (sau offset). </summary>
    public double SongTimeSec => hasStarted ? (AudioSettings.dspTime - anchorDsp) : 0.0;

    /// <summary> Thời điểm DSP mà 'beat' sẽ xảy ra (tính theo anchor hiện tại & BPM hiện tại). </summary>
    public double DspAtBeat(double beat)
    {
        // Không dùng dspStartTime + beat*SecPerBeat nữa, vì như vậy sẽ “gãy” khi đổi BPM.
        return anchorDsp + (beat - anchorBeat) * SecPerBeat;
    }

    void Start()
    {
        // Không tự start — gọi StartConductor() từ Boot/Loader.
    }

    public void StartConductor()
    {
        if (hasStarted) return;

        double now = AudioSettings.dspTime;
        double scheduleAt = now + 0.10; // 100 ms để đảm bảo lịch phát an toàn

        // mốc beat 0 sẽ là: time = (scheduleAt + firstBeatOffsetSec)
        anchorBeat = 0.0;
        anchorDsp = scheduleAt + firstBeatOffsetSec;

        // giữ lại để tương thích (nếu nơi khác còn dùng biến này)
        dspStartTime = anchorDsp;

        if (music && music.clip)
        {
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.PlayScheduled(scheduleAt);
        }
        else
        {
            // Không có audio: vẫn neo mốc 0 theo offset, nhưng bắt đầu ngay (không schedule).
            anchorDsp = now + firstBeatOffsetSec;
            dspStartTime = anchorDsp;
        }

        hasStarted = true;
    }

    /// <summary> Đổi BPM mà vẫn giữ nguyên vị trí beat hiện tại (không giật nốt). </summary>
    public void SetBpm(float newBpm)
    {
        newBpm = Mathf.Max(1f, newBpm);
        if (!hasStarted)
        {
            bpm = newBpm;
            OnTempoChanged?.Invoke(bpm);
            return;
        }

        // Chụp beat hiện tại (theo bpm cũ), neo lại tại thời điểm DSP hiện tại, rồi đổi độ dốc (bpm).
        double nowBeat = SongBeats;            // tính theo bpm hiện tại (cũ)
        anchorBeat = nowBeat;
        anchorDsp = AudioSettings.dspTime;
        bpm = newBpm;

        OnTempoChanged?.Invoke(bpm);
    }

    /// <summary>
    /// Đổi BPM mượt (ramp) trong 'rampSec'. Không nhảy beat; tốc độ thay đổi dần.
    /// Gọi: StartCoroutine(conductor.SetBpmSmooth(target, 0.12f));
    /// </summary>
    public IEnumerator SetBpmSmooth(float targetBpm, float rampSec = 0.12f)
    {
        targetBpm = Mathf.Max(1f, targetBpm);
        if (!hasStarted || rampSec <= 0f)
        {
            SetBpm(targetBpm);
            yield break;
        }

        double startBpm = bpm;
        double startBeat = SongBeats;              // đảm bảo liên tục
        double startDsp = AudioSettings.dspTime;

        float t = 0f;
        while (t < rampSec)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / rampSec);
            double curBpm = Mathf.Lerp((float)startBpm, (float)targetBpm, k);

            // Giữ liên tục beat bằng cách neo lại về (startBeat, startDsp) mỗi frame khi đang ramp
            anchorBeat = startBeat;
            anchorDsp = startDsp;
            bpm = (float)curBpm;

            yield return null;
        }

        // Chốt mốc cuối để tiếp tục chạy ổn định
        anchorBeat = SongBeats;
        anchorDsp = AudioSettings.dspTime;
        bpm = targetBpm;

        OnTempoChanged?.Invoke(bpm);
    }
    
    /// <summary>
    /// Stop the conductor
    /// </summary>
    public void StopConductor()
    {
        hasStarted = false;
        if (music && music.isPlaying)
        {
            music.Stop();
        }
    }
    
    /// <summary>
    /// Reset the conductor to initial state
    /// </summary>
    public void Reset()
    {
        hasStarted = false;
        anchorBeat = 0.0;
        anchorDsp = 0.0;
        dspStartTime = 0.0;
        
        if (music && music.isPlaying)
        {
            music.Stop();
        }
    }
}
