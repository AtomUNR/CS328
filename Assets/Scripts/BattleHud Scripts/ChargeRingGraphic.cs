using UnityEngine;
using UnityEngine.UI;

// Pixel-stepped rings drawn directly into the UI; no sprite assets needed.
public sealed class ChargeRingGraphic : MaskableGraphic
{
    public const float StartRadius = 14f;
    public const float EndRadius = 92f;
    public const float TargetRadius = 66f;
    public const float GoodHalfWidth = 6f;
    public const float PerfectHalfWidth = 2.6f;
    private float progress;
    private Color32 chargeColor = new Color32(86, 230, 236, 255);
    private static readonly Color32 Ivory = new Color32(245, 236, 213, 255);
    private static readonly Color32 Gold = new Color32(255, 212, 90, 255);
    private static readonly Color32 Band = new Color32(69, 59, 33, 255);

    public void SetCharge(float value, Color tint)
    {
        progress = Mathf.Clamp01(value);
        chargeColor = tint;
        SetVerticesDirty();
    }

    public static float Score(float progress)
    {
        if (progress >= 1f) return 0f;
        float radius = Mathf.Lerp(StartRadius, EndRadius, Mathf.Clamp01(progress));
        float distance = Mathf.Abs(radius - TargetRadius);
        if (distance <= PerfectHalfWidth) return 1.5f;
        return distance <= GoodHalfWidth ? 1f : 0f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        float pixel = size / 192f;
        Vector2 origin = rect.center - Vector2.one * size * 0.5f;
        float chargeRadius = Mathf.Lerp(StartRadius, EndRadius, progress);
        for (int y = 0; y < 192; y++)
        {
            int runStart = 0;
            Color32 previous = new Color32(0, 0, 0, 0);
            for (int x = 0; x <= 192; x++)
            {
                Color32 next = new Color32(0, 0, 0, 0);
                if (x < 192)
                {
                    float distance = new Vector2(x + 0.5f - 96f, y + 0.5f - 96f).magnitude;
                    float targetDistance = Mathf.Abs(distance - TargetRadius);
                    if (targetDistance <= GoodHalfWidth) next = Band;
                    if (Mathf.Abs(targetDistance - GoodHalfWidth) <= 0.6f) next = Ivory;
                    if (targetDistance <= 1f) next = Gold;
                    if (Mathf.Abs(distance - chargeRadius) <= 1.15f) next = chargeColor;
                }
                if (next.r == previous.r && next.g == previous.g &&
                    next.b == previous.b && next.a == previous.a) continue;
                if (previous.a != 0)
                    AddRun(vh, origin, pixel, runStart, x, y, previous);
                previous = next;
                runStart = x;
            }
        }
    }

    private static void AddRun(VertexHelper vh, Vector2 origin, float pixel,
        int from, int to, int y, Color32 tint)
    {
        int index = vh.currentVertCount;
        float left = origin.x + from * pixel, right = origin.x + to * pixel;
        float bottom = origin.y + y * pixel, top = bottom + pixel;
        vh.AddVert(new Vector3(left, bottom, 0f), tint, Vector2.zero);
        vh.AddVert(new Vector3(left, top, 0f), tint, Vector2.zero);
        vh.AddVert(new Vector3(right, top, 0f), tint, Vector2.zero);
        vh.AddVert(new Vector3(right, bottom, 0f), tint, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }
}
