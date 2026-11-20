using UnityEngine;
using UnityEngine.UI;

public sealed class FlameConeGraphic : MaskableGraphic
{
    private Vector2 origin;
    private Vector2 direction;
    private float length;
    private float nearWidth;
    private float farWidth;
    private bool warning;

    public void SetCone(Vector2 start, Vector2 forward, float distance, float startHalfWidth,
        float endHalfWidth, bool isWarning, Color tint)
    {
        origin = start;
        direction = forward;
        length = distance;
        nearWidth = startHalfWidth;
        farWidth = endHalfWidth;
        warning = isWarning;
        color = tint;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Vector2 side = new Vector2(-direction.y, direction.x);
        Vector2 end = origin + direction * length;
        if (warning)
        {
            Band(mesh, side, end, 1f, color, color);
            Color edge = new Color(1f, 0.74f, 0.25f, Mathf.Min(1f, color.a * 3f));
            Line(mesh, origin - side * nearWidth, end - side * farWidth, 3f, edge);
            Line(mesh, origin + side * nearWidth, end + side * farWidth, 3f, edge);
            return;
        }
        Band(mesh, side, end, 1f, new Color(1f, 0.28f, 0.04f, 0.9f), new Color(1f, 0.12f, 0.02f, 0.7f));
        Band(mesh, side, end, 0.7f, new Color(1f, 0.82f, 0.25f), new Color(1f, 0.48f, 0.06f, 0.95f));
        Band(mesh, side, end, 0.28f, new Color(1f, 0.98f, 0.82f), new Color(1f, 0.85f, 0.38f, 0.9f));
    }

    private void Band(VertexHelper mesh, Vector2 side, Vector2 end, float scale, Color startColor, Color endColor)
    {
        Quad(mesh, origin - side * nearWidth * scale, origin + side * nearWidth * scale,
            end + side * farWidth * scale, end - side * farWidth * scale, startColor, endColor);
    }

    private static void Line(VertexHelper mesh, Vector2 start, Vector2 end, float width, Color tint)
    {
        Vector2 delta = end - start;
        Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
        Quad(mesh, start - side, start + side, end + side, end - side, tint, tint);
    }

    private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color startColor, Color endColor)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(a, startColor, Vector2.zero);
        mesh.AddVert(b, startColor, Vector2.up);
        mesh.AddVert(c, endColor, Vector2.one);
        mesh.AddVert(d, endColor, Vector2.right);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first + 2, first + 3, first);
    }
}
