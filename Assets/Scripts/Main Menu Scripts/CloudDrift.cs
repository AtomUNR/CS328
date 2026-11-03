using UnityEngine;

/// <summary>
/// Adds a slow, looping horizontal drift to a cloud layer in the title scene.
/// Use a separate cloud sprite/layer so the tower and menu remain still.
/// </summary>
public class CloudDrift : MonoBehaviour
{
    [SerializeField, Min(0f)] private float travelDistance = 0.35f;
    [SerializeField, Min(0f)] private float cyclesPerSecond = 0.025f;

    private Vector3 startPosition;

    private void Awake()
    {
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * cyclesPerSecond) + 1f) * 0.5f;
        float offset = Mathf.Lerp(-travelDistance, travelDistance, wave);
        transform.localPosition = startPosition + Vector3.right * offset;
    }
}
