using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public sealed class BombRainPattern : EnemyAttackPattern
{
    public BombSpawner spawner;
    public Sprite[] summonerFrames;
    [Min(1f)] public float framesPerSecond = 8f;
    [Min(0)] public int releaseFrame = 5;

    public RectTransform battleBox { get; private set; }
    public RectTransform ProjectileRoot => (RectTransform)transform;
    public override bool ControlsEnemyAnimation => true;

    private DodgePhaseController owner;
    private AudioSource audioSource;

    public override void Initialize(DodgePhaseController controller, RectTransform arenaArea)
    {
        ClearProjectiles();
        owner = controller;
        battleBox = arenaArea;
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        spawner.BeginUI(this);
        UpdateSummoner();
    }

    public override void Tick(float deltaTime, Vector2 essenceBefore, Vector2 essenceAfter, float essenceRadius)
    {
        spawner.TickUI(deltaTime, releaseFrame / Mathf.Max(1f, framesPerSecond));
        UpdateSummoner();
        foreach (Bomb bomb in GetComponentsInChildren<Bomb>()) bomb.TickUI(deltaTime);
        foreach (Shard shard in GetComponentsInChildren<Shard>())
            shard.TickUI(deltaTime, essenceBefore, essenceAfter, essenceRadius);
    }

    private void UpdateSummoner()
    {
        if (owner.EnemyImage == null || summonerFrames == null || summonerFrames.Length == 0) return;
        int frame = Mathf.Min(summonerFrames.Length - 1,
            Mathf.FloorToInt(spawner.WaveElapsed * Mathf.Max(1f, framesPerSecond)));
        owner.EnemyImage.sprite = summonerFrames[frame];
        owner.EnemyImage.enabled = true;
    }

    public bool TryHit(Vector2 relativeStart, Vector2 relativeEnd, float radius)
    {
        Vector2 segment = relativeEnd - relativeStart;
        float fraction = segment.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector2.Dot(-relativeStart, segment) / segment.sqrMagnitude) : 0f;
        return (relativeStart + segment * fraction).sqrMagnitude <= radius * radius && owner.TryTakeHit();
    }

    public void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null) audioSource.PlayOneShot(clip, volume);
    }

    public override void ClearProjectiles()
    {
        if (spawner != null) spawner.StopAttack();
        foreach (Bomb bomb in GetComponentsInChildren<Bomb>())
        {
            bomb.gameObject.SetActive(false);
            Destroy(bomb.gameObject);
        }
        foreach (Shard shard in GetComponentsInChildren<Shard>())
        {
            shard.gameObject.SetActive(false);
            Destroy(shard.gameObject);
        }
    }

    private void OnDisable() => ClearProjectiles();
}