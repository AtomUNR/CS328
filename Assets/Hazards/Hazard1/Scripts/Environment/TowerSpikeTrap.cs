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

    [Header("Visuals")]
    [Tooltip("Optional replacement spike sprite. Empty uses simple triangular test spikes.")]
    public Sprite spikeSprite;
    public int sortingOrder = 10;
    public Color warningColor = new Color(1f, 0.65f, 0.15f, 0.6f);

    private enum Phase { Safe, Warning, Raised }
    private Phase phase;
    private float elapsed;
    private bool hitThisRise;
    private BoxCollider2D area;
    private GameObject visuals;
    private GameObject spikes;
    private SpriteRenderer warning;
    private Texture2D pixel;
    private Sprite square;
    private Mesh spikeMesh;
    private Material spikeMaterial;

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
        playerStats.TakeDamage(Mathf.Max(1, damage));
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

    private void RefreshVisuals()
    {
        if (spikes != null) spikes.SetActive(phase == Phase.Raised);
        if (warning != null)
        {
            warning.gameObject.SetActive(phase == Phase.Warning);
            Color tint = warningColor;
            tint.a *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(elapsed * 12f));
            warning.color = tint;
        }
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
        GameObject flash = new GameObject("Warning");
        flash.transform.SetParent(visuals.transform, false);
        flash.transform.localScale = new Vector3(area.size.x, area.size.y, 1);
        warning = flash.AddComponent<SpriteRenderer>();
        warning.sprite = square;
        warning.sortingOrder = sortingOrder;
        spikes = new GameObject("RaisedSpikes");
        spikes.transform.SetParent(visuals.transform, false);

        if (spikeSprite != null)
        {
            SpriteRenderer sprite = spikes.AddComponent<SpriteRenderer>();
            sprite.sprite = spikeSprite;
            sprite.sortingOrder = sortingOrder + 1;
            Vector3 size = spikeSprite.bounds.size;
            spikes.transform.localScale = new Vector3(area.size.x / size.x, area.size.y / size.y, 1);
            spikes.transform.localPosition = -Vector3.Scale(spikeSprite.bounds.center, spikes.transform.localScale);
            return;
        }

        // Code-built placeholder geometry: replace with your own sprite whenever ready.
        spikeMesh = new Mesh();
        spikeMesh.vertices = new[] { new Vector3(-0.45f, -0.45f, 0), new Vector3(0.45f, -0.45f, 0), new Vector3(0, 0.45f, 0) };
        spikeMesh.colors = new[] { Color.white, Color.white, Color.white };
        spikeMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
        spikeMesh.triangles = new[] { 0, 1, 2 };
        spikeMesh.RecalculateBounds();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) { Debug.LogError("Sprites/Default shader missing.", this); return; }
        spikeMaterial = new Material(shader);
        spikeMaterial.color = new Color(0.95f, 0.93f, 0.82f);
        const int columns = 6, rows = 2;
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                GameObject tip = new GameObject("Spike");
                tip.transform.SetParent(spikes.transform, false);
                tip.transform.localPosition = new Vector3(
                    ((column + 0.5f) / columns - 0.5f) * area.size.x,
                    ((row + 0.5f) / rows - 0.5f) * area.size.y, 0);
                tip.transform.localScale = new Vector3(area.size.x / columns, area.size.y / rows, 1);
                tip.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
                MeshRenderer meshRenderer = tip.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = spikeMaterial;
                meshRenderer.sortingOrder = sortingOrder + 1;
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
