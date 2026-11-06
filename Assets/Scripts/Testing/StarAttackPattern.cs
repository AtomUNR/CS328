using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rains animated stars into the dodge arena. Each star picks a slight
/// horizontal drift when it spawns, then keeps that path so players can
/// dodge it predictably.
/// </summary>
public sealed class StarAttackPattern : EnemyAttackPattern
{
    [Header("Star animation")]
    [SerializeField] private Sprite[] starFrames;
    [SerializeField, Min(1f)] private float framesPerSecond = 10f;
    [SerializeField] private Color starColor = Color.white;

    [Header("Star rain")]
    [SerializeField, Min(0.05f)] private float waveInterval = 0.35f;
    [SerializeField, Range(1, 12)] private int starsPerWave = 4;
    [SerializeField, Min(1f)] private float fallSpeed = 250f;
    [SerializeField, Min(1f)] private float starRadius = 10f;
    [SerializeField, Min(0f)] private float horizontalDrift = 90f;
    [SerializeField, Range(1, 200)] private int maxStars = 120;

    private sealed class Star
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Position;
        public Vector2 Direction;
        public float AnimationTime;
    }

    private readonly List<Star> stars = new List<Star>();

    private DodgePhaseController owner;
    private RectTransform arena;
    private float waveTimer;

    public override void Initialize(
        DodgePhaseController controller,
        RectTransform arenaArea)
    {
        owner = controller;
        arena = arenaArea;
        waveTimer = 0f;
        ClearProjectiles();
    }

    public override void Tick(
        float deltaTime,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius)
    {
        if (owner == null || arena == null || deltaTime <= 0f)
            return;

        waveTimer -= deltaTime;

        while (waveTimer <= 0f)
        {
            for (int i = 0; i < starsPerWave && stars.Count < maxStars; i++)
                SpawnStar();

            waveTimer += Mathf.Max(0.05f, waveInterval);
        }

        float bottom = -arena.rect.height * 0.5f;
        float halfWidth = arena.rect.width * 0.5f;
        float combinedRadius = starRadius + essenceRadius;
        float combinedRadiusSquared = combinedRadius * combinedRadius;

        for (int i = stars.Count - 1; i >= 0; i--)
        {
            Star star = stars[i];
            Vector2 before = star.Position;
            Vector2 after = before + star.Direction * fallSpeed * deltaTime;

            star.Position = after;
            star.Rect.anchoredPosition = after;

            if (starFrames != null && starFrames.Length > 0)
            {
                star.AnimationTime += deltaTime;
                int frame = Mathf.FloorToInt(
                    star.AnimationTime * Mathf.Max(1f, framesPerSecond))
                    % starFrames.Length;
                star.Image.sprite = starFrames[frame];
            }

            // Compare movement relative to the player so fast motion cannot
            // skip through the collision check between frames.
            Vector2 relativeBefore = before - essenceBefore;
            Vector2 relativeAfter = after - essenceAfter;

            bool hit = DistanceToSegmentSquared(
                relativeBefore,
                relativeAfter) <= combinedRadiusSquared;

            if (hit)
            {
                owner.TryTakeHit();
                RemoveStar(i);
            }
            else if (after.y < bottom - starRadius * 2f ||
                     Mathf.Abs(after.x) > halfWidth + starRadius * 2f)
            {
                RemoveStar(i);
            }
        }
    }

    private void SpawnStar()
    {
        float halfWidth = arena.rect.width * 0.5f;
        float halfHeight = arena.rect.height * 0.5f;
        float spawnRange = Mathf.Max(0f, halfWidth - starRadius);

        Vector2 position = new Vector2(
            Random.Range(-spawnRange, spawnRange),
            halfHeight + starRadius * 2f);

        float drift = Random.Range(-horizontalDrift, horizontalDrift);
        Vector2 direction = new Vector2(drift, -fallSpeed).normalized;

        GameObject instance = new GameObject(
            "StarProjectile",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * starRadius * 2f;
        rect.anchoredPosition = position;

        Image image = instance.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = starColor;

        if (starFrames != null && starFrames.Length > 0)
            image.sprite = starFrames[0];

        stars.Add(new Star
        {
            Rect = rect,
            Image = image,
            Position = position,
            Direction = direction,
            AnimationTime = Random.Range(0f, 0.25f)
        });
    }

    private static float DistanceToSegmentSquared(Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared <= 0.000001f)
            return start.sqrMagnitude;

        float t = Mathf.Clamp01(
            -Vector2.Dot(start, segment) / lengthSquared);

        return (start + segment * t).sqrMagnitude;
    }

    private void RemoveStar(int index)
    {
        if (stars[index].Rect != null)
        {
            stars[index].Rect.gameObject.SetActive(false);
            Destroy(stars[index].Rect.gameObject);
        }

        stars.RemoveAt(index);
    }

    public override void ClearProjectiles()
    {
        for (int i = stars.Count - 1; i >= 0; i--)
            RemoveStar(i);
    }
}
