using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Flashy "you beat the boss" screen. Call CongratulationsScreen.Show("BOSS NAME").
///
/// Everything is built in code, so no scene setup is needed. It shows:
///  - a white screen flash and gold pulses
///  - a rotating gold sunburst behind everything
///  - a rainbow, wavy, bouncing CONGRATULATIONS! title
///  - falling confetti
///  - a blinking "press Z" prompt, which fades to the Level Select scene
///
/// The game is paused while it is up and the screen uses unscaled time. No music is played.
/// </summary>
public sealed class CongratulationsScreen : MonoBehaviour
{
    private const string NextScene = "Level Select";
    private const string Title = "CONGRATULATIONS!";
    private const int ConfettiCount = 150;
    private const int RayCount = 16;

    public static bool IsShowing { get; private set; }

    private static readonly Color Gold = new Color32(255, 212, 90, 255);
    private static readonly Color Ivory = new Color32(245, 236, 213, 255);

    private Image dim, flash;
    private RectTransform sunburst, titleRect, subtitleRect;
    private Image[] rays;
    private TextMeshProUGUI title, subtitle, prompt;

    private RectTransform[] confettiRect;
    private Image[] confettiImage;
    private float[] confettiSpeed, confettiSway, confettiPhase, confettiSpin, confettiX;

    private float shownAt;
    private float previousTimeScale = 1f;
    private bool leaving;

    public static void Show(string bossName)
    {
        if (IsShowing) return;
        var go = new GameObject("Congratulations Screen");
        var screen = go.AddComponent<CongratulationsScreen>();
        screen.Build(bossName);
        IsShowing = true;
    }

    // ------------------------------------------------------------------ build

    private void Build(string bossName)
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        shownAt = Time.unscaledTime;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // above the game, below the Esc menu and scene fades
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = transform;

        TMP_FontAsset font = FindGameFont();

        dim = Img(root, "Dim", Vector2.zero, Vector2.zero, true, new Color(0.02f, 0.01f, 0.08f, 0f));

        // Sunburst of gold rays rotating behind everything.
        sunburst = Rect(root, "Sunburst", Vector2.zero, Vector2.zero);
        rays = new Image[RayCount];
        for (int i = 0; i < RayCount; i++)
        {
            RectTransform ray = Rect(sunburst, "Ray", Vector2.zero, new Vector2(90f, 1500f));
            ray.pivot = new Vector2(0.5f, 0f);
            ray.localRotation = Quaternion.Euler(0f, 0f, i * (360f / RayCount));
            rays[i] = ray.gameObject.AddComponent<Image>();
            rays[i].color = new Color(Gold.r, Gold.g, Gold.b, 0f);
            rays[i].raycastTarget = false;
        }

        BuildConfetti(root);

        title = Text(root, "Title", new Vector2(0f, 120f), new Vector2(1800f, 200f), "", 110f, Color.white, font);
        titleRect = title.rectTransform;
        titleRect.localScale = Vector3.zero;

        string who = string.IsNullOrWhiteSpace(bossName) ? "THE BOSS" : bossName.ToUpperInvariant();
        if (who == "BOSS") who = "THE BOSS";
        subtitle = Text(root, "Subtitle", new Vector2(0f, -70f), new Vector2(1800f, 100f), "YOU DEFEATED " + who + "!", 52f, Ivory, font);
        subtitleRect = subtitle.rectTransform;
        subtitle.alpha = 0f;

        prompt = Text(root, "Prompt", new Vector2(0f, -330f), new Vector2(1800f, 80f), "PRESS Z TO CONTINUE", 40f, Color.white, font);
        prompt.alpha = 0f;

        flash = Img(root, "Flash", Vector2.zero, Vector2.zero, true, new Color(1f, 1f, 1f, 1f));
    }

    private void BuildConfetti(Transform root)
    {
        var holder = Rect(root, "Confetti", Vector2.zero, Vector2.zero);
        confettiRect = new RectTransform[ConfettiCount];
        confettiImage = new Image[ConfettiCount];
        confettiSpeed = new float[ConfettiCount];
        confettiSway = new float[ConfettiCount];
        confettiPhase = new float[ConfettiCount];
        confettiSpin = new float[ConfettiCount];
        confettiX = new float[ConfettiCount];
        for (int i = 0; i < ConfettiCount; i++)
        {
            float size = Random.Range(14f, 30f);
            RectTransform r = Rect(holder, "Confetti", Vector2.zero, new Vector2(size, size * Random.Range(0.5f, 1f)));
            var img = r.gameObject.AddComponent<Image>();
            img.color = Color.HSVToRGB(Random.value, 0.8f, 1f);
            img.raycastTarget = false;
            confettiRect[i] = r;
            confettiImage[i] = img;
            confettiSpeed[i] = Random.Range(180f, 480f);
            confettiSway[i] = Random.Range(40f, 140f);
            confettiPhase[i] = Random.Range(0f, 10f);
            confettiSpin[i] = Random.Range(-420f, 420f);
            confettiX[i] = Random.Range(-1000f, 1000f);
            // Start scattered over the whole screen height so confetti is already falling when it appears.
            r.anchoredPosition = new Vector2(confettiX[i], Random.Range(-600f, 600f));
        }
    }

    // ------------------------------------------------------------------ animate

    private void Update()
    {
        float t = Time.unscaledTime - shownAt;
        float dt = Time.unscaledDeltaTime;

        dim.color = new Color(0.02f, 0.01f, 0.08f, Mathf.Min(0.92f, t / 0.4f * 0.92f));

        // Sunburst: spin and breathe.
        sunburst.localRotation = Quaternion.Euler(0f, 0f, t * 14f);
        float rayAlpha = Mathf.Clamp01(t / 0.8f) * (0.10f + 0.05f * Mathf.Sin(t * 3f));
        for (int i = 0; i < rays.Length; i++)
        {
            // Every other ray in a second hue for a shimmering look.
            Color c = (i % 2 == 0) ? Gold : Color.HSVToRGB(Mathf.Repeat(t * 0.2f, 1f), 0.6f, 1f);
            rays[i].color = new Color(c.r, c.g, c.b, rayAlpha);
        }

        AnimateConfetti(t, dt);
        AnimateTitle(t);
        AnimateSubtitleAndPrompt(t);

        // Flash: a big one at the start, soft gold pulses afterwards.
        float flashAlpha = Mathf.Clamp01(1f - t / 0.7f);
        float pulse = Mathf.Repeat(t - 0.7f, 2.4f);
        if (t > 0.7f) flashAlpha = Mathf.Max(flashAlpha, 0.22f * Mathf.Clamp01(1f - pulse / 0.35f));
        flash.color = new Color(1f, t < 0.7f ? 1f : 0.9f, t < 0.7f ? 1f : 0.55f, flashAlpha);

        Keyboard keys = Keyboard.current;
        if (!leaving && t > 1.2f && keys != null && keys.zKey.wasPressedThisFrame) Continue();
    }

    private void AnimateConfetti(float t, float dt)
    {
        float fade = Mathf.Clamp01(t / 0.6f);
        for (int i = 0; i < ConfettiCount; i++)
        {
            RectTransform r = confettiRect[i];
            Vector2 p = r.anchoredPosition;
            p.y -= confettiSpeed[i] * dt;
            p.x = confettiX[i] + Mathf.Sin(t * 2f + confettiPhase[i]) * confettiSway[i];
            if (p.y < -640f)
            {
                p.y = 640f;
                confettiX[i] = Random.Range(-1000f, 1000f);
                confettiImage[i].color = Color.HSVToRGB(Random.value, 0.8f, 1f);
            }
            r.anchoredPosition = p;
            r.localRotation = Quaternion.Euler(0f, 0f, t * confettiSpin[i]);
            // Squash like a flipping piece of paper.
            r.localScale = new Vector3(1f, Mathf.Abs(Mathf.Cos(t * 4f + confettiPhase[i])) * 0.8f + 0.2f, 1f);
            Color c = confettiImage[i].color;
            c.a = fade;
            confettiImage[i].color = c;
        }
    }

    private void AnimateTitle(float t)
    {
        // Rainbow letters that ripple up and down.
        var sb = new StringBuilder(Title.Length * 40);
        for (int i = 0; i < Title.Length; i++)
        {
            Color col = Color.HSVToRGB(Mathf.Repeat(t * 0.5f + i * 0.06f, 1f), 0.85f, 1f);
            float lift = Mathf.Sin(t * 6f + i * 0.5f) * 0.12f;
            sb.Append("<voffset=").Append(lift.ToString("0.00", CultureInfo.InvariantCulture)).Append("em><color=#")
              .Append(ColorUtility.ToHtmlStringRGB(col)).Append('>').Append(Title[i]).Append("</color></voffset>");
        }
        title.text = sb.ToString();

        // Pop in with overshoot, then keep pulsing and shaking slightly.
        float scale;
        if (t < 0.7f)
        {
            float s = t / 0.7f - 1f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            scale = 1f + c3 * s * s * s + c1 * s * s;
        }
        else scale = 1f + 0.04f * Mathf.Sin(t * 6f);
        titleRect.localScale = Vector3.one * Mathf.Max(0f, scale);
        titleRect.anchoredPosition = new Vector2(Mathf.Sin(t * 31f) * 2f, 120f + Mathf.Sin(t * 3f) * 10f);
    }

    private void AnimateSubtitleAndPrompt(float t)
    {
        float subT = Mathf.Clamp01((t - 0.9f) / 0.4f);
        subtitle.alpha = subT;
        subtitle.color = Color.Lerp(Ivory, Gold, 0.5f + 0.5f * Mathf.Sin(t * 5f));
        subtitle.alpha = subT;
        subtitleRect.localScale = Vector3.one * (0.8f + 0.2f * subT);

        float promptT = Mathf.Clamp01((t - 2.2f) / 0.4f);
        bool on = Mathf.Repeat(t * 1.8f, 1f) < 0.65f;
        prompt.alpha = promptT * (on ? 1f : 0.25f);
    }

    private void Continue()
    {
        leaving = true;
        Time.timeScale = 1f;                 // the scene fade needs time to run
        IsShowing = false;
        SceneTransition.Load(NextScene, 0.6f);
    }

    private void OnDestroy()
    {
        IsShowing = false;
        if (!leaving) Time.timeScale = previousTimeScale;
    }

    // ------------------------------------------------------------------ helpers

    private static TMP_FontAsset FindGameFont()
    {
        foreach (TMP_FontAsset candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (candidate != null && candidate.name == "ENTIRE GAME FONT") return candidate;
        return null;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Img(Transform parent, string name, Vector2 position, Vector2 size, bool stretch, Color color)
    {
        RectTransform rect = Rect(parent, name, position, size);
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, Vector2 position, Vector2 size, string text,
        float fontSize, Color color, TMP_FontAsset font)
    {
        RectTransform rect = Rect(parent, name, position, size);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.richText = true;
        label.raycastTarget = false;
        return label;
    }
}
