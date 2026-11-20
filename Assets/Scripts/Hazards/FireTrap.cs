using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FireTrap : MonoBehaviour
{
    [Header("Scene player")]
    public CharacterStats playerStats;
    public Collider2D playerCollider;
    [Tooltip("Use the same movement component as the other traps. Disabling it pauses this trap.")]
    public Behaviour playerMovement;

    [Header("Cycle in seconds")]
    [Min(0f)] public float startDelay;
    [Min(0.1f)] public float safeDuration = 2.3f;
    [Min(0.1f)] public float warningDuration = 0.8f;
    [Min(0.1f)] public float burnDuration = 1.25f;
    [Min(1)] public int damage = 3;

    [Header("Feedback")]
    public TMP_FontAsset popupFont;
    public string deathSceneName = "Game Over";
    public UnityEvent onPlayerHit = new UnityEvent();
    public UnityEvent onPlayerDefeated = new UnityEvent();

    [Header("Flames")]
    public Sprite[] flameFrames;
    [Min(1f)] public float framesPerSecond = 10f;
    public int sortingOrder = -50;

    private enum Phase { Safe, Warning, Burning }
    private Phase phase;
    private float elapsed;
    private bool hitThisBurst;
    private BoxCollider2D area;
    private GameObject visuals;
    private SpriteRenderer warning;
    private SpriteRenderer[] flames;
    private SpriteRenderer[] border;
    private Texture2D pixel;
    private Sprite square;

    private static readonly Color ColdMetal = new Color32(118, 108, 105, 255);
    private static readonly Color HotMetal = new Color32(255, 129, 48, 255);

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        if (playerStats == null || playerCollider == null || playerMovement == null ||
            playerCollider.isTrigger || playerCollider.GetComponentInParent<CharacterStats>() != playerStats ||
            playerMovement.GetComponentInParent<CharacterStats>() != playerStats ||
            flameFrames == null || flameFrames.Length == 0)
        {
            Debug.LogError(name + ": assign the player's stats, physical collider, movement, and flame frames.", this);
            enabled = false;
            return;
        }
        BuildVisuals();
    }

    private void OnEnable()
    {
        phase = Phase.Safe;
        elapsed = -Mathf.Max(0f, startDelay);
        hitThisBurst = false;
        if (visuals != null) visuals.SetActive(true);
        RefreshVisuals();
    }

    private bool CanRun()
    {
        return playerStats != null && playerStats.CurrentHP > 0 &&
               playerStats.gameObject.activeInHierarchy && playerMovement != null &&
               playerMovement.isActiveAndEnabled;
    }

    private float PhaseDuration()
    {
        if (phase == Phase.Warning) return Mathf.Max(0.1f, warningDuration);
        if (phase == Phase.Burning) return Mathf.Max(0.1f, burnDuration);
        return Mathf.Max(0.1f, safeDuration);
    }

    private void FixedUpdate()
    {
        if (!CanRun()) return;
        elapsed += Time.fixedDeltaTime;
        while (elapsed >= PhaseDuration())
        {
            elapsed -= PhaseDuration();
            phase = (Phase)(((int)phase + 1) % 3);
            if (phase == Phase.Burning) hitThisBurst = false;
        }

        if (phase != Phase.Burning || hitThisBurst || !area.enabled || !playerCollider.enabled) return;
        // Query the physical shapes so a stationary player is also hit when the vent ignites.
        ColliderDistance2D overlap = area.Distance(playerCollider);
        if (!overlap.isValid || !overlap.isOverlapped) return;
        hitThisBurst = true;
        int before = playerStats.CurrentHP;
        playerStats.TakeDamage(Mathf.Max(1, damage));
        Bounds body = playerCollider.bounds;
        DamagePopup.Spawn(new Vector3(body.center.x, body.max.y + 0.15f, 0f),
            "-" + (before - playerStats.CurrentHP), HotMetal, popupFont, sortingOrder + 100);
        onPlayerHit.Invoke();

        if (playerStats.CurrentHP > 0) return;
        playerMovement.enabled = false;
        if (playerCollider.attachedRigidbody != null)
            playerCollider.attachedRigidbody.linearVelocity = Vector2.zero;
        onPlayerDefeated.Invoke();
        if (string.IsNullOrWhiteSpace(deathSceneName)) return;
        if (Application.CanStreamedLevelBeLoaded(deathSceneName))
        {
            GameOverController.RetrySceneName = gameObject.scene.name;
            Time.timeScale = 1f;
            SceneManager.LoadScene(deathSceneName);
        }
        else Debug.LogError("Fire trap: add '" + deathSceneName + "' to the build scene list.", this);
    }

    private void Update() => RefreshVisuals();

    private void RefreshVisuals()
    {
        if (visuals == null) return;
        bool burning = phase == Phase.Burning;
        bool warming = phase == Phase.Warning;
        float progress = Mathf.Clamp01(elapsed / PhaseDuration());
        float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.Lerp(12f, 30f, progress));
        Color glow = HotMetal;
        glow.a = burning ? 0.4f : warming ? Mathf.Lerp(0.08f, 0.55f, pulse) : 0f;
        warning.color = glow;
        foreach (SpriteRenderer edge in border)
            edge.color = burning ? HotMetal : warming ? Color.Lerp(ColdMetal, HotMetal, pulse) : ColdMetal;

        for (int i = 0; i < flames.Length; i++)
        {
            SpriteRenderer flame = flames[i];
            flame.enabled = burning;
            if (!burning) continue;
            int frame = (Mathf.FloorToInt(elapsed * Mathf.Max(1f, framesPerSecond)) + i * 2) % flameFrames.Length;
            flame.sprite = flameFrames[frame];
            if (flame.sprite == null) continue;
            float ramp = Mathf.Min(Mathf.Clamp01(elapsed / 0.1f), Mathf.Clamp01((burnDuration - elapsed) / 0.16f));
            Vector2 spriteSize = flame.sprite.bounds.size;
            flame.transform.localScale = new Vector3(area.size.x / 3f / spriteSize.x,
                1.45f * ramp / spriteSize.y, 1f);
            // Keep each flame rooted in the vent as its height changes.
            flame.transform.localPosition = new Vector3((i - 1) * area.size.x / 3f,
                -area.size.y * 0.3f + 1.45f * ramp * 0.5f, 0f);
        }
    }

    private SpriteRenderer MakeSprite(string objectName, Vector2 position, Vector2 size, Color color, int order)
    {
        var obj = new GameObject(objectName);
        obj.transform.SetParent(visuals.transform, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    private void BuildVisuals()
    {
        visuals = new GameObject("RuntimeFireVent");
        visuals.transform.SetParent(transform, false);
        visuals.transform.localPosition = area.offset;
        pixel = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        Vector2 size = area.size;
        MakeSprite("VentPlate", Vector2.zero, size, new Color32(25, 25, 39, 255), sortingOrder);
        border = new[]
        {
            MakeSprite("Top", new Vector2(0, size.y * 0.5f), new Vector2(size.x, 0.06f), ColdMetal, sortingOrder + 1),
            MakeSprite("Bottom", new Vector2(0, -size.y * 0.5f), new Vector2(size.x, 0.06f), ColdMetal, sortingOrder + 1),
            MakeSprite("Left", new Vector2(-size.x * 0.5f, 0), new Vector2(0.06f, size.y), ColdMetal, sortingOrder + 1),
            MakeSprite("Right", new Vector2(size.x * 0.5f, 0), new Vector2(0.06f, size.y), ColdMetal, sortingOrder + 1)
        };
        for (int i = 0; i < 7; i++)
            MakeSprite("Grate", new Vector2((i - 3) * size.x / 8f, 0),
                new Vector2(0.035f, size.y - 0.12f), ColdMetal, sortingOrder + 1);
        warning = MakeSprite("HeatGlow", Vector2.zero, size, Color.clear, sortingOrder + 2);
        flames = new SpriteRenderer[3];
        for (int i = 0; i < flames.Length; i++)
            flames[i] = MakeSprite("Flame", Vector2.zero, Vector2.one, Color.white, sortingOrder + 3);
        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (visuals != null) visuals.SetActive(false);
    }

    private void OnDestroy()
    {
        if (visuals != null) Destroy(visuals);
        if (square != null) Destroy(square);
        if (pixel != null) Destroy(pixel);
    }
}
