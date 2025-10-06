using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple restart button that calls Boot.RestartGame()
/// </summary>
public class RestartButton : MonoBehaviour
{
    [Header("References")]
    public Boot boot;
    
    [Header("Button Settings")]
    public string buttonText = "Restart";
    public Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color textColor = Color.white;
    public int fontSize = 24;
    
    void Start()
    {
        // Find Boot component if not assigned
        if (!boot)
        {
            boot = FindFirstObjectByType<Boot>();
        }
        
        // Create button UI
        CreateButton();
    }
    
    void CreateButton()
    {
        // Get or create Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (!canvas)
        {
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        
        // Create button GameObject
        var buttonGO = new GameObject("RestartButton");
        buttonGO.transform.SetParent(canvas.transform, false);
        
        // Add Image component for button background
        var image = buttonGO.AddComponent<Image>();
        image.color = buttonColor;
        
        // Add Button component
        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = image;
        
        // Position button in top right
        var rectTransform = buttonGO.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = new Vector2(-20f, -20f);
        rectTransform.sizeDelta = new Vector2(120f, 50f);
        
        // Create text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        var text = textGO.AddComponent<Text>();
        text.text = buttonText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        
        // Position text to fill button
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Add click listener
        button.onClick.AddListener(OnRestartClicked);
    }
    
    void OnRestartClicked()
    {
        if (boot)
        {
            boot.RestartGame();
        }
        else
        {
            // Boot component not found - this is expected in some cases
        }
    }
}
