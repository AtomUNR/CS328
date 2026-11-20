using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Press Esc in any gameplay scene to open a small pause menu that shows the player's HP and offers
/// to quit the game. It uses the game's normal controls: Z confirms (quit), X goes back
/// (Esc also closes it).
///
/// It is disabled in the Main Menu, which has its own Quit button.
///
/// No scene setup is needed: this creates itself once when the game starts and survives scene changes.
/// While the menu is open the game is paused (Time.timeScale = 0) and restored afterwards.
/// </summary>
public sealed class QuitOnEscape : MonoBehaviour
{
    // Scenes where Esc does nothing (they already have their own quit button).
    private static readonly string[] DisabledScenes = { "Main Menu" };

    private static QuitOnEscape instance;

    private GameObject prompt;
    private TMP_Text hpLabel;
    private RectTransform hpFill;
    private GameObject hpRow;
    private float previousTimeScale = 1f;
    private bool open;

    private static readonly Color Ivory = new Color32(245, 236, 213, 255);
    private static readonly Color Cyan = new Color32(86, 230, 236, 255);
    private static readonly Color Muted = new Color32(176, 170, 150, 255);
    private static readonly Color HpPink = new Color32(239, 115, 124, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("QuitOnEscape");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<QuitOnEscape>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A scene change (death, menu, level select) ends the menu and resumes time.
        if (open) Close();
    }

    private static bool InDisabledScene()
    {
        string current = SceneManager.GetActiveScene().name;
        foreach (string name in DisabledScenes)
            if (current == name) return true;
        return false;
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null) return;

        if (InDisabledScene())
        {
            if (open) Close();
            return;
        }

        if (keys.escapeKey.wasPressedThisFrame)
        {
            if (open) Close();   // Esc again = back
            else Open();
        }
        else if (open)
        {
            if (keys.zKey.wasPressedThisFrame) Quit();                // Z = confirm
            else if (keys.xKey.wasPressedThisFrame) Close();          // X = back
        }
    }

    private void Open()
    {
        if (prompt == null) prompt = BuildPrompt();
        RefreshHp();
        previousTimeScale = Time.timeScale; // may already be 0 (e.g. the congratulations screen is up)
        Time.timeScale = 0f;
        prompt.SetActive(true);
        open = true;
    }

    private void Close()
    {
        if (prompt != null) prompt.SetActive(false);
        if (open) Time.timeScale = previousTimeScale;
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
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
        if (open) Time.timeScale = previousTimeScale;
    }

    // ------------------------------------------------------------------ HP

    private void RefreshHp()
    {
        Player player = FindAnyObjectByType<Player>();
        CharacterStats stats = player != null ? player.Stats : null;
        // Scenes without a player (level select, game over) just don't show the HP row.
        hpRow.SetActive(stats != null);
        if (stats == null) return;

        hpLabel.text = $"HP {stats.CurrentHP}/{stats.MaxHP}";
        float fraction = stats.MaxHP > 0 ? Mathf.Clamp01((float)stats.CurrentHP / stats.MaxHP) : 0f;
        hpFill.gameObject.SetActive(fraction > 0.001f);
        hpFill.anchorMax = new Vector2(fraction, 1f);
    }

    // ------------------------------------------------------------------ UI

    private GameObject BuildPrompt()
    {
        var canvasObject = new GameObject("Pause Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
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
        Box(canvasObject.transform, "Dim", Vector2.zero, Vector2.zero, true, new Color(0f, 0f, 0f, 0.7f));

        // Ivory-bordered black panel, like the battle HUD.
        RectTransform panel = Box(canvasObject.transform, "Panel", Vector2.zero, new Vector2(900f, 400f), false, Ivory);
        Box(panel, "Fill", Vector2.zero, new Vector2(892f, 392f), false, Color.black);

        TMP_FontAsset font = FindGameFont();
        Label(panel, "Title", new Vector2(0f, 130f), new Vector2(840f, 80f), "QUIT GAME?", 56f, Ivory, font);

        // HP read-out: text plus a bar, in the HUD's colours.
        hpRow = new GameObject("HP Row", typeof(RectTransform));
        var rowRect = hpRow.GetComponent<RectTransform>();
        rowRect.SetParent(panel, false);
        rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, 45f);
        rowRect.sizeDelta = new Vector2(840f, 60f);
        hpLabel = Label(rowRect, "HP Text", new Vector2(-150f, 0f), new Vector2(380f, 50f), "", 38f, Ivory, font);
        RectTransform barBorder = Box(rowRect, "HP Bar", new Vector2(215f, 0f), new Vector2(300f, 34f), false, Ivory);
        Box(barBorder, "Fill", Vector2.zero, new Vector2(288f, 22f), false, new Color32(20, 20, 20, 255));
        hpFill = Box(barBorder, "Value", Vector2.zero, Vector2.zero, false, HpPink);
        hpFill.anchorMin = new Vector2(0f, 0f);
        hpFill.anchorMax = new Vector2(1f, 1f);
        hpFill.pivot = new Vector2(0f, 0.5f);
        hpFill.offsetMin = new Vector2(6f, 6f);
        hpFill.offsetMax = new Vector2(-6f, -6f);

        // Confirm and back use the game's normal keys.
        Label(panel, "Confirm", new Vector2(0f, -55f), new Vector2(840f, 50f), "Z  -  QUIT", 36f, Cyan, font);
        Label(panel, "Back", new Vector2(0f, -120f), new Vector2(840f, 50f), "X  -  KEEP PLAYING", 32f, Muted, font);

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

    private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, bool stretch, Color color)
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

    private static TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, string text,
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
        return label;
    }
}
