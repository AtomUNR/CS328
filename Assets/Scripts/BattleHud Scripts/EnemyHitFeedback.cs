using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EnemyHitKind
{
    Basic,          // timing-bar attack
    ChargedGood,    // Charged Strike, Good Hit
    ChargedPerfect, // Charged Strike, Perfect
    Miss            // any attack that missed
}

// Shows a damage indicator on the enemy sprite. Each kind of attack looks different:
//   Basic          - white number, quick flicker, small sideways shake
//   ChargedGood    - bigger cyan number, cyan flash, harder shake, a few sparks
//   ChargedPerfect - huge gold number, gold flashes, violent shake, burst of sparks
//   Miss           - grey "MISS", the enemy sidesteps
public sealed class EnemyHitFeedback : MonoBehaviour
{
    [Header("Scene references")]
    [Tooltip("The enemy picture on the battle HUD (Enemy Sprite).")]
    [SerializeField] private RectTransform enemyImage;
    [Tooltip("Where popups and sparks are created. Use the BattleHUD (same parent as the enemy picture).")]
    [SerializeField] private RectTransform popupParent;
    [SerializeField] private TMP_FontAsset font;

    private struct Style
    {
        public Color color;     // number colour
        public Color tint;      // enemy flash colour
        public float fontSize;
        public float rise;      // pixels the number floats up
        public float life;      // seconds
        public float popScale;  // peak scale of the pop-in
        public float shake;     // pixels
        public float shakeTime; // seconds
        public bool horizontalOnly;
        public bool dodge;      // enemy sidesteps instead of shaking
        public int blinks;
        public int sparks;
    }

    private static readonly Color Ivory = new Color32(245, 236, 213, 255);
    private static readonly Color Cyan = new Color32(86, 230, 236, 255);
    private static readonly Color Gold = new Color32(255, 212, 90, 255);
    private static readonly Color Muted = new Color32(176, 170, 150, 255);
    private static readonly Color Outline = new Color32(10, 8, 12, 255);

    private Coroutine enemyRoutine;
    private Vector2 restPosition;
    private Color restColor;
    private Graphic enemyGraphic;

    public void Play(EnemyHitKind kind, int damage)
    {
        if (enemyImage == null || popupParent == null || !isActiveAndEnabled) return;

        if (enemyRoutine != null)
        {
            StopCoroutine(enemyRoutine);
            RestoreEnemy();
        }
        else
        {
            restPosition = enemyImage.anchoredPosition;
            enemyGraphic = enemyImage.GetComponent<Graphic>();
            restColor = enemyGraphic != null ? enemyGraphic.color : Color.white;
        }

        Style style = StyleFor(kind);
        enemyRoutine = StartCoroutine(EnemyRoutine(style));
        StartCoroutine(PopupRoutine(kind == EnemyHitKind.Miss ? "MISS" : "-" + damage, style));
        if (style.sparks > 0) StartCoroutine(SparkRoutine(style));
    }

    private static Style StyleFor(EnemyHitKind kind)
    {
        switch (kind)
        {
            case EnemyHitKind.ChargedGood:
                return new Style
                {
                    color = Cyan, tint = new Color(0.4f, 0.95f, 1f, 1f), fontSize = 100f, rise = 140f,
                    life = 1.0f, popScale = 1.5f, shake = 18f, shakeTime = 0.35f, blinks = 3, sparks = 8
                };
            case EnemyHitKind.ChargedPerfect:
                return new Style
                {
                    color = Gold, tint = new Color(1f, 0.85f, 0.3f, 1f), fontSize = 135f, rise = 170f,
                    life = 1.2f, popScale = 1.8f, shake = 30f, shakeTime = 0.5f, blinks = 5, sparks = 18
                };
            case EnemyHitKind.Miss:
                return new Style
                {
                    color = Muted, tint = Color.white, fontSize = 70f, rise = 60f,
                    life = 0.85f, popScale = 1.1f, dodge = true, shakeTime = 0.35f
                };
            default:
                return new Style
                {
                    color = Ivory, tint = new Color(1f, 1f, 1f, 0.25f), fontSize = 72f, rise = 110f,
                    life = 0.85f, popScale = 1.25f, shake = 10f, shakeTime = 0.22f, horizontalOnly = true, blinks = 2
                };
        }
    }

    // ------------------------------------------------------------ enemy sprite

    private IEnumerator EnemyRoutine(Style style)
    {
        float duration = Mathf.Max(style.shakeTime, style.blinks * 0.12f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Vector2 offset = Vector2.zero;
            if (style.dodge)
            {
                offset.x = Mathf.Sin(Mathf.Clamp01(t / style.shakeTime) * Mathf.PI) * 48f;
            }
            else if (style.shake > 0f && t < style.shakeTime)
            {
                float falloff = 1f - t / style.shakeTime;
                offset.x = Random.Range(-1f, 1f) * style.shake * falloff;
                if (!style.horizontalOnly) offset.y = Random.Range(-1f, 1f) * style.shake * falloff;
            }
            enemyImage.anchoredPosition = restPosition + offset;

            if (enemyGraphic != null && style.blinks > 0)
                enemyGraphic.color = Mathf.FloorToInt(t / 0.06f) % 2 == 0 ? style.tint : restColor;
            yield return null;
        }
        RestoreEnemy();
        enemyRoutine = null;
    }

    private void RestoreEnemy()
    {
        if (enemyImage != null) enemyImage.anchoredPosition = restPosition;
        if (enemyGraphic != null) enemyGraphic.color = restColor;
    }

    // ------------------------------------------------------------ number popup

    private IEnumerator PopupRoutine(string message, Style style)
    {
        RectTransform rect = MakeRect("Enemy Hit Popup", new Vector2(600f, 220f));
        Vector2 start = enemyImage.anchoredPosition + new Vector2(Random.Range(-40f, 40f), 70f);
        rect.anchoredPosition = start;

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = message;
        label.fontSize = style.fontSize;
        label.color = style.color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.outlineWidth = 0.3f;
        label.outlineColor = Outline;

        float t = 0f;
        while (t < style.life)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / style.life);
            float eased = 1f - (1f - u) * (1f - u);
            rect.anchoredPosition = start + Vector2.up * (style.rise * eased);

            float scale = u < 0.12f ? Mathf.Lerp(0.4f, style.popScale, u / 0.12f)
                                    : Mathf.Lerp(style.popScale, 1f, Mathf.Clamp01((u - 0.12f) / 0.2f));
            rect.localScale = Vector3.one * scale;

            float alpha = u < 0.6f ? 1f : 1f - (u - 0.6f) / 0.4f;
            Color c = style.color;
            c.a = alpha;
            label.color = c;
            label.outlineColor = new Color(Outline.r, Outline.g, Outline.b, alpha);
            yield return null;
        }
        Destroy(rect.gameObject);
    }

    // ------------------------------------------------------------ sparks

    private IEnumerator SparkRoutine(Style style)
    {
        int count = style.sparks;
        RectTransform[] rects = new RectTransform[count];
        Image[] images = new Image[count];
        Vector2[] velocity = new Vector2[count];
        Vector2 origin = enemyImage.anchoredPosition + new Vector2(0f, 30f);

        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(12f, 24f);
            rects[i] = MakeRect("Enemy Hit Spark", new Vector2(size, size));
            rects[i].anchoredPosition = origin;
            images[i] = rects[i].gameObject.AddComponent<Image>();
            images[i].color = i % 3 == 0 ? Ivory : style.color;
            images[i].raycastTarget = false;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(350f, 750f);
        }

        const float life = 0.55f;
        float t = 0f;
        while (t < life)
        {
            float dt = Time.deltaTime;
            t += dt;
            float alpha = 1f - Mathf.Clamp01(t / life);
            for (int i = 0; i < count; i++)
            {
                velocity[i] += Vector2.down * 900f * dt; // fall a little
                rects[i].anchoredPosition += velocity[i] * dt;
                Color c = images[i].color;
                c.a = alpha;
                images[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) Destroy(rects[i].gameObject);
    }

    private RectTransform MakeRect(string objectName, Vector2 size)
    {
        var obj = new GameObject(objectName, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(popupParent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.SetAsLastSibling(); // draw above the charge screen and the rest of the HUD
        return rect;
    }

    private void OnDisable()
    {
        if (enemyRoutine != null)
        {
            StopCoroutine(enemyRoutine);
            enemyRoutine = null;
            RestoreEnemy();
        }
    }
}
