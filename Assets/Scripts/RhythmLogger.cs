using UnityEngine;

public static class RhythmLogger
{
    [System.Serializable] struct NoteEvent { public string type, noteId, songId; public double expected, input, deltaMs; }
    [System.Serializable] struct SongEnd { public string type, songId; public int hit, miss; public double duration; }

    public static void LogNote(string type, string noteId, string songId, double expected, double input)
    {
        double deltaMs = (input - expected) * 1000.0;
        var ev = new NoteEvent { type = type, noteId = noteId, songId = songId, expected = expected, input = input, deltaMs = deltaMs };
        Debug.Log(JsonUtility.ToJson(ev));
    }
    public static void LogSongEnd(string songId, int hit, int miss, double duration)
    { 
        var ev = new SongEnd { type = "song_end", songId = songId, hit = hit, miss = miss, duration = duration }; 
        Debug.Log(JsonUtility.ToJson(ev)); 
    }
}
