using System;
using UnityEngine;

public class AttackTimer : MonoBehaviour
{
    [Header("UI references")]
    [SerializeField] private GameObject timingPanel;
    [SerializeField] private RectTransform meterArea;
    [SerializeField] private RectTransform movingMarker;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float sweepDuration = 1.5f;

    [Header("Marker travel")]
    [Tooltip("Shrinks the marker's travel range on each side, in MeterArea units. Raise it if the marker still pokes past the frame's inner edge.")]
    [SerializeField, Min(0f)] private float edgePadding = 0f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float edgeMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float centerMultiplier = 1.5f;
    [Tooltip("Width of the 'perfect' zone as a fraction of the meter (0.05 = 5%). Anywhere inside it gives exactly the center multiplier. Match it to the width of the center (cyan) bar.")]
    [SerializeField, Range(0f, 0.5f)] private float perfectZoneWidth = 0.06f;

    // Zero means a missed attack.
    public event Action<float> AttackFinished;

    private bool isRunning;
    private float startedAt;
    private int startedFrame;

    private void Awake()
    {
        CancelAttack();
    }

    public bool BeginAttack()
    {
        if (timingPanel == null ||
            meterArea == null ||
            movingMarker == null)
        {
            Debug.LogError(
                "Assign the timing panel, meter area, and moving marker.",
                this
            );

            return false;
        }

        if (isRunning)
            return false;

        sweepDuration = Mathf.Max(0.1f, sweepDuration);

        timingPanel.SetActive(true);

        // Ensure the panel's layout is ready before positioning the marker.
        Canvas.ForceUpdateCanvases();

        startedAt = Time.unscaledTime;
        startedFrame = Time.frameCount;
        isRunning = true;

        PositionMarker(0f);
        return true;
    }

    private void Update()
    {
        if (!isRunning)
            return;

        PositionMarker(GetProgress());
    }

    public void ConfirmHit()
    {
        if (!isRunning || Time.frameCount == startedFrame)
            return;

        float progress = GetProgress();
        PositionMarker(progress);

        FinishAttack(GetMultiplier(progress));
    }

    private float GetMultiplier(float progress)
    {
        float edge = Mathf.Max(0f, edgeMultiplier);
        float center = Mathf.Max(0f, centerMultiplier);

        // Distance from the exact center, 0 to 0.5.
        float distance = Mathf.Abs(progress - 0.5f);
        float halfZone = perfectZoneWidth * 0.5f;

        // Anywhere in the perfect zone is exactly the same damage every time.
        if (distance <= halfZone)
            return center;

        // Outside it, fall from the center value (at the zone's edge) to the edge value (at the very end).
        float accuracy = 1f - (distance - halfZone) / Mathf.Max(0.0001f, 0.5f - halfZone);
        return Mathf.Lerp(edge, center, Mathf.Clamp01(accuracy));
    }

    private float GetProgress()
    {
        float elapsed = Time.unscaledTime - startedAt;

        return Mathf.PingPong(
            elapsed / sweepDuration,
            1f
        );
    }

    private void PositionMarker(float progress)
    {
        // Work in MeterArea's local space so scale, pivot and anchors on the marker can't push it off the bar.
        float meterScale = Mathf.Abs(meterArea.lossyScale.x);
        if (meterScale < 0.0001f) return;

        float markerWidth =
            movingMarker.rect.width * Mathf.Abs(movingMarker.lossyScale.x) / meterScale;

        Rect meter = meterArea.rect;
        float pivotX = movingMarker.pivot.x;

        // Keep the marker's left and right edges inside the meter (minus padding).
        float minX = meter.xMin + edgePadding + markerWidth * pivotX;
        float maxX = meter.xMax - edgePadding - markerWidth * (1f - pivotX);
        if (maxX < minX) minX = maxX = (minX + maxX) * 0.5f;

        Vector3 local = meterArea.InverseTransformPoint(movingMarker.position);
        local.x = Mathf.Lerp(minX, maxX, progress);
        movingMarker.position = meterArea.TransformPoint(local);

        // Vertical placement exactly as the original script did it (centered anchors/pivot).
        Vector2 anchored = movingMarker.anchoredPosition;
        anchored.y = 0f;
        movingMarker.anchoredPosition = anchored;
    }

    private void FinishAttack(float multiplier)
    {
        // Stop first so damage can only be resolved once.
        isRunning = false;

        if (timingPanel != null)
            timingPanel.SetActive(false);

        AttackFinished?.Invoke(multiplier);
    }

    public void CancelAttack()
    {
        isRunning = false;

        if (timingPanel != null)
            timingPanel.SetActive(false);
    }

    private void OnDisable()
    {
        CancelAttack();
    }
}