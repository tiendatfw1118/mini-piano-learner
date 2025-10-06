
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PitchUIBuilder
{
    [MenuItem("Tools/Add Pitch UI (7 Keys + 7 Lanes)")]
    public static void AddPitchUI()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (!canvas) { EditorUtility.DisplayDialog("Missing Canvas", "Run Scene Builder first.", "OK"); return; }

        var spawner = Object.FindFirstObjectByType<NoteSpawner>();
        var input = Object.FindFirstObjectByType<TapInput>();

        // 7 lanes horizontally
        var playField = GameObject.Find("PlayField")?.transform ?? canvas.transform;
        var lanes = new RectTransform[7];
        float width = 900f; // total usable width
        float col = width / 7f;
        float startX = -width/2f + col/2f;

        for (int i=0;i<7;i++)
        {
            var lane = new GameObject("Lane"+i, typeof(RectTransform)).GetComponent<RectTransform>();
            lane.SetParent(playField, false);
            lane.sizeDelta = new Vector2(col-12f, 1600f);
            lane.anchoredPosition = new Vector2(startX + i*col, 200f);
            lanes[i] = lane;
        }

        if (spawner)
        {
            spawner.lanes = lanes;
            spawner.mapDegreeToLane = true;
            spawner.degreeToLane = new int[7]{0,1,2,3,4,5,6};
        }

        // Piano keys at bottom
        var keysRoot = new GameObject("PianoKeys", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        keysRoot.SetParent(canvas.transform, false);
        keysRoot.anchorMin = new Vector2(0, 0);
        keysRoot.anchorMax = new Vector2(1, 0);
        keysRoot.pivot = new Vector2(0.5f, 0);
        keysRoot.offsetMin = new Vector2(80, 40);
        keysRoot.offsetMax = new Vector2(-80, 320);

        var h = keysRoot.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;

        var piano = keysRoot.gameObject.AddComponent<PianoKeysUI>();
        piano.input = input;
        piano.spawner = spawner;
        piano.keys = new Button[7];

        string[] names = {"C","D","E","F","G","A","B"};

        for (int i=0;i<7;i++)
        {
            var bGO = new GameObject("Key"+(i+1), typeof(RectTransform));
            bGO.transform.SetParent(keysRoot, false);
            var img = bGO.AddComponent<Image>(); img.color = new Color(1,1,1,0.25f);
            var btn = bGO.AddComponent<Button>();
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(bGO.transform, false);
            label.text = names[i]; label.fontSize = 60; label.alignment = TextAlignmentOptions.Center;
            var lrt = label.GetComponent<RectTransform>(); lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f,0.5f); lrt.sizeDelta = new Vector2(120,120);

            piano.keys[i] = btn;

            int degree = i+1;
            btn.onClick.AddListener(() =>
            {
                var ti = Object.FindFirstObjectByType<TapInput>();
                if (ti) ti.RaiseTapDegree(degree);
            });
        }

        EditorUtility.DisplayDialog("Pitch UI", "Added 7 lanes and PianoKeys UI.", "OK");
    }
}
#endif
