using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Monster))]
public sealed class BossBarrier : MonoBehaviour
{
    [Tooltip("Every listed enemy must be defeated and have its victory rewards collected.")]
    public Monster[] requiredEnemies;
    public BattleManager battleManager;
    public BoxCollider2D blocker;
    public TMP_FontAsset labelFont;

    public bool IsUnlocked { get; private set; }

    private GameObject visuals;
    private TextMeshPro label;
    private int previousRemaining = -1;
    private Texture2D pixel;
    private Sprite square;

    private void Awake()
    {
        if (blocker != null)
        {
            blocker.isTrigger = false;
            blocker.enabled = true;
        }
        if (battleManager == null || blocker == null || requiredEnemies == null || requiredEnemies.Length == 0)
        {
            Debug.LogError("Boss barrier: assign the battle manager, solid blocker, and required enemies.", this);
            enabled = false;
            return;
        }
        foreach (Monster enemy in requiredEnemies)
        {
            if (enemy == null || enemy.gameObject == gameObject)
            {
                Debug.LogError("Boss barrier: each requirement must reference another enemy in the level.", this);
                enabled = false;
                return;
            }
        }
        BuildVisuals();
    }

    private void Update()
    {
        if (IsUnlocked || battleManager.IsInBattle) return;
        int remaining = 0;
        foreach (Monster enemy in requiredEnemies)
        {
            // FinishVictory disables the enemy after collecting its rewards.
            // Fleeing or merely disabling a living enemy cannot open the gate.
            if (enemy == null || !enemy.IsDefeated || enemy.gameObject.activeSelf) remaining++;
        }
        if (remaining != previousRemaining)
        {
            label.text = "DEFEAT ALL ENEMIES  " + (requiredEnemies.Length - remaining) + "/" + requiredEnemies.Length;
            previousRemaining = remaining;
        }
        if (remaining != 0) return;
        IsUnlocked = true;
        blocker.enabled = false;
        visuals.SetActive(false);
    }

    private void MakeBar(string objectName, Vector2 position, Vector2 size, Color color, int order)
    {
        var bar = new GameObject(objectName);
        bar.transform.SetParent(visuals.transform, false);
        bar.transform.localPosition = position;
        bar.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = bar.AddComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.color = color;
        renderer.sortingOrder = order;
    }

    private void BuildVisuals()
    {
        visuals = new GameObject("RuntimeBossBarrier");
        visuals.transform.SetParent(blocker.transform, false);
        visuals.transform.localPosition = blocker.offset;
        pixel = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        float width = blocker.size.x;
        Color frame = new Color32(210, 185, 147, 255);
        MakeBar("Seal", Vector2.zero, new Vector2(width, 0.45f), new Color(0.15f, 0.6f, 0.9f, 0.45f), 8);
        MakeBar("TopRail", new Vector2(0, 0.32f), new Vector2(width, 0.08f), frame, 12);
        MakeBar("BottomRail", new Vector2(0, -0.32f), new Vector2(width, 0.08f), frame, 12);
        int columns = Mathf.Max(1, Mathf.CeilToInt(width / 0.4f));
        for (int i = 0; i <= columns; i++)
            MakeBar("GateBar", new Vector2(Mathf.Lerp(-width * 0.5f, width * 0.5f, (float)i / columns), 0),
                new Vector2(0.05f, 0.65f), frame, 12);

        var textObject = new GameObject("Requirements");
        textObject.transform.SetParent(visuals.transform, false);
        textObject.transform.localPosition = new Vector3(0, 0.78f, 0f);
        label = textObject.AddComponent<TextMeshPro>();
        if (labelFont != null) label.font = labelFont;
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(width, 0.6f);
        label.color = new Color32(166, 233, 255, 255);
        label.outlineWidth = 0.12f;
        label.outlineColor = Color.black;
        label.sortingOrder = 200;
    }

    private void OnDestroy()
    {
        if (visuals != null) Destroy(visuals);
        if (square != null) Destroy(square);
        if (pixel != null) Destroy(pixel);
    }
}
