using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Wizard attack pattern.
// Fireballs aim at the player's position when spawned,
// then travel straight without tracking the player.
public sealed class HellfireRainPattern : EnemyAttackPattern
{
    [Header("Fireball animation (individual PNG sprites)")]
    [SerializeField] private Sprite[] fireballFrames;
    [SerializeField, Min(1f)] private float framesPerSecond = 10f;
    [SerializeField] private Color fireballColor = Color.white;

    [Header("Hellfire rain")]
    [SerializeField, Min(0.05f)] private float volleyInterval = 0.28f;
    [SerializeField, Range(1, 8)] private int fireballsPerVolley = 4;
    [SerializeField, Min(1f)] private float fallSpeed = 260f;
    [SerializeField, Min(0f)] private float horizontalTrackingSpeed = 145f;
    [SerializeField, Min(1f)] private float fireballRadius = 11f;
    [SerializeField, Min(0f)] private float horizontalScatter = 100f;
    [SerializeField, Range(1, 200)] private int maxFireballs = 120;

    private sealed class Fireball
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Position;
        public Vector2 Direction;
        public float FallSpeed;
        public float AnimationTime;
    }

    private readonly List<Fireball> fireballs = new List<Fireball>();

    private DodgePhaseController owner;
    private RectTransform arena;
    private float volleyTimer;

    public override void Initialize(
        DodgePhaseController controller,
        RectTransform arenaArea)
    {
        owner = controller;
        arena = arenaArea;
        volleyTimer = 0f;

        ClearProjectiles();
    }

    // DodgePhaseController calls Tick during the active attack.
    public override void Tick(
        float deltaTime,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius)
    {
        if (owner == null || arena == null || deltaTime <= 0f)
            return;

        // Spawn fireballs
        volleyTimer -= deltaTime;

        while (volleyTimer <= 0f)
        {
            for (int i = 0;
                 i < fireballsPerVolley &&
                 fireballs.Count < maxFireballs;
                 i++)
            {
                SpawnFireball(essenceAfter);
            }

            volleyTimer += Mathf.Max(0.05f, volleyInterval);
        }

        float bottom = -arena.rect.height * 0.5f;
        float halfWidth = arena.rect.width * 0.5f;

        float collisionRadius = fireballRadius + essenceRadius;
        float collisionRadiusSquared =
            collisionRadius * collisionRadius;

        // Update existing fireballs
        for (int i = fireballs.Count - 1; i >= 0; i--)
        {
            Fireball fireball = fireballs[i];

            Vector2 before = fireball.Position;

            // Move in the original firing direction.
            // The player can move away, but the fireball
            // will NOT change direction to follow them.
            Vector2 after = before +
                fireball.Direction *
                fireball.FallSpeed *
                deltaTime;

            fireball.Position = after;
            fireball.Rect.anchoredPosition = after;

            // Animate the fireball sprites
            if (fireballFrames != null &&
                fireballFrames.Length > 0)
            {
                fireball.AnimationTime += deltaTime;

                int frame = Mathf.FloorToInt(
                    fireball.AnimationTime * framesPerSecond)
                    % fireballFrames.Length;

                fireball.Image.sprite = fireballFrames[frame];
            }

            // Collision detection
            // Prevent fast fireballs from passing through player.
            Vector2 relativeBefore = before - essenceBefore;
            Vector2 relativeAfter = after - essenceAfter;

            bool collided = DistanceToSegmentSquared(
                relativeBefore,
                relativeAfter) <= collisionRadiusSquared;

            if (collided)
            {
                owner.TryTakeHit();
                RemoveFireball(i);
            }
            // Remove fireballs outside the arena
            else if (
                after.y < bottom - fireballRadius * 2f ||
                Mathf.Abs(after.x) > halfWidth + fireballRadius * 2f)
            {
                RemoveFireball(i);
            }
        }
    }

    private void SpawnFireball(Vector2 playerPosition)
    {
        float halfWidth = arena.rect.width * 0.5f;
        float halfHeight = arena.rect.height * 0.5f;

        // Spawn near the player's horizontal position
        // with some random scatter.
        float spawnX = Mathf.Clamp(
            playerPosition.x +
            Random.Range(-horizontalScatter, horizontalScatter),
            -halfWidth + fireballRadius,
            halfWidth - fireballRadius
        );

        Vector2 spawnPosition = new Vector2(
            spawnX,
            halfHeight + fireballRadius * 2f
        );

        // Aim at the player's position ONCE.
        Vector2 direction =
            (playerPosition - spawnPosition).normalized;

        // Create the fireball
        GameObject instance = new GameObject(
            "HellfireFireball",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform rect =
            instance.GetComponent<RectTransform>();

        rect.SetParent(transform, false);

        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.sizeDelta = new Vector2(
            fireballRadius * 2f,
            fireballRadius * 2.8f
        );

        rect.anchoredPosition = spawnPosition;

        Image image = instance.GetComponent<Image>();

        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = fireballColor;

        if (fireballFrames != null &&
            fireballFrames.Length > 0)
        {
            image.sprite = fireballFrames[0];
        }

        // Save the direction when spawned.
        // It never changes during flight.
        fireballs.Add(new Fireball
        {
            Rect = rect,
            Image = image,
            Position = spawnPosition,
            Direction = direction,
            FallSpeed = fallSpeed *
                        Random.Range(0.85f, 1.15f),
            AnimationTime = Random.Range(0f, 0.25f)
        });
    }

    // Swept collision check
    private static float DistanceToSegmentSquared(
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;

        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared <= 0.000001f)
            return start.sqrMagnitude;

        float t = Mathf.Clamp01(
            -Vector2.Dot(start, segment) /
            lengthSquared
        );

        return (start + segment * t).sqrMagnitude;
    }

    // Destroy one fireball
    private void RemoveFireball(int index)
    {
        if (fireballs[index].Rect != null)
        {
            Destroy(fireballs[index].Rect.gameObject);
        }

        fireballs.RemoveAt(index);
    }

    // Clear all fireballs when attack finishes
    public override void ClearProjectiles()
    {
        for (int i = fireballs.Count - 1; i >= 0; i--)
        {
            RemoveFireball(i);
        }
    }
}
