
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public static class SpeedItUpSceneBuilderV21
{
    [MenuItem("Tools/Build Speed It Up Scene (Pitch Mode, 7 Keys)")]
    public static void BuildScenePitch()
    {
        if (!TypeExists("TMPro.TextMeshProUGUI"))
        {
            EditorUtility.DisplayDialog("Missing TMP", "Window → TextMeshPro → Import TMP Essentials trước đã.", "OK");
            return;
        }

        Ensure("Assets/Scenes");
        Ensure("Assets/Prefabs");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SpeedItUp";

        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGO.AddComponent<AudioListener>();

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var topBar = CreateUI("TopBar", canvasGO.transform);
        RectTop(topBar, 24, 24, 24, 180);
        var bpmText = CreateTMP("BpmText", topBar.transform, "BPM: 100", 64, TextAlignmentOptions.Left);
        var bannerText = CreateTMP("BpmBanner", topBar.transform, "", 64, TextAlignmentOptions.Right);
        (bannerText.transform as RectTransform).anchoredPosition = new Vector2(400, -60);
        bannerText.gameObject.SetActive(false);
        var feedback = CreateTMP("Feedback", topBar.transform, "", 56, TextAlignmentOptions.Center);
        (feedback.transform as RectTransform).anchoredPosition = new Vector2(0, -120);

        var playField = CreateUI("PlayField", canvasGO.transform);
        RectStretch(playField, 60, 60, 360, 380);

        var hitLine = CreateUI("HitLine", playField.transform);
        var hitImg = hitLine.AddComponent<Image>();
        hitImg.color = Color.white;
        var rtHit = hitLine.GetComponent<RectTransform>();
        rtHit.anchorMin = rtHit.anchorMax = new Vector2(0.5f, 0.28f);
        rtHit.anchoredPosition = Vector2.zero;
        rtHit.sizeDelta = new Vector2(900, 10);

        var hitSprite = LoadSprite("hitline_1024");
        if (hitSprite) { hitImg.sprite = hitSprite; rtHit.sizeDelta = new Vector2(hitSprite.rect.width, hitSprite.rect.height); }

        var lanes = new RectTransform[7];
        float usableWidth = 900f;
        float col = usableWidth / 7f;
        float startX = -usableWidth/2f + col/2f;

        for (int i=0;i<7;i++)
        {
            var lane = CreateUI("Lane"+i, playField.transform);
            var rt = lane.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(col - 12f, 1600f);
            rt.anchoredPosition = new Vector2(startX + i*col, 200f);

            var img = lane.AddComponent<Image>();
            img.color = new Color(1,1,1,0.05f);
            lanes[i] = rt;
        }

        var systems = new GameObject("Systems");
        var conductor = systems.AddComponent<Conductor>();
        var tempo = systems.AddComponent<TempoController>(); tempo.conductor = conductor;
        var input = systems.AddComponent<TapInput>();
        var hud = systems.AddComponent<BpmHud>(); hud.conductor = conductor; hud.tempo = tempo; hud.bpmText = bpmText; hud.bannerText = bannerText;

        var spawnerGO = new GameObject("Spawner");
        var spawner = spawnerGO.AddComponent<NoteSpawner>();
        spawner.conductor = conductor;
        spawner.lanes = lanes;
        spawner.hitLine = rtHit;
        spawner.spawnLeadBeats = 8f;
        spawner.unitsPerBeat = 220f;

        var mapField = typeof(NoteSpawner).GetField("mapDegreeToLane");
        if (mapField != null) mapField.SetValue(spawner, true);
        var mapArray = typeof(NoteSpawner).GetField("degreeToLane");
        if (mapArray != null) mapArray.SetValue(spawner, new int[7]{0,1,2,3,4,5,6});

        var judgeGO = new GameObject("Judge");
        var judge = judgeGO.AddComponent<JudgeController>();
        judge.conductor = conductor;
        judge.tempo = tempo;
        judge.spawner = spawner;
        judge.feedbackText = feedback;

        var noteGO = CreateUI("Note", canvasGO.transform);
        var rtNote = noteGO.GetComponent<RectTransform>();
        rtNote.sizeDelta = new Vector2(120, 120);
        var imgNote = noteGO.AddComponent<Image>();
        var noteSprite = LoadSprite("note_circle_256");
        if (noteSprite) imgNote.sprite = noteSprite;
        noteGO.AddComponent<PoolableNote>();
        var view = noteGO.AddComponent<NoteView>();
        view.rect = rtNote;
        view.hitLineY = spawner.hitLine.anchoredPosition.y;
        var notePrefabPath = "Assets/Prefabs/Note.prefab";
        var notePrefab = PrefabUtility.SaveAsPrefabAsset(noteGO, notePrefabPath);
        Object.DestroyImmediate(noteGO);
        spawner.notePrefab = notePrefab;

        var pool = spawnerGO.AddComponent<ObjectPool>();
        pool.prefab = notePrefab; pool.initialSize = 128;
        var adapter = spawnerGO.AddComponent<NotePoolAdapter>();
        adapter.pool = pool; adapter.spawner = spawner;

        var noteAudioGO = new GameObject("NoteAudio");
        var scale = noteAudioGO.AddComponent<ScaleNoteAudio>();
        for (int d=1; d<=7; d++)
        {
            var clip = LoadClip("degree_" + d + "_Cmaj");
            if (clip)
            {
                if (scale.degreeClips == null || scale.degreeClips.Length < 7) scale.degreeClips = new AudioClip[7];
                scale.degreeClips[d-1] = clip;
            }
        }

        var fJ = typeof(JudgeController).GetField("noteAudio"); if (fJ!=null) fJ.SetValue(judge, scale);
        var fS = typeof(NoteSpawner).GetField("noteAudio"); if (fS!=null) fS.SetValue(spawner, scale);
        var fG = typeof(NoteSpawner).GetField("guidePlayback"); if (fG!=null) fG.SetValue(spawner, false);

        var hitClip = LoadClip("hit_beep"); if (hitClip){ var a = judgeGO.AddComponent<AudioSource>(); a.playOnAwake=false; a.spatialBlend=0f; a.clip=hitClip; judge.sfxHit=a; }
        var missClip= LoadClip("miss_beep"); if (missClip){ var a = judgeGO.AddComponent<AudioSource>(); a.playOnAwake=false; a.spatialBlend=0f; a.clip=missClip; judge.sfxMiss=a; }

        var keysRoot = new GameObject("PianoKeys", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        keysRoot.SetParent(canvasGO.transform, false);
        keysRoot.anchorMin = new Vector2(0, 0);
        keysRoot.anchorMax = new Vector2(1, 0);
        keysRoot.pivot = new Vector2(0.5f, 0);
        keysRoot.offsetMin = new Vector2(80, 40);
        keysRoot.offsetMax = new Vector2(-80, 320);
        var h = keysRoot.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;

        string[] names = {"C","D","E","F","G","A","B"};
        for (int i=0;i<7;i++)
        {
            var bGO = new GameObject("Key"+(i+1), typeof(RectTransform));
            bGO.transform.SetParent(keysRoot, false);
            var img = bGO.AddComponent<Image>(); img.color = new Color(1,1,1,0.25f);
            var btn = bGO.AddComponent<Button>();
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(bGO.transform, false);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = names[i]; label.fontSize = 60; label.alignment = TextAlignmentOptions.Center;
            var lrt = label.GetComponent<RectTransform>(); lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f,0.5f); lrt.sizeDelta = new Vector2(120,120);

            int degree = i+1;
            btn.onClick.AddListener(() =>
            {
                var ti = Object.FindFirstObjectByType<TapInput>();
                if (ti) ti.RaiseTapDegree(degree);
            });
        }

        var bootGO = new GameObject("Boot");
        var boot = bootGO.AddComponent<Boot>();
        TryAssign(boot, "conductor", conductor);
        TryAssign(boot, "tempoController", tempo);
        TryAssign(boot, "noteSpawner", spawner);
        TryAssign(boot, "input", input);
        TryAssign(boot, "judge", judge);
        TryAssign(boot, "noteAudio", scale);
        TryAssign(boot, "chartFileName", "scale_demo.json");
        TryAssign(boot, "useGuidePlayback", false);
        TryAssign(boot, "takeBpmFromRemoteConfig", true);

        string scenePath = "Assets/Scenes/SpeedItUp.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorUtility.DisplayDialog("Scene Built", "Created (Pitch Mode): " + scenePath, "OK");
    }

    static bool TypeExists(string typeName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(typeName);
            if (t != null) return true;
        }
        return false;
    }
    static void Ensure(string path)
    {
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i=1;i<parts.Length;i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
    static GameObject CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return go;
    }
    static RectTransform RectTop(GameObject go, float left, float right, float top, float height)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(left, -height);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }
    static RectTransform RectStretch(GameObject go, float left, float right, float bottom, float top)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }
    static TextMeshProUGUI CreateTMP(string name, Transform parent, string text, int size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(900, 120);
        return tmp;
    }
    static Sprite LoadSprite(string name)
    {
        var guids = AssetDatabase.FindAssets(name + " t:sprite");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp && sp.name.ToLower().Contains(name.ToLower())) return sp;
        }
        return null;
    }
    static AudioClip LoadClip(string name)
    {
        var guids = AssetDatabase.FindAssets(name + " t:audioclip");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var ac = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (ac && ac.name.ToLower().Contains(name.ToLower())) return ac;
        }
        return null;
    }
    static void TryAssign(Object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName);
        if (f != null) f.SetValue(obj, value);
        var p = obj.GetType().GetProperty(fieldName);
        if (p != null && p.CanWrite) p.SetValue(obj, value);
    }
}
#endif
