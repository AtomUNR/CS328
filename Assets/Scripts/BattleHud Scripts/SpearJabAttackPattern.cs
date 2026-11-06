using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tower Guardian "Spear Jab".
///
/// The guardian winds up while the lanes it is about to hit light up with a
/// warning (flashing icon, marching chevrons, pulsing tint). When the warning
/// ends the guardian jabs and giant spears stab down through those lanes of
/// the dodge arena, hold for a moment, then pull back out.
///
/// Waves alternate between:
///   Block   - a solid "section" of neighbouring lanes around the player.
///   Scatter - the lane the player is standing in, plus random lanes elsewhere.
///
/// Notes
///  - Like the other patterns, this object is assumed to be centred on the
///    arena; everything is positioned in arena-centred coordinates.
///  - Tick only runs during the active attack, so the warning lives in here.
///  - Import the spear / warning PNGs with Filter Mode = Point and
///    Compression = None. One PNG pixel = one "art pixel" (artPixelSize units).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SpearJabAttackPattern : EnemyAttackPattern
{
    private enum GuardFrame
    {
        Idle = 0,
        Crouch = 1,
        Rise = 2,
        Peak = 3,
        Thrust = 4,
        Impact = 5,
        Recoil = 6,
        Settle = 7
    }

    private const int GuardFrameCount = 8;

    public override bool ControlsEnemyAnimation
    {
        get
        {
            if (guardianImage == null || guardianFrames == null ||
                guardianFrames.Length != GuardFrameCount)
                return false;
            for (int i = 0; i < GuardFrameCount; i++)
                if (guardianFrames[i] == null) return false;
            return true;
        }
    }

    [Header("Guardian (the sprite that performs the jab)")]
    [Tooltip("Leave empty on the prefab. Bound to the dodge controller enemy image at runtime.")]
    [SerializeField] private Image guardianImage;
    [Tooltip("Idle, Crouch, Rise, Peak, Thrust, Impact, Recoil, Settle (in that order).")]
    [SerializeField] private Sprite[] guardianFrames = new Sprite[GuardFrameCount];

    [Header("Spear art")]
    [SerializeField] private Sprite spearHeadSprite;
    [SerializeField] private Sprite spearShaftSprite;
    [SerializeField] private Sprite[] impactFrames;
    [SerializeField, Min(1f)] private float impactFramesPerSecond = 14f;
    [SerializeField] private Color spearColor = Color.white;
    [Tooltip("Arena units covered by one art pixel (the guardian sprite is about 4.4).")]
    [SerializeField, Min(0.5f)] private float artPixelSize = 4.4f;

    [Tooltip("Use 8 for sprites sliced from an 8x enlarged FX sheet; 1 for native-resolution FX sprites.")]
    [SerializeField, Min(1f)] private float sourcePixelsPerArtPixel = 8f;

    private float SpritePixelSize => artPixelSize / Mathf.Max(1f, sourcePixelsPerArtPixel);

    [Header("Warning art")]
    [SerializeField] private Sprite[] warningIconFrames;
    [SerializeField] private Sprite warningChevronSprite;
    [SerializeField] private Color warningColor = new Color(0.98f, 0.92f, 0.8f, 1f);
    [SerializeField, Min(1f)] private float warningFlashesPerSecond = 8f;
    [SerializeField, Min(0f)] private float chevronSpeed = 360f;

    [Header("Waves")]
    [SerializeField, Min(0f)] private float firstWaveDelay = 0.3f;
    [SerializeField, Min(0f)] private float timeBetweenWaves = 0.45f;
    [SerializeField, Min(20f)] private float laneWidth = 100f;
    [SerializeField, Range(1, 8)] private int blockLanes = 2;
    [SerializeField, Range(1, 8)] private int scatterSpears = 2;
    [SerializeField, Range(1, 6)] private int minSafeLanes = 3;
    [SerializeField, Min(0f)] private float staggerDelay = 0.06f;

    [Header("Timing (seconds)")]
    [Tooltip("How long a lane is highlighted before its spear lands.")]
    [SerializeField, Min(0.2f)] private float warningTime = 1.2f;
    [Tooltip("Guardian crouch / rise / peak frames, played at the end of the warning.")]
    [SerializeField, Min(0.1f)] private float windupTime = 0.42f;
    [SerializeField, Min(0.02f)] private float thrustTime = 0.09f;
    [SerializeField, Min(0f)] private float holdTime = 0.35f;
    [SerializeField, Min(0.05f)] private float retractTime = 0.3f;
    [Tooltip("Sideways shake of a spear as it lands, in art pixels (0 = none).")]
    [SerializeField, Min(0f)] private float impactShake = 1.5f;

    [Header("Hit box")]
    [Tooltip("Fraction of the drawn spear width that hurts. Lower is more forgiving.")]
    [SerializeField, Range(0.3f, 1f)] private float hitboxScale = 0.85f;

    private enum WaveShape
    {
        Block,
        Scatter
    }

    private sealed class Strike
    {
        public int Lane;
        public float LaneX;
        public float Delay;
        public float TipY;
        public bool HasHit;
        public bool ImpactStarted;

        public RectTransform Root;
        public RectTransform WarningGroup;
        public RectTransform SpearGroup;
        public RectTransform ShaftRect;
        public RectTransform HeadRect;

        public Image Tint;
        public Image EdgeLeft;
        public Image EdgeRight;
        public Image Icon;
        public Image[] Chevrons;
        public Image Impact;
        public float ImpactAge;
    }

    private readonly List<Strike> strikes = new List<Strike>();

    private DodgePhaseController owner;
    private RectTransform arena;
    private RectTransform layer;

    private bool waveActive;
    private float waveTime;
    private float gapTimer;
    private int waveIndex;

    private Sprite originalGuardianSprite;
    private int currentGuardFrame = -1;

    // ------------------------------------------------------------------ lifecycle

    public override void Initialize(
        DodgePhaseController controller,
        RectTransform arenaArea)
    {
        ClearProjectiles();
        owner = controller;
        arena = arenaArea;
        guardianImage = controller != null ? controller.EnemyImage : null;
        originalGuardianSprite = guardianImage != null ? guardianImage.sprite : null;
        currentGuardFrame = -1;

        laneWidth = Mathf.Max(20f, laneWidth);
        blockLanes = Mathf.Max(1, blockLanes);
        scatterSpears = Mathf.Max(1, scatterSpears);
        minSafeLanes = Mathf.Max(1, minSafeLanes);
        warningTime = Mathf.Max(0.2f, warningTime);
        windupTime = Mathf.Clamp(windupTime, 0.1f, warningTime);
        thrustTime = Mathf.Max(0.02f, thrustTime);
        holdTime = Mathf.Max(0f, holdTime);
        retractTime = Mathf.Max(0.05f, retractTime);

        if (arena != null && Mathf.FloorToInt(arena.rect.width / laneWidth) <= minSafeLanes)
            Debug.LogWarning("Spear Jab: arena has too few lanes. Reduce Lane Width or Min Safe Lanes. This attack will skip waves to preserve safe space.", this);

        gapTimer = firstWaveDelay;
        waveIndex = 0;
        waveActive = false;
        waveTime = 0f;
    }

    // The dodge controller calls this only during the active attack.
    public override void Tick(
        float deltaTime,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius)
    {
        if (owner == null || arena == null || deltaTime <= 0f)
            return;

        EnsureLayer();

        if (!waveActive)
        {
            gapTimer -= deltaTime;

            if (gapTimer > 0f)
            {
                UpdateGuardian();
                return;
            }

            StartWave(essenceAfter);
        }

        waveTime += deltaTime;

        bool anyAlive = false;

        for (int i = 0; i < strikes.Count; i++)
        {
            if (UpdateStrike(
                strikes[i],
                essenceBefore,
                essenceAfter,
                essenceRadius))
            {
                anyAlive = true;
            }
        }

        UpdateGuardian();

        if (!anyAlive)
            EndWave();
    }

    public override void ClearProjectiles()
    {
        for (int i = strikes.Count - 1; i >= 0; i--)
            DestroyStrike(strikes[i]);

        strikes.Clear();
        waveActive = false;

        RestoreGuardian();
    }

    // ------------------------------------------------------------------ waves

    private void StartWave(Vector2 essencePosition)
    {
        int lanes = Mathf.Max(1, Mathf.FloorToInt(arena.rect.width / laneWidth));
        if (lanes <= minSafeLanes)
        {
            gapTimer = Mathf.Max(0.1f, timeBetweenWaves);
            return;
        }
        float left = -lanes * laneWidth * 0.5f;

        int playerLane = Mathf.Clamp(
            Mathf.FloorToInt((essencePosition.x - left) / laneWidth),
            0,
            lanes - 1);

        int maxSpears = Mathf.Max(1, lanes - minSafeLanes);
        WaveShape shape = (waveIndex % 2 == 0)
            ? WaveShape.Block
            : WaveShape.Scatter;

        List<int> chosenLanes = new List<int>();
        List<float> delays = new List<float>();

        if (shape == WaveShape.Block)
        {
            // One solid section that always covers the player's current lane,
            // but not always centred on it.
            int count = Mathf.Min(blockLanes, maxSpears);
            int start = Mathf.Clamp(
                playerLane - Random.Range(0, count),
                0,
                lanes - count);

            float centre = start + (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int lane = start + i;
                chosenLanes.Add(lane);

                // Ripple outward from the middle of the section.
                delays.Add(Mathf.Abs(lane - centre) * staggerDelay);
            }
        }
        else
        {
            // The player's lane first, then random lanes that are not
            // touching each other, so there is always somewhere to stand.
            int count = Mathf.Min(scatterSpears, maxSpears);
            chosenLanes.Add(playerLane);

            int attempts = 0;

            while (chosenLanes.Count < count && attempts < 64)
            {
                attempts++;

                int candidate = Random.Range(0, lanes);
                bool tooClose = false;

                for (int i = 0; i < chosenLanes.Count; i++)
                {
                    if (Mathf.Abs(chosenLanes[i] - candidate) < 2)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    chosenLanes.Add(candidate);
            }

            for (int i = 0; i < chosenLanes.Count; i++)
                delays.Add(i * staggerDelay);
        }

        for (int i = 0; i < chosenLanes.Count; i++)
        {
            float laneX = left + (chosenLanes[i] + 0.5f) * laneWidth;
            strikes.Add(CreateStrike(chosenLanes[i], laneX, delays[i]));
        }

        waveIndex++;
        waveActive = true;
        waveTime = 0f;
    }

    private void EndWave()
    {
        for (int i = strikes.Count - 1; i >= 0; i--)
            DestroyStrike(strikes[i]);

        strikes.Clear();

        waveActive = false;
        gapTimer = timeBetweenWaves;

        SetGuardianFrame(GuardFrame.Idle);
    }

    // ------------------------------------------------------------------ one strike

    // Returns false once the strike has completely finished.
    private bool UpdateStrike(
        Strike strike,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius)
    {
        float strikeStart = warningTime + strike.Delay;
        float age = waveTime - strikeStart;
        float total = thrustTime + holdTime + retractTime;

        if (age >= total)
        {
            strike.Root.gameObject.SetActive(false);
            return false;
        }

        if (age < 0f)
        {
            UpdateWarning(strike, strikeStart);
            return true;
        }

        if (strike.WarningGroup.gameObject.activeSelf)
            strike.WarningGroup.gameObject.SetActive(false);

        if (!strike.SpearGroup.gameObject.activeSelf)
            strike.SpearGroup.gameObject.SetActive(true);

        float previousTip = strike.TipY;
        float tip = TipYAt(age);
        strike.TipY = tip;

        float shake = 0f;

        if (impactShake > 0f &&
            age >= thrustTime &&
            age < thrustTime + 0.12f)
        {
            float falloff = 1f - (age - thrustTime) / 0.12f;
            float sign = (Mathf.FloorToInt(age * 40f) % 2 == 0) ? 1f : -1f;
            shake = sign * impactShake * artPixelSize * falloff;
        }

        PositionSpear(strike, tip, shake);

        if (age >= thrustTime && !strike.ImpactStarted)
            StartImpact(strike);

        UpdateImpact(strike);

        // The spear body covers everything above its tip, so the lowest tip
        // of this step is enough to catch a fast jab.
        float lowestTip = Mathf.Min(previousTip, tip);

        if (!strike.HasHit &&
            PathHitsSpear(
                essenceBefore,
                essenceAfter,
                essenceRadius,
                strike.LaneX,
                lowestTip))
        {
            // An invulnerable contact must not consume this spear's hit.
            strike.HasHit = owner.TryTakeHit();
        }

        return true;
    }

    private float HiddenTipY()
    {
        // Head fully above the arena, so the mask hides it.
        return arena.rect.height * 0.5f + HeadHeight() + artPixelSize;
    }

    private float ExtendedTipY()
    {
        return -arena.rect.height * 0.5f + artPixelSize * 0.5f;
    }

    private float TipYAt(float age)
    {
        float hidden = HiddenTipY();
        float extended = ExtendedTipY();

        if (age < thrustTime)
        {
            float u = Mathf.Clamp01(age / thrustTime);
            float ease = 1f - (1f - u) * (1f - u);
            return Mathf.Lerp(hidden, extended, ease);
        }

        if (age < thrustTime + holdTime)
            return extended;

        float r = Mathf.Clamp01((age - thrustTime - holdTime) / retractTime);
        return Mathf.Lerp(extended, hidden, r * r);
    }

    // ------------------------------------------------------------------ collision

    private float HeadHeight()
    {
        return spearHeadSprite != null
            ? spearHeadSprite.rect.height * SpritePixelSize
            : artPixelSize * 47f;
    }

    private float HeadHalfWidth()
    {
        float drawn = spearHeadSprite != null
            ? spearHeadSprite.rect.width * SpritePixelSize * 0.5f
            : artPixelSize * 8.5f;

        return drawn * hitboxScale;
    }

    private float ShaftHalfWidth()
    {
        // The outermost art pixel on each side of the shaft is a dark outline.
        float drawn = spearShaftSprite != null
            ? Mathf.Max(0f, spearShaftSprite.rect.width - 2f * sourcePixelsPerArtPixel) * SpritePixelSize * 0.5f
            : artPixelSize * 4.5f;

        return drawn * hitboxScale;
    }

    // Half width of the spear at a given distance above its tip.
    private float HalfWidthAt(float aboveTip)
    {
        float headHeight = HeadHeight();
        float headHalf = HeadHalfWidth();

        if (aboveTip <= headHeight)
        {
            float t = Mathf.Clamp01(aboveTip / (headHeight * 0.7f));
            return Mathf.Lerp(artPixelSize * 0.5f, headHalf, t);
        }

        return ShaftHalfWidth();
    }

    private bool PathHitsSpear(
        Vector2 from,
        Vector2 to,
        float radius,
        float laneX,
        float tipY)
    {
        // Sample along the essence's movement this frame so a fast dodge
        // cannot slip through a spear that lands in the same frame.
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Clamp(
            Mathf.CeilToInt(distance / Mathf.Max(1f, radius * 0.5f)),
            1,
            12);

        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, i / (float)steps);

            if (CircleHitsSpear(point, radius, laneX, tipY))
                return true;
        }

        return false;
    }

    private bool CircleHitsSpear(
        Vector2 centre,
        float radius,
        float laneX,
        float tipY)
    {
        float dx = Mathf.Abs(centre.x - laneX);
        float above = centre.y - tipY;

        if (above < 0f)
        {
            // Below the tip: only the point of the spear can reach.
            float reach = radius + artPixelSize * 0.5f;
            return dx * dx + above * above <= reach * reach;
        }

        return dx <= HalfWidthAt(above) + radius;
    }

    // ------------------------------------------------------------------ warning visuals

    private void UpdateWarning(Strike strike, float strikeStart)
    {
        float progress = Mathf.Clamp01(waveTime / Mathf.Max(0.01f, strikeStart));
        float remaining = strikeStart - waveTime;
        float height = arena.rect.height;
        float top = height * 0.5f;

        // Lane tint: pulses, and gets stronger as the strike gets closer.
        float pulse = 0.75f + 0.25f * Mathf.Sin(
            waveTime * Mathf.PI * warningFlashesPerSecond);

        float alpha = Mathf.Lerp(0.05f, 0.22f, progress) * pulse;

        // Last moments: flicker fast so it clearly reads as "now".
        bool finalFlicker = remaining < 0.2f;

        if (finalFlicker)
            alpha *= (Mathf.FloorToInt(waveTime * 24f) % 2 == 0) ? 1.5f : 0.6f;

        SetAlpha(strike.Tint, warningColor, alpha);

        float edgeAlpha = finalFlicker
            ? ((Mathf.FloorToInt(waveTime * 24f) % 2 == 0) ? 1f : 0.45f)
            : Mathf.Lerp(0.45f, 0.9f, progress);

        SetAlpha(strike.EdgeLeft, warningColor, edgeAlpha);
        SetAlpha(strike.EdgeRight, warningColor, edgeAlpha);

        // Flashing icon at the top of the lane.
        if (strike.Icon != null)
        {
            if (warningIconFrames != null && warningIconFrames.Length > 0)
            {
                int frame = Mathf.FloorToInt(
                    waveTime * warningFlashesPerSecond) % warningIconFrames.Length;

                strike.Icon.sprite = warningIconFrames[frame];
            }

            RectTransform iconRect = strike.Icon.rectTransform;

            iconRect.anchoredPosition = new Vector2(
                0f,
                top - iconRect.sizeDelta.y * 0.5f - artPixelSize * 3f);
        }

        // Chevrons marching down the lane, pointing at where the spear lands.
        if (strike.Chevrons != null)
        {
            float iconHeight = strike.Icon != null
                ? strike.Icon.rectTransform.sizeDelta.y
                : artPixelSize * 15f;

            float span = height * 0.8f;
            float startY = top - iconHeight - artPixelSize * 10f;

            for (int i = 0; i < strike.Chevrons.Length; i++)
            {
                Image chevron = strike.Chevrons[i];

                float phase = Mathf.Repeat(
                    waveTime * chevronSpeed + i * span / strike.Chevrons.Length,
                    span);

                chevron.rectTransform.anchoredPosition =
                    new Vector2(0f, startY - phase);

                SetAlpha(chevron, warningColor, Mathf.Lerp(1f, 0.15f, phase / span));
            }
        }
    }

    private static void SetAlpha(Image image, Color baseColor, float alpha)
    {
        if (image == null)
            return;

        Color c = baseColor;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    // ------------------------------------------------------------------ spear + impact visuals

    private void PositionSpear(Strike strike, float tipY, float shakeX)
    {
        float headHeight = HeadHeight();

        strike.HeadRect.anchoredPosition = new Vector2(shakeX, tipY);

        // The shaft starts one art pixel inside the head and runs past the
        // top of the arena (the mask clips it).
        float shaftBottom = tipY + headHeight - artPixelSize;
        float shaftTop = arena.rect.height * 0.5f + artPixelSize * 4f;

        strike.ShaftRect.anchoredPosition = new Vector2(shakeX, shaftBottom);

        strike.ShaftRect.sizeDelta = new Vector2(
            strike.ShaftRect.sizeDelta.x,
            Mathf.Max(0f, shaftTop - shaftBottom));
    }

    private void StartImpact(Strike strike)
    {
        strike.ImpactStarted = true;

        if (impactFrames == null || impactFrames.Length == 0)
            return;

        Sprite first = impactFrames[0];

        Vector2 size = first != null
            ? first.rect.size * SpritePixelSize
            : Vector2.one * artPixelSize;

        strike.Impact = CreateImage(
            "Impact",
            strike.Root,
            new Vector2(0.5f, 0f),
            size,
            new Vector2(0f, -arena.rect.height * 0.5f),
            first,
            spearColor);

        strike.ImpactAge = 0f;
    }

    private void UpdateImpact(Strike strike)
    {
        if (strike.Impact == null)
            return;

        // Advance by real time since the impact began.
        strike.ImpactAge = Mathf.Max(
            0f,
            waveTime - (warningTime + strike.Delay + thrustTime));

        int frame = Mathf.FloorToInt(strike.ImpactAge * impactFramesPerSecond);

        if (frame >= impactFrames.Length)
        {
            Destroy(strike.Impact.gameObject);
            strike.Impact = null;
            return;
        }

        strike.Impact.sprite = impactFrames[frame];
        if (impactFrames[frame] != null)
            strike.Impact.rectTransform.sizeDelta = impactFrames[frame].rect.size * SpritePixelSize;
    }

    // ------------------------------------------------------------------ guardian animation

    private void UpdateGuardian()
    {
        GuardFrame frame = GuardFrame.Idle;

        if (waveActive)
        {
            float windupStart = warningTime - windupTime;
            float t = waveTime;

            if (t >= windupStart)
            {
                if (t < warningTime)
                {
                    float u = (t - windupStart) / windupTime;

                    frame = u < 1f / 3f
                        ? GuardFrame.Crouch
                        : (u < 2f / 3f ? GuardFrame.Rise : GuardFrame.Peak);
                }
                else
                {
                    float s = t - warningTime;

                    if (s < thrustTime)
                        frame = GuardFrame.Thrust;
                    else if (s < thrustTime + holdTime)
                        frame = GuardFrame.Impact;
                    else if (s < thrustTime + holdTime + retractTime * 0.5f)
                        frame = GuardFrame.Recoil;
                    else if (s < thrustTime + holdTime + retractTime)
                        frame = GuardFrame.Settle;
                }
            }
        }

        SetGuardianFrame(frame);
    }

    private void SetGuardianFrame(GuardFrame frame)
    {
        if (!ControlsEnemyAnimation)
            return;

        if (originalGuardianSprite == null)
            originalGuardianSprite = guardianImage.sprite;

        int index = (int)frame;

        if (index == currentGuardFrame || guardianFrames[index] == null)
            return;

        currentGuardFrame = index;
        guardianImage.sprite = guardianFrames[index];
    }

    private void RestoreGuardian()
    {
        currentGuardFrame = -1;

        if (guardianImage != null)
            guardianImage.sprite = originalGuardianSprite;
    }

    // ------------------------------------------------------------------ object creation

    private void EnsureLayer()
    {
        if (layer == null)
        {
            GameObject layerObject = new GameObject(
                "SpearAttackLayer",
                typeof(RectTransform),
                typeof(RectMask2D));

            layer = layerObject.GetComponent<RectTransform>();
            layer.SetParent(transform, false);
            layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0.5f);
            layer.pivot = new Vector2(0.5f, 0.5f);
            layer.anchoredPosition = Vector2.zero;
        }

        // Clip spears to the arena so they appear to come from behind its
        // top border and never poke out of the box.
        layer.sizeDelta = arena.rect.size;
    }

    private Strike CreateStrike(int lane, float laneX, float delay)
    {
        float height = arena.rect.height;

        Strike strike = new Strike
        {
            Lane = lane,
            LaneX = laneX,
            Delay = delay,
            TipY = HiddenTipY()
        };

        strike.Root = CreateGroup(
            "SpearStrike",
            layer,
            new Vector2(laneWidth, height),
            new Vector2(laneX, 0f));

        // Warning.
        strike.WarningGroup = CreateGroup(
            "Warning",
            strike.Root,
            new Vector2(laneWidth, height),
            Vector2.zero);

        Vector2 centre = new Vector2(0.5f, 0.5f);

        strike.Tint = CreateImage(
            "LaneTint",
            strike.WarningGroup,
            centre,
            new Vector2(laneWidth, height),
            Vector2.zero,
            null,
            warningColor);

        strike.EdgeLeft = CreateImage(
            "EdgeLeft",
            strike.WarningGroup,
            new Vector2(0f, 0.5f),
            new Vector2(artPixelSize * 2f, height),
            new Vector2(-laneWidth * 0.5f, 0f),
            null,
            warningColor);

        strike.EdgeRight = CreateImage(
            "EdgeRight",
            strike.WarningGroup,
            new Vector2(1f, 0.5f),
            new Vector2(artPixelSize * 2f, height),
            new Vector2(laneWidth * 0.5f, 0f),
            null,
            warningColor);

        if (warningChevronSprite != null)
        {
            strike.Chevrons = new Image[2];

            Vector2 chevronSize =
                warningChevronSprite.rect.size * SpritePixelSize;

            for (int i = 0; i < strike.Chevrons.Length; i++)
            {
                strike.Chevrons[i] = CreateImage(
                    "Chevron",
                    strike.WarningGroup,
                    centre,
                    chevronSize,
                    new Vector2(0f, height * 0.5f),
                    warningChevronSprite,
                    warningColor);
            }
        }

        if (warningIconFrames != null && warningIconFrames.Length > 0)
        {
            Sprite firstIcon = warningIconFrames[0];

            strike.Icon = CreateImage(
                "WarningIcon",
                strike.WarningGroup,
                centre,
                firstIcon != null
                    ? firstIcon.rect.size * SpritePixelSize
                    : Vector2.one * artPixelSize * 8f,
                new Vector2(0f, height * 0.5f),
                firstIcon,
                warningColor);
        }

        // Spear (hidden until the warning ends).
        strike.SpearGroup = CreateGroup(
            "Spear",
            strike.Root,
            new Vector2(laneWidth, height),
            Vector2.zero);

        float shaftWidth = spearShaftSprite != null
            ? spearShaftSprite.rect.width * SpritePixelSize
            : artPixelSize * 11f;

        Image shaft = CreateImage(
            "Shaft",
            strike.SpearGroup,
            new Vector2(0.5f, 0f),
            new Vector2(shaftWidth, 0f),
            new Vector2(0f, strike.TipY),
            spearShaftSprite,
            spearColor);

        Vector2 headSize = spearHeadSprite != null
            ? spearHeadSprite.rect.size * SpritePixelSize
            : new Vector2(artPixelSize * 17f, artPixelSize * 47f);

        Image head = CreateImage(
            "Head",
            strike.SpearGroup,
            new Vector2(0.5f, 0f),
            headSize,
            new Vector2(0f, strike.TipY),
            spearHeadSprite,
            spearColor);

        strike.ShaftRect = shaft.rectTransform;
        strike.HeadRect = head.rectTransform;

        strike.SpearGroup.gameObject.SetActive(false);

        return strike;
    }

    private static RectTransform CreateGroup(
        string name,
        RectTransform parent,
        Vector2 size,
        Vector2 position)
    {
        GameObject groupObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = groupObject.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return rect;
    }

    private static Image CreateImage(
        string name,
        RectTransform parent,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        Sprite sprite,
        Color color)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform rect = imageObject.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private void DestroyStrike(Strike strike)
    {
        if (strike != null && strike.Root != null)
        {
            strike.Root.gameObject.SetActive(false);
            Destroy(strike.Root.gameObject);
        }
    }
}
