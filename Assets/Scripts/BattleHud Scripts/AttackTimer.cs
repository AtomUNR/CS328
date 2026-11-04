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

    [Header("Damage")]
    [SerializeField, Min(0f)] private float edgeMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float centerMultiplier = 1.5f;

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

        float progress = GetProgress();
        PositionMarker(progress);

        if (progress >= 1f)
            FinishAttack(0f);
    }

    public void ConfirmHit()
    {
        if (!isRunning || Time.frameCount == startedFrame)
            return;

        float progress = GetProgress();
        PositionMarker(progress);

        if (progress >= 1f)
        {
            FinishAttack(0f);
            return;
        }

        // 0 at either edge, 1 at the center.
        float accuracy =
            1f - Mathf.Abs(progress - 0.5f) * 2f;

        float multiplier = Mathf.Lerp(
            Mathf.Max(0f, edgeMultiplier),
            Mathf.Max(0f, centerMultiplier),
            accuracy
        );

        FinishAttack(multiplier);
    }

    private float GetProgress()
    {
        return Mathf.Clamp01(
            (Time.unscaledTime - startedAt) / sweepDuration
        );
    }

    private void PositionMarker(float progress)
    {
        // MovingMarker must be a direct child of MeterArea,
        // with centered anchors and pivot.
        float availableWidth = Mathf.Max(
            0f,
            meterArea.rect.width - movingMarker.rect.width
        );

        float x = Mathf.Lerp(
            -availableWidth * 0.5f,
            availableWidth * 0.5f,
            progress
        );

        movingMarker.anchoredPosition = new Vector2(x, 0f);
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