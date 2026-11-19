//made with chatgpt

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class SentinelChargePattern : EnemyAttackPattern
{
    [Header("Animation — assign frames 0 through 11")]
    public Sprite[] chargeFrames = new Sprite[12];

    [Header("Timing")]
    public float warningTime = 1.1f;
    public float chargeTime = 0.65f;
    public float recoveryTime = 0.5f;
    public float firstChargeDelay = 0.25f;

    [Header("Size in HUD units")]
    public float sentinelHeight = 110f;
    public float hitboxWidth = 55f;
    public float hitboxHeight = 65f;

    [Header("Warning")]
    public Color warningColor = new Color(1f, 0.65f, 0.15f, 0.3f);

    [Header("Sound effects")]
    public AudioClip warningSound;
    public AudioClip chargeSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.7f;

    private DodgePhaseController owner;
    private RectTransform arena;
    private RectTransform layer;
    private Image fighter;
    private Image warning;
    private Image originalImage;
    private bool originalEnabled;

    private enum Phase { Gap, Warning, Charge, Recovery }
    private Phase phase;

    private float timer;
    private float laneY;
    private float startX;
    private float endX;
    private bool fromLeft;
    private bool hitThisCharge;

    public override bool ControlsEnemyAnimation => true;

    public override void Initialize(
        DodgePhaseController controller,
        RectTransform arenaArea)
    {
        owner = controller;
        arena = arenaArea;

        if (chargeFrames == null || chargeFrames.Length != 12)
        {
            Debug.LogError("Assign all 12 Sentinel frames.", this);
            return;
        }

        foreach (Sprite frame in chargeFrames)
        {
            if (frame == null)
            {
                Debug.LogError("A Sentinel frame is missing.", this);
                return;
            }
        }

        warningTime = Mathf.Max(0.2f, warningTime);
        chargeTime = Mathf.Max(0.1f, chargeTime);
        recoveryTime = Mathf.Max(0.1f, recoveryTime);
        sentinelHeight = Mathf.Max(10f, sentinelHeight);

        originalImage = controller.EnemyImage;

        if (originalImage != null)
        {
            originalEnabled = originalImage.enabled;
            originalImage.enabled = true;
            originalImage.sprite = chargeFrames[0];
        }

        GameObject layerObject = new GameObject(
            "SentinelChargeLayer",
            typeof(RectTransform),
            typeof(RectMask2D));

        layer = layerObject.GetComponent<RectTransform>();
        layer.SetParent(transform, false);
        layer.anchorMin = layer.anchorMax = Vector2.one * 0.5f;
        layer.pivot = Vector2.one * 0.5f;
        layer.anchoredPosition = Vector2.zero;
        layer.sizeDelta = arena.rect.size;

        warning = CreateImage("LaneWarning");
        fighter = CreateImage("ChargingSentinel");
        fighter.preserveAspect = true;

        float aspect = chargeFrames[0].rect.width /
                       chargeFrames[0].rect.height;

        fighter.rectTransform.sizeDelta =
            new Vector2(sentinelHeight * aspect, sentinelHeight);

        fighter.sprite = chargeFrames[0];
        fighter.enabled = false;
        warning.enabled = false;

        phase = Phase.Gap;
        timer = 0f;
        fromLeft = true;
    }

    public override void Tick(
        float deltaTime,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius)
    {
        if (fighter == null || arena == null || owner == null)
            return;

        float previousTime = timer;
        timer += deltaTime;

        if (phase == Phase.Gap)
        {
            if (timer >= firstChargeDelay)
                BeginWarning(essenceAfter.y);

            return;
        }

        if (phase == Phase.Warning)
        {
            float progress = Mathf.Clamp01(timer / warningTime);

            SetFrame(Mathf.Min(3, Mathf.FloorToInt(progress * 4f)));

            Color tint = warningColor;
            tint.a *= 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(timer * 12f));
            warning.color = tint;

            if (timer >= warningTime)
            {
                warning.enabled = false;
                phase = Phase.Charge;
                timer = 0f;
                SetFrame(4);
                PlaySfx(chargeSound, soundVolume);
            }

            return;
        }

        if (phase == Phase.Charge)
        {
            float oldX = Mathf.Lerp(
                startX, endX, Mathf.Clamp01(previousTime / chargeTime));

            float newX = Mathf.Lerp(
                startX, endX, Mathf.Clamp01(timer / chargeTime));

            fighter.rectTransform.anchoredPosition =
                new Vector2(newX, laneY);

            float progress = Mathf.Clamp01(timer / chargeTime);
            SetFrame(4 + Mathf.Min(3, Mathf.FloorToInt(progress * 4f)));

            // Check relative movement so a fast lunge cannot skip the player.
            Vector2 relativeBefore =
                essenceBefore - new Vector2(oldX, laneY);

            Vector2 relativeAfter =
                essenceAfter - new Vector2(newX, laneY);

            Vector2 halfSize = new Vector2(
                hitboxWidth * 0.5f + essenceRadius,
                hitboxHeight * 0.5f + essenceRadius);

            if (!hitThisCharge &&
                SegmentHitsBox(relativeBefore, relativeAfter, halfSize))
            {
                hitThisCharge = owner.TryTakeHit();
            }

            if (timer >= chargeTime)
            {
                phase = Phase.Recovery;
                timer = 0f;
            }

            return;
        }

        if (phase == Phase.Recovery)
        {
            float progress = Mathf.Clamp01(timer / recoveryTime);
            SetFrame(8 + Mathf.Min(3, Mathf.FloorToInt(progress * 4f)));

            if (timer >= recoveryTime)
            {
                fromLeft = !fromLeft;
                BeginWarning(essenceAfter.y);
            }
        }
    }

    private void BeginWarning(float targetY)
    {
        phase = Phase.Warning;
        timer = 0f;
        hitThisCharge = false;
        PlaySfx(warningSound, soundVolume);

        float height = arena.rect.height;
        float laneHeight = Mathf.Min(hitboxHeight, height);

        laneY = Mathf.Clamp(
            targetY,
            -height * 0.5f + laneHeight * 0.5f,
             height * 0.5f - laneHeight * 0.5f);

        float halfWidth = arena.rect.width * 0.5f;
        float spriteHalfWidth = fighter.rectTransform.sizeDelta.x * 0.5f;

        // Start visibly at one edge, then run out through the other.
        startX = fromLeft
            ? -halfWidth + spriteHalfWidth
            : halfWidth - spriteHalfWidth;

        endX = fromLeft
            ? halfWidth + spriteHalfWidth
            : -halfWidth - spriteHalfWidth;

        fighter.rectTransform.anchoredPosition =
            new Vector2(startX, laneY);

        // The supplied sheet faces right.
        fighter.rectTransform.localScale =
            new Vector3(fromLeft ? 1f : -1f, 1f, 1f);

        fighter.enabled = true;
        SetFrame(0);

        warning.rectTransform.sizeDelta =
            new Vector2(arena.rect.width, laneHeight);

        warning.rectTransform.anchoredPosition =
            new Vector2(0f, laneY);

        warning.color = warningColor;
        warning.enabled = true;
    }

    private void SetFrame(int index)
    {
        Sprite frame = chargeFrames[Mathf.Clamp(index, 0, 11)];

        fighter.sprite = frame;

        if (originalImage != null)
            originalImage.sprite = frame;
    }

    private Image CreateImage(string objectName)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(layer, false);
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;

        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static bool SegmentHitsBox(
        Vector2 from, Vector2 to, Vector2 halfSize)
    {
        Vector2 movement = to - from;
        float enter = 0f;
        float exit = 1f;

        for (int axis = 0; axis < 2; axis++)
        {
            if (Mathf.Abs(movement[axis]) < 0.00001f)
            {
                if (Mathf.Abs(from[axis]) > halfSize[axis])
                    return false;
            }
            else
            {
                float a = (-halfSize[axis] - from[axis]) / movement[axis];
                float b = (halfSize[axis] - from[axis]) / movement[axis];

                enter = Mathf.Max(enter, Mathf.Min(a, b));
                exit = Mathf.Min(exit, Mathf.Max(a, b));

                if (enter > exit)
                    return false;
            }
        }

        return true;
    }

    public override void ClearProjectiles()
    {
        StopSfx();

        if (layer != null)
        {
            layer.gameObject.SetActive(false);
            Destroy(layer.gameObject);
        }

        if (originalImage != null)
            originalImage.enabled = originalEnabled;

        fighter = null;
        warning = null;
        layer = null;
    }

    private void OnDisable()
    {
        ClearProjectiles();
    }
}