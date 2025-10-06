using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

[System.Serializable]
public class ChartNote { public string id; public double beat; public int lane; public int degree = 1; public float len = 0f; }

[System.Serializable]
public class ChartData
{
    public string songId = "twinkle";
    public float bpm = 120f;
    public float firstBeatOffsetSec = 0f;
    public string[] stems;
    public ChartNote[] notes;
}

public static class ChartLoader
{
    public static async Task<ChartData> LoadAsync(string fileName = "twinkle.json")
    {
        string rel = Path.Combine("Charts", fileName);
        string path = Path.Combine(Application.streamingAssetsPath, rel);
        using var req = UnityWebRequest.Get(path);
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();
        if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("ChartLoader failed: "+req.error); return null; }
        return JsonUtility.FromJson<ChartData>(req.downloadHandler.text);
    }
}
