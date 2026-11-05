using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class FallingAttackPattern : EnemyAttackPattern
{
    [Header("Appearance")]
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Color projectileColor = Color.white;

    [Header("Pattern")]
    [SerializeField, Min(1f)] private float fallSpeed = 220f;
    [SerializeField, Min(0.05f)] private float spawnInterval = 0.4f;
    [SerializeField, Min(1f)] private float projectileRadius = 10f;

    private sealed class Projectile
    {
        public RectTransform Rect;
        public Vector2 Position;
    }

    private readonly List<Projectile> projectiles =
        new List<Projectile>();

    private DodgePhaseController owner;
    private RectTransform arena;
    private float spawnTimer;

    public override void Initialize(
        DodgePhaseController controller,
        RectTransform arenaArea)
    {
        owner = controller;
        arena = arenaArea;
        spawnTimer = 0f;
    }

    // The dodge controller calls this only during the active attack.
    public override void Tick(
        float deltaTime,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius)
    {
        if (owner == null || arena == null)
            return;

        spawnTimer -= deltaTime;

        if (spawnTimer <= 0f)
        {
            SpawnProjectile();
            spawnTimer += Mathf.Max(0.05f, spawnInterval);
        }

        float bottom = -arena.rect.height * 0.5f;

        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];

            Vector2 before = projectile.Position;
            Vector2 after =
                before + Vector2.down * fallSpeed * deltaTime;

            projectile.Position = after;
            projectile.Rect.anchoredPosition = after;

            // Test relative movement to avoid skipping a collision
            // when either the projectile or essence moves quickly.
            Vector2 relativeBefore = before - essenceBefore;
            Vector2 relativeAfter = after - essenceAfter;

            float combinedRadius = projectileRadius + essenceRadius;

            bool hit = DistanceToSegmentSquared(
                relativeBefore,
                relativeAfter
            ) <= combinedRadius * combinedRadius;

            if (hit)
            {
                owner.TryTakeHit();
                RemoveProjectile(i);
            }
            else if (after.y < bottom - projectileRadius)
            {
                RemoveProjectile(i);
            }
        }
    }

    private void SpawnProjectile()
    {
        float halfWidth = arena.rect.width * 0.5f;
        float halfHeight = arena.rect.height * 0.5f;

        float horizontalRange =
            Mathf.Max(0f, halfWidth - projectileRadius);

        Vector2 position = new Vector2(
            Random.Range(-horizontalRange, horizontalRange),
            halfHeight + projectileRadius
        );

        GameObject projectileObject = new GameObject(
            "EnemyProjectile",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform rect =
            projectileObject.GetComponent<RectTransform>();

        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * projectileRadius * 2f;
        rect.anchoredPosition = position;

        Image image = projectileObject.GetComponent<Image>();
        image.sprite = projectileSprite;
        image.color = projectileColor;
        image.raycastTarget = false;

        projectiles.Add(new Projectile
        {
            Rect = rect,
            Position = position
        });
    }

    private static float DistanceToSegmentSquared(
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared <= 0.000001f)
            return start.sqrMagnitude;

        float t = Mathf.Clamp01(
            -Vector2.Dot(start, segment) / lengthSquared
        );

        return (start + segment * t).sqrMagnitude;
    }

    private void RemoveProjectile(int index)
    {
        if (projectiles[index].Rect != null)
        {
            projectiles[index].Rect.gameObject.SetActive(false);
            Destroy(projectiles[index].Rect.gameObject);
        }

        projectiles.RemoveAt(index);
    }

    public override void ClearProjectiles()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
            RemoveProjectile(i);
    }
}