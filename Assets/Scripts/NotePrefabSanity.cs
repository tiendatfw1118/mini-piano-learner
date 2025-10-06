using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-10)]
public class NotePrefabSanity : MonoBehaviour
{
    void Awake()
    {
        var rt = GetComponent<RectTransform>();
        if (rt && rt.sizeDelta.sqrMagnitude < 1f)
            rt.sizeDelta = new Vector2(80f, 130f);
        var img = GetComponent<Image>();
        if (!img) gameObject.AddComponent<Image>();
    }
}
