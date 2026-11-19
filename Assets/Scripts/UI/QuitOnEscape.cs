using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Press Esc in any scene to quit the game. A confirmation is shown first so a stray key press
/// does not throw away progress: Esc again quits, Backspace keeps playing.
///
/// No scene setup is needed: this creates itself once when the game starts and survives scene changes.
/// While the prompt is open the game is paused (Time.timeScale = 0) and restored afterwards.
/// </summary>
public sealed class QuitOnEscape : MonoBehaviour
{
    private static QuitOnEscape instance;

    private GameObject prompt;
    private float previousTimeScale = 1f;
    private bool open;

    private static readonly Color Ivory = new Color32(245, 236, 213, 255);
    private static readonly Color Cyan = new Color32(86, 230, 236, 255);
    private static readonly Color Muted = new Color32(176, 170, 150, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("QuitOnEscape");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<QuitOnEscape>();
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null) return;

        if (keys.escapeKey.wasPressedThisFrame)
        {
            if (open) Quit();
            else Open();
        }
        else if (open && keys.backspaceKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void Open()
    {
        if (prompt == null) prompt = BuildPrompt();
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        prompt.SetActive(true);
        open = true;
    }

    private void Close()
    {
        if (prompt != null) prompt.SetActive(false);
        Time.timeScale = previousTimeScale;
        open = false;
    }

    private void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Application.Quit does nothing in the editor
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (open) Time.timeScale = previousTimeScale;
    }

    // ------------------------------------------------------------------ UI

    private GameObject BuildPrompt()
    {
        var canvasObject = new GameObject("Quit Prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // above every other UI
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Dim the whole screen.
        Rect(canvasObject.transform, "Dim", Vector2.zero, Vector2.zero, true, new Color(0f, 0f, 0f, 0.7f));

        // Ivory-bordered black panel, like the battle HUD.
        RectTransform panel = Rect(canvasObject.transform, "Panel", Vector2.zero, new Vector2(900f, 300f), false, Ivory);
        Rect(panel, "Fill", Vector2.zero, new Vector2(892f, 292f), false, Color.black);

        TMP_FontAsset font = FindGameFont();
        Label(panel, "Title", new Vector2(0f, 60f), new Vector2(840f, 80f), "QUIT GAME?", 56f, Ivory, font);
        Label(panel, "Yes", new Vector2(0f, -20f), new Vector2(840f, 50f), "ESC  -  QUIT", 34f, Cyan, font);
        Label(panel, "No", new Vector2(0f, -80f), new Vector2(840f, 50f), "BACKSPACE  -  KEEP PLAYING", 28f, Muted, font);

        canvasObject.SetActive(false);
        return canvasObject;
    }

    // Reuse the game's pixel font if a scene already loaded it; otherwise TextMeshPro's default.
    private static TMP_FontAsset FindGameFont()
    {
        foreach (TMP_FontAsset candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (candidate != null && candidate.name == "ENTIRE GAME FONT") return candidate;
        return null;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size, bool stretch, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = stretch; // the dim layer blocks clicks on the game underneath
        return rect;
    }

    private static void Label(Transform parent, string name, Vector2 position, Vector2 size, string text,
        float fontSize, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }
}
