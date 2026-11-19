using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TowerSpikeTrap : MonoBehaviour
{
    [Header("Scene player (assign on the scene instance)")]
    public CharacterStats playerStats;
    public Collider2D playerCollider;
    [Tooltip("Assign the PlayerMovement component. Traps pause while it is disabled for battle.")]
    public Behaviour playerMovement;

    [Header("Cycle in seconds")]
    [Min(0)] public float startDelay;
    [Min(0.1f)] public float safeDuration = 2f;
    [Min(0.1f)] public float warningDuration = 0.7f;
    [Min(0.1f)] public float raisedDuration = 1f;
    [Min(1)] public int damage = 3;

    [Header("Death")]
    [Tooltip("Exact scene name, included in the build scene list. Leave empty to handle death through On Player Defeated.")]
    public string deathSceneName = "Game Over";
    public UnityEvent onPlayerHit = new UnityEvent();
    public UnityEvent onPlayerDefeated = new UnityEvent();

    [Header("Hit feedback")]
    [Tooltip("Font for the floating damage number. Empty uses TextMeshPro's default font.")]
    public TMPro.TMP_FontAsset popupFont;
    [Tooltip("Colour of the damage number and the player's flash when hit.")]
    public Color hitColor = new Color32(239, 115, 124, 255);

    [Header("Visuals")]
    [Tooltip("Optional replacement spike sprite. Empty uses the built-in animated metal spikes.")]
    public Sprite spikeSprite;
    [Tooltip("Base draw order of the trap. Keep it below the player (0) and above the floor (-100) so the player is drawn over the spikes.")]
    public int sortingOrder = -50;
    [Tooltip("Glow color for the warning, matched to the blue torch flames.")]
    public Color glowColor = new Color(0.3f, 0.8f, 1f, 0.6f);
    [Tooltip("Spike columns per row for the built-in spikes.")]
    [Range(3, 16)] public int spikeColumns = 8;
    [Range(1, 4)] public int spikeRows = 2;

    private enum Phase { Safe, Warning, Raised }
    private Phase phase;
    private float elapsed;
    private bool hitThisRise;
    private BoxCollider2D area;
    private GameObject visuals;
    private GameObject spikes;
    private SpriteRenderer warning;
    private SpriteRenderer plate;
    private SpriteRenderer[] border;
    private Transform[] tips;
    private float[] tipDelay;
    private float tipBaseWidth, tipMaxHeight;
    private Texture2D pixel;
    private Sprite square;
    private Mesh spikeMesh;
    private Material spikeMaterial;

    // Palette sampled from the tower interior art: slate stone, tan trim, black outlines, blue flame.
    private static readonly Color PlateColor = new Color(0.1f, 0.1f, 0.18f, 0.92f);
    private static readonly Color HoleColor = new Color(0.02f, 0.02f, 0.05f, 1f);
    private static readonly Color BorderSafe = new Color(0.62f, 0.52f, 0.42f, 1f);   // dull tan trim
    private static readonly Color BorderHot = new Color(0.55f, 0.92f, 1f, 1f);       // flame blue
    private static readonly Color Outline = new Color(0.03f, 0.03f, 0.06f, 1f);
    private const int PixelSteps = 7; // spike height snaps to steps for a pixel-art feel

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.enabled = true; // The interior builder intentionally left these disabled.
        if (playerStats == null || playerCollider == null || playerMovement == null)
        {
            Debug.LogError(name + ": assign Player Stats, Player Collider, and Player Movement.", this);
            enabled = false;
            return;
        }
        BuildVisuals();
    }

    private void OnEnable()
    {
        elapsed = -Mathf.Max(0, startDelay);
        phase = Phase.Safe;
        hitThisRise = false;
        if (visuals != null) visuals.SetActive(true);
        RefreshVisuals();
    }

    private void Update()
    {
        if (!CanRun()) return;
        elapsed += Time.deltaTime;
        float duration = Duration();
        while (elapsed >= duration)
        {
            elapsed -= duration;
            phase = (Phase)(((int)phase + 1) % 3);
            if (phase == Phase.Raised) hitThisRise = false;
            duration = Duration();
        }
        RefreshVisuals();
    }

    private float Duration()
    {
        if (phase == Phase.Warning) return Mathf.Max(0.1f, warningDuration);
        if (phase == Phase.Raised) return Mathf.Max(0.1f, raisedDuration);
        return Mathf.Max(0.1f, safeDuration);
    }

    private bool CanRun()
    {
        return playerStats != null && playerStats.CurrentHP > 0 &&
               playerStats.gameObject.activeInHierarchy && playerMovement != null &&
               playerMovement.isActiveAndEnabled;
    }

    private void FixedUpdate()
    {
        if (!CanRun() || phase != Phase.Raised || hitThisRise ||
            playerCollider == null || !playerCollider.enabled || !area.enabled) return;

        // Explicit shape overlap also detects a player standing still as spikes rise.
        // Only the assigned player's physical collider can receive damage.
        ColliderDistance2D overlap = area.Distance(playerCollider);
        if (!overlap.isValid || !overlap.isOverlapped) return;
        hitThisRise = true;
        int hpBefore = playerStats.CurrentHP;
        playerStats.TakeDamage(Mathf.Max(1, damage));
        ShowHitFeedback(hpBefore - playerStats.CurrentHP);
        Debug.Log($"{name}: player HP {playerStats.CurrentHP}/{playerStats.MaxHP}", this);
        onPlayerHit.Invoke();
        if (playerStats.CurrentHP > 0) return;

        playerMovement.enabled = false;
        Rigidbody2D body = playerCollider.attachedRigidbody;
        if (body != null) body.linearVelocity = Vector2.zero;
        onPlayerDefeated.Invoke();
        if (string.IsNullOrWhiteSpace(deathSceneName)) return;
        if (Application.CanStreamedLevelBeLoaded(deathSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(deathSceneName);
        }
        else
            Debug.LogError("Spike death: add the scene '" + deathSceneName +
                "' to your build scene list, or correct Death Scene Name on the traps.", this);
    }

    // ---------------------------------------------------------------- visuals

    // Floating damage number above the player plus a short red flash on their sprite.
    private void ShowHitFeedback(int amount)
    {
        if (amount <= 0 || playerCollider == null) return;
        Bounds body = playerCollider.bounds;
        DamagePopup.Spawn(new Vector3(body.center.x, body.max.y + 0.15f, 0f),
            "-" + amount, hitColor, popupFont, sortingOrder + 100);

        SpriteRenderer sprite = playerCollider.GetComponentInParent<SpriteRenderer>();
        if (sprite == null) sprite = playerCollider.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) StartCoroutine(FlashSprite(sprite));
    }

    private System.Collections.IEnumerator FlashSprite(SpriteRenderer sprite)
    {
        Color original = sprite.color;
        const int flashes = 3;
        for (int i = 0; i < flashes; i++)
        {
            if (sprite == null) yield break;
            sprite.color = hitColor;
            yield return new WaitForSeconds(0.06f);
            if (sprite == null) yield break;
            sprite.color = original;
            yield return new WaitForSeconds(0.06f);
        }
        if (sprite != null) sprite.color = original;
    }

    private void RefreshVisuals()
    {
        if (warning == null) return;
        float duration = Duration();
        float t = Mathf.Max(0f, elapsed);
        float progress = Mathf.Clamp01(t / duration);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (phase == Phase.Warning ? 22f : 3f));

        // Pit floor and hazard border are always visible so the trap reads as dangerous.
        Color edge = BorderSafe;
        Color fill = glowColor;
        fill.a = 0f;

        if (phase == Phase.Warning)
        {
            // Pulse faster as the spikes get closer to firing.
            float rate = Mathf.Lerp(10f, 34f, progress);
            pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * rate);
            fill = glowColor;
            fill.a *= Mathf.Lerp(0.35f, 1f, pulse) * Mathf.Lerp(0.6f, 1f, progress);
            edge = Color.Lerp(BorderSafe, BorderHot, 0.4f + 0.6f * pulse);
        }
        else if (phase == Phase.Raised)
        {
            float flash = Mathf.Clamp01(1f - t / 0.18f);
            fill = Color.Lerp(glowColor, new Color(0.9f, 0.98f, 1f), flash);
            fill.a = 0.7f * flash;
            edge = Color.Lerp(BorderHot, BorderSafe, progress);
        }

        warning.color = fill;
        warning.enabled = fill.a > 0.01f;
        for (int i = 0; i < border.Length; i++) border[i].color = edge;

        if (tips == null)
        {
            // Custom sprite path: show the sprite while raised.
            if (spikes != null) spikes.SetActive(phase == Phase.Raised);
            return;
        }

        for (int i = 0; i < tips.Length; i++)
        {
            float h = TipHeight(i, t, duration, progress);
            bool visible = h > 0.004f;
            if (tips[i].gameObject.activeSelf != visible) tips[i].gameObject.SetActive(visible);
            if (visible)
            {
                float stepped = Mathf.Ceil(h * PixelSteps) / PixelSteps;
                tips[i].localScale = new Vector3(tipBaseWidth, tipMaxHeight * stepped, 1f);
            }
        }
    }

    private float TipHeight(int index, float t, float duration, float progress)
    {
        if (phase == Phase.Safe) return 0f;
        if (phase == Phase.Warning)
        {
            // Tips twitch up out of their holes.
            float twitch = 0.03f * Mathf.Sin(Time.time * 55f + index * 1.7f);
            return Mathf.Clamp01(Mathf.Lerp(0.04f, 0.2f, progress) + twitch * progress);
        }

        const float riseTime = 0.09f, startHeight = 0.2f;
        float retract = Mathf.Min(0.22f, duration * 0.3f);
        float local = t - tipDelay[index];
        if (local < 0f) return startHeight;
        if (local < riseTime)
        {
            // Fast thrust with a little overshoot.
            float s = local / riseTime - 1f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float eased = 1f + c3 * s * s * s + c1 * s * s;
            return Mathf.LerpUnclamped(startHeight, 1f, eased);
        }
        float retractStart = duration - retract;
        if (t > retractStart)
        {
            float r = Mathf.Clamp01((t - retractStart) / retract);
            return 1f - r * r;
        }
        return 1f;
    }

    private SpriteRenderer MakeSprite(string objectName, Transform parent, Vector3 localPos, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void BuildVisuals()
    {
        visuals = new GameObject("RuntimeSpikeVisuals");
        visuals.transform.SetParent(transform, false);
        visuals.transform.localPosition = area.offset;
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);

        Vector2 size = area.size;
        plate = MakeSprite("Pit", visuals.transform, Vector3.zero, size, PlateColor, sortingOrder - 1);

        // Hazard border: four strips around the trap area.
        const float thick = 0.07f;
        border = new SpriteRenderer[4];
        border[0] = MakeSprite("EdgeTop", visuals.transform, new Vector3(0, (size.y - thick) * 0.5f, 0), new Vector2(size.x, thick), BorderSafe, sortingOrder + 2);
        border[1] = MakeSprite("EdgeBottom", visuals.transform, new Vector3(0, -(size.y - thick) * 0.5f, 0), new Vector2(size.x, thick), BorderSafe, sortingOrder + 2);
        border[2] = MakeSprite("EdgeLeft", visuals.transform, new Vector3(-(size.x - thick) * 0.5f, 0, 0), new Vector2(thick, size.y), BorderSafe, sortingOrder + 2);
        border[3] = MakeSprite("EdgeRight", visuals.transform, new Vector3((size.x - thick) * 0.5f, 0, 0), new Vector2(thick, size.y), BorderSafe, sortingOrder + 2);

        // Glow overlay for warning and the strike flash.
        warning = MakeSprite("Warning", visuals.transform, Vector3.zero, size, glowColor, sortingOrder + 1);

        spikes = new GameObject("RaisedSpikes");
        spikes.transform.SetParent(visuals.transform, false);

        if (spikeSprite != null)
        {
            SpriteRenderer sprite = spikes.AddComponent<SpriteRenderer>();
            sprite.sprite = spikeSprite;
            sprite.sortingOrder = sortingOrder + 3;
            Vector3 spriteSize = spikeSprite.bounds.size;
            spikes.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1);
            spikes.transform.localPosition = -Vector3.Scale(spikeSprite.bounds.center, spikes.transform.localScale);
            return;
        }

        // Spike mesh: base at y=0, tip at y=1. A black outline triangle sits behind two
        // flat-shaded faces in the tower's slate-blue stone tones, with a pale tan-steel tip.
        Color baseDark = new Color(0.2f, 0.21f, 0.33f);
        Color baseLight = new Color(0.36f, 0.38f, 0.52f);
        Color tipLeft = new Color(0.62f, 0.64f, 0.78f);
        Color tipRight = new Color(0.9f, 0.84f, 0.72f);
        spikeMesh = new Mesh();
        spikeMesh.vertices = new[]
        {
            new Vector3(-0.56f, -0.04f, 0), new Vector3(0.56f, -0.04f, 0), new Vector3(0, 1.08f, 0),
            new Vector3(-0.5f, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0),
            new Vector3(0, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0, 1, 0)
        };
        spikeMesh.colors = new[]
        {
            Outline, Outline, Outline,
            baseDark, baseDark, tipLeft, baseLight, baseLight, tipRight
        };
        spikeMesh.uv = new Vector2[9];
        spikeMesh.triangles = new[] { 0, 2, 1, 3, 5, 4, 6, 8, 7 };
        spikeMesh.RecalculateBounds();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) { Debug.LogError("Sprites/Default shader missing.", this); return; }
        spikeMaterial = new Material(shader);
        spikeMaterial.color = Color.white;

        int columns = Mathf.Max(1, spikeColumns), rows = Mathf.Max(1, spikeRows);
        float cellW = size.x / columns, cellH = size.y / rows;
        tipBaseWidth = cellW * 0.9f;
        tipMaxHeight = cellH * 1.1f;
        tips = new Transform[columns * rows];
        tipDelay = new float[columns * rows];
        int n = 0;
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Vector3 basePos = new Vector3(
                    ((column + 0.5f) / columns - 0.5f) * size.x,
                    (float)row / rows * size.y - size.y * 0.5f + cellH * 0.12f, 0);

                // Dark hole the spike emerges from.
                MakeSprite("Hole", visuals.transform, basePos + new Vector3(0, cellH * 0.02f, 0),
                    new Vector2(cellW * 0.55f, cellH * 0.16f), HoleColor, sortingOrder);

                GameObject tip = new GameObject("Spike");
                tip.transform.SetParent(spikes.transform, false);
                tip.transform.localPosition = basePos;
                tip.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
                MeshRenderer meshRenderer = tip.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = spikeMaterial;
                meshRenderer.sortingOrder = sortingOrder + 3 + row;
                tip.SetActive(false);
                tips[n] = tip.transform;
                // Slight wave across the trap so the spikes do not fire in perfect lockstep.
                tipDelay[n] = (column + row * 0.5f) * 0.012f;
                n++;
            }
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
        if (spikeMesh != null) Destroy(spikeMesh);
        if (spikeMaterial != null) Destroy(spikeMaterial);
    }
}
