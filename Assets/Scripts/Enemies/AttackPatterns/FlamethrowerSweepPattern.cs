using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(AudioSource))]
public sealed class FlamethrowerSweepPattern : EnemyAttackPattern
{
    public Sprite[] characterFrames;
    public Sprite[] fireFrames;
    public Sprite[] warningIcons;
    public Sprite warningChevron;
    public AudioClip warningSound;
    public AudioClip flamethrowerLoop;
    [Range(0f, 1f)] public float soundVolume = 0.55f;
    [Min(0.5f)] public float warningTime = 1.2f;
    [Min(1f)] public float sweepTime = 3f;
    [Min(0.1f)] public float recoveryTime = 0.5f;
    [Min(1)] public int sweepCount = 2;
    [Range(25f, 75f)] public float sweepAngle = 65f;
    [Range(3f, 15f)] public float coneHalfAngle = 6f;

    public override bool ControlsEnemyAnimation => true;

    private enum Phase { Warning, Sweep, Recovery, Done }
    private Phase phase = Phase.Done;
    private float timer;
    private int sweepIndex;
    private DodgePhaseController owner;
    private RectTransform arena;
    private RectTransform layer;
    private FlameConeGraphic cone;
    private Image warningIcon;
    private Image[] chevrons;
    private Image[] flames;
    private AudioSource streamAudio;
    private Sprite restingSprite;
    private Vector2 origin;
    private float streamLength;
    private const float StartHalfWidth = 6f;

    public override void Initialize(DodgePhaseController controller, RectTransform arenaArea)
    {
        ClearProjectiles();
        owner = controller;
        arena = arenaArea;
        if (characterFrames == null || characterFrames.Length != 8 || fireFrames == null || fireFrames.Length == 0)
        {
            Debug.LogError("Flamethrower: assign eight character frames and animated flame sprites.", this);
            return;
        }
        warningTime = Mathf.Max(0.5f, warningTime);
        sweepTime = Mathf.Max(1f, sweepTime);
        recoveryTime = Mathf.Max(0.1f, recoveryTime);
        sweepAngle = Mathf.Clamp(sweepAngle, 25f, 75f);
        coneHalfAngle = Mathf.Clamp(coneHalfAngle, 3f, 15f);
        restingSprite = owner.EnemyImage != null ? owner.EnemyImage.sprite : null;
        origin = new Vector2(arena.rect.center.x, arena.rect.yMax + Mathf.Max(50f, arena.rect.height * 0.2f));
        streamLength = arena.rect.size.magnitude + 150f;
        var layerObject = new GameObject("FlamethrowerStream", typeof(RectTransform));
        layer = (RectTransform)layerObject.transform;
        layer.SetParent(transform, false);
        layer.sizeDelta = arena.rect.size;
        cone = new GameObject("ContinuousFlameCone", typeof(RectTransform), typeof(CanvasRenderer), typeof(FlameConeGraphic))
            .GetComponent<FlameConeGraphic>();
        cone.transform.SetParent(layer, false);
        cone.rectTransform.sizeDelta = arena.rect.size;
        cone.raycastTarget = false;
        flames = new Image[24];
        for (int i = 0; i < flames.Length; i++) flames[i] = MakeImage("FlowingFlame");
        warningIcon = MakeImage("IgnitionWarning");
        warningIcon.rectTransform.sizeDelta = new Vector2(64, 64);
        chevrons = new Image[3];
        for (int i = 0; i < chevrons.Length; i++)
        {
            chevrons[i] = MakeImage("SweepDirection");
            chevrons[i].sprite = warningChevron;
            chevrons[i].rectTransform.sizeDelta = new Vector2(38, 38);
        }
        streamAudio = GetComponent<AudioSource>();
        streamAudio.playOnAwake = false;
        streamAudio.spatialBlend = 0f;
        streamAudio.loop = true;
        streamAudio.clip = flamethrowerLoop;
        phase = Phase.Warning;
        timer = 0f;
        sweepIndex = 0;
        PlaySfx(warningSound, soundVolume);
        UpdateVisuals();
    }

    public override void Tick(float deltaTime, Vector2 essenceBefore, Vector2 essenceAfter, float essenceRadius)
    {
        if (layer == null || deltaTime <= 0f) return;
        float remaining = deltaTime;
        while (remaining > 0f && phase != Phase.Done)
        {
            float duration = phase == Phase.Warning ? warningTime : phase == Phase.Sweep ? sweepTime : recoveryTime;
            float step = Mathf.Min(remaining, duration - timer);
            if (phase == Phase.Sweep)
            {
                float startFraction = (deltaTime - remaining) / deltaTime;
                float endFraction = (deltaTime - remaining + step) / deltaTime;
                CheckHits(timer, timer + step, Vector2.Lerp(essenceBefore, essenceAfter, startFraction),
                    Vector2.Lerp(essenceBefore, essenceAfter, endFraction), essenceRadius);
            }
            timer += step;
            remaining -= step;
            if (timer >= duration)
            {
                timer = 0f;
                NextPhase();
            }
        }
        UpdateVisuals();
    }

    private void NextPhase()
    {
        if (phase == Phase.Warning)
        {
            phase = Phase.Sweep;
            streamAudio.volume = 0f;
            if (flamethrowerLoop != null) streamAudio.Play();
        }
        else if (phase == Phase.Sweep)
        {
            phase = Phase.Recovery;
            streamAudio.Stop();
        }
        else
        {
            sweepIndex++;
            phase = sweepIndex >= Mathf.Max(1, sweepCount) ? Phase.Done : Phase.Warning;
            if (phase == Phase.Warning) PlaySfx(warningSound, soundVolume);
        }
    }

    private float Angle(float time)
    {
        float progress = Mathf.Clamp01(time / sweepTime);
        if (sweepIndex % 2 != 0) progress = 1f - progress;
        return Mathf.Lerp(-sweepAngle, sweepAngle, progress);
    }

    private static Vector2 Forward(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
    }

    private void CheckHits(float beforeTime, float afterTime, Vector2 before, Vector2 after, float radius)
    {
        float angleBefore = Angle(beforeTime);
        float angleAfter = Angle(afterTime);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(angleAfter - angleBefore) / 1.5f,
            Vector2.Distance(before, after) / Mathf.Max(2f, radius * 0.5f))));
        float spread = Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad);
        for (int i = 0; i <= steps; i++)
        {
            float fraction = (float)i / steps;
            Vector2 direction = Forward(Mathf.Lerp(angleBefore, angleAfter, fraction));
            Vector2 relative = Vector2.Lerp(before, after, fraction) - origin;
            float along = Vector2.Dot(relative, direction);
            float sideways = Mathf.Abs(relative.x * direction.y - relative.y * direction.x);
            float width = StartHalfWidth + Mathf.Clamp(along, 0f, streamLength) * spread;
            if (along >= -radius && along <= streamLength + radius && sideways <= width + radius)
            {
                owner.TryTakeHit();
                return;
            }
        }
    }

    private void UpdateVisuals()
    {
        bool firing = phase == Phase.Sweep;
        bool warning = phase == Phase.Warning;
        float angle = Angle(firing ? timer : 0f);
        Vector2 direction = Forward(angle);
        float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(timer * 12f));
        cone.enabled = firing || warning;
        cone.SetCone(origin, direction, streamLength, StartHalfWidth,
            StartHalfWidth + streamLength * Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad), warning,
            new Color(1f, 0.58f, 0.12f, 0.18f * pulse));
        warningIcon.enabled = warning && warningIcons != null && warningIcons.Length > 0;
        if (warningIcon.enabled)
        {
            warningIcon.sprite = warningIcons[Mathf.FloorToInt(timer * 8f) % warningIcons.Length];
            float entry = (origin.y - arena.rect.yMax + 45f) / -direction.y;
            warningIcon.rectTransform.anchoredPosition = origin + direction * entry;
        }
        for (int i = 0; i < chevrons.Length; i++)
        {
            Image arrow = chevrons[i];
            arrow.enabled = warning && warningChevron != null;
            arrow.color = new Color(1f, 0.72f, 0.23f, pulse);
            arrow.rectTransform.anchoredPosition = new Vector2((i - 1) * 48f, arena.rect.yMax - 45f);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, sweepIndex % 2 == 0 ? 90f : -90f);
        }
        for (int i = 0; i < flames.Length; i++)
        {
            Image flame = flames[i];
            flame.enabled = firing;
            if (!firing) continue;
            float distance = Mathf.Repeat(i * streamLength / flames.Length + timer * 380f, streamLength);
            float width = StartHalfWidth + distance * Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad);
            flame.sprite = fireFrames[(Mathf.FloorToInt(timer * 12f) + i) % fireFrames.Length];
            flame.rectTransform.anchoredPosition = origin + direction * distance;
            flame.rectTransform.sizeDelta = new Vector2(width * 2.2f, Mathf.Max(90f, width * 2.4f));
            flame.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }
        if (firing)
        {
            streamAudio.volume = soundVolume * Mathf.Clamp01(Mathf.Min(timer / 0.08f, (sweepTime - timer) / 0.12f));
        }
        if (owner.EnemyImage == null) return;
        int frame = 7;
        if (warning) frame = Mathf.Min(2, Mathf.FloorToInt(timer / warningTime * 3f));
        if (firing)
        {
            float progress = timer / sweepTime;
            if (sweepIndex % 2 != 0) progress = 1f - progress;
            frame = 3 + Mathf.Min(2, Mathf.FloorToInt(progress * 3f));
        }
        if (phase == Phase.Recovery && timer < recoveryTime * 0.5f) frame = 6;
        owner.EnemyImage.sprite = characterFrames[frame];
        owner.EnemyImage.enabled = true;
    }

    private Image MakeImage(string objectName)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(layer, false);
        var image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    public override void ClearProjectiles()
    {
        phase = Phase.Done;
        StopSfx();
        if (streamAudio != null) streamAudio.Stop();
        if (layer != null)
        {
            layer.gameObject.SetActive(false);
            Destroy(layer.gameObject);
            layer = null;
        }
        if (owner != null && owner.EnemyImage != null) owner.EnemyImage.sprite = restingSprite;
    }

    private void OnDisable() => ClearProjectiles();
}
