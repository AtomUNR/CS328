using TMPro;
using UnityEngine;

// Floating "-3" style number that pops out of a character, rises, and fades away.
// Built entirely in code so no prefab is needed; call DamagePopup.Spawn(...).
public sealed class DamagePopup : MonoBehaviour
{
    private const float Lifetime = 1.05f;
    private const float RiseDistance = 1.3f;

    private TextMeshPro label;
    private Color baseColor;
    private Vector3 start;
    private float age;

    public static DamagePopup Spawn(Vector3 position, string message, Color color,
        TMP_FontAsset font, int sortingOrder = 200)
    {
        GameObject obj = new GameObject("DamagePopup");
        obj.transform.position = position;
        DamagePopup popup = obj.AddComponent<DamagePopup>();

        TextMeshPro text = obj.AddComponent<TextMeshPro>();
        if (font != null) text.font = font;
        text.text = message;
        text.fontSize = 9f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = color;
        text.sortingOrder = sortingOrder;
        text.rectTransform.sizeDelta = new Vector2(6f, 2f);
        // Dark outline keeps the number readable over any floor colour.
        text.outlineWidth = 0.12f; // thin: a thick outline blobs the small holes in digits
        text.outlineColor = new Color32(10, 8, 12, 255);

        popup.label = text;
        popup.baseColor = color;
        popup.start = position;
        return popup;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);

        // Quick upward burst that slows down.
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = start + Vector3.up * (RiseDistance * eased);

        // Pop in slightly oversized, then settle.
        float scale = t < 0.12f ? Mathf.Lerp(0.5f, 1.35f, t / 0.12f)
                                : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((t - 0.12f) / 0.2f));
        transform.localScale = Vector3.one * scale;

        // Fade over the last part of its life.
        float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        Color c = baseColor;
        c.a = alpha;
        label.color = c;
        label.outlineColor = new Color32(10, 8, 12, (byte)Mathf.RoundToInt(255f * alpha));

        if (age >= Lifetime) Destroy(gameObject);
    }
}
