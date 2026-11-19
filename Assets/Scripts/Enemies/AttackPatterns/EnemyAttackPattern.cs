using UnityEngine;

public abstract class EnemyAttackPattern : MonoBehaviour
{
    // Patterns may synchronize enemy poses to their own hazard timing.
    public virtual bool ControlsEnemyAnimation => false;

    public abstract void Initialize(
        DodgePhaseController controller,
        RectTransform arenaArea);

    public abstract void Tick(
        float deltaTime,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius);

    public abstract void ClearProjectiles();

    // ---- Sound helpers for patterns that want attack audio ----
    // The AudioSource lives on this attack instance, so when the attack ends
    // (or is cancelled) its sounds end with it.
    private AudioSource sfxSource;

    protected void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f || !isActiveAndEnabled) return;
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }
        sfxSource.PlayOneShot(clip, volume);
    }

    // Picks one of several variants so repeated sounds don't feel mechanical.
    protected void PlayRandomSfx(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySfx(clips[Random.Range(0, clips.Length)], volume);
    }

    protected void StopSfx()
    {
        if (sfxSource != null) sfxSource.Stop();
    }
}
