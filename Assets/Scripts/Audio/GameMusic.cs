using System.Collections;
using UnityEngine;

/// <summary>
/// Background music for a gameplay scene.
///  - Area music: the exterior track outside the tower, the interior track once the player
///    is inside the tower interior map. Changing area crossfades between them.
///  - Battle music: when a battle starts, the area music fades out (and pauses where it was)
///    and the guard or boss theme fades in. When the battle ends the area music resumes.
/// One per scene. Put it on any object and fill in the references.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameMusic : MonoBehaviour
{
    [Header("Tracks")]
    [SerializeField] private AudioClip exteriorMusic;
    [SerializeField] private AudioClip interiorMusic;
    [SerializeField] private AudioClip guardBattleMusic;
    [SerializeField] private AudioClip bossBattleMusic;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float exteriorVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float interiorVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float guardBattleVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float bossBattleVolume = 0.6f;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.8f;

    [Header("Scene references")]
    [Tooltip("The player's transform, used to tell whether they are inside the tower.")]
    [SerializeField] private Transform player;
    [Tooltip("The tower interior's background sprite. Inside its bounds = interior music.")]
    [SerializeField] private SpriteRenderer interiorBackground;
    [SerializeField] private BattleManager battleManager;
    [Tooltip("Fighting this monster plays the boss theme. Every other battle plays the guard theme.")]
    [SerializeField] private Monster boss;

    private AudioSource areaSource;
    private AudioSource battleSource;
    private Coroutine areaFade;
    private Coroutine battleFade;
    private AudioClip pendingAreaClip;
    private bool inBattle;

    private void Awake()
    {
        areaSource = MakeSource("Area Music");
        battleSource = MakeSource("Battle Music");
    }

    private AudioSource MakeSource(string label)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.priority = 0; // music should never be culled by sound effects
        return source;
    }

    private void Update()
    {
        bool battleNow = battleManager != null && battleManager.IsInBattle;
        if (battleNow && !inBattle) BeginBattleMusic();
        else if (!battleNow && inBattle) EndBattleMusic();
        inBattle = battleNow;

        if (!inBattle) UpdateAreaMusic();
    }

    // ------------------------------------------------------------------ area music

    private bool PlayerInsideTower()
    {
        if (player == null || interiorBackground == null) return false;
        Bounds b = interiorBackground.bounds;
        Vector3 p = player.position;
        return p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
    }

    private void UpdateAreaMusic()
    {
        bool inside = PlayerInsideTower();
        AudioClip wanted = inside ? interiorMusic : exteriorMusic;
        float volume = inside ? interiorVolume : exteriorVolume;
        if (wanted == null) return;

        if (areaSource.clip != wanted && pendingAreaClip != wanted)
        {
            // Crossfade: fade out whatever is playing, then start the new area track.
            SwitchAreaTrack(wanted, volume);
        }
        else if (areaFade == null && !areaSource.isPlaying)
        {
            areaSource.UnPause();
            if (!areaSource.isPlaying) areaSource.Play();
            areaFade = StartCoroutine(Fade(areaSource, volume, fadeSeconds, false));
        }
    }

    private void SwitchAreaTrack(AudioClip clip, float volume)
    {
        if (areaFade != null) StopCoroutine(areaFade);
        pendingAreaClip = clip;
        areaFade = StartCoroutine(SwitchRoutine(clip, volume));
    }

    private IEnumerator SwitchRoutine(AudioClip clip, float volume)
    {
        if (areaSource.isPlaying && areaSource.volume > 0.001f)
            yield return Fade(areaSource, 0f, fadeSeconds, false);
        areaSource.Stop();
        areaSource.clip = clip;
        areaSource.volume = 0f;
        areaSource.Play();
        yield return Fade(areaSource, volume, fadeSeconds, false);
        areaFade = null;
        pendingAreaClip = null;
    }

    // ------------------------------------------------------------------ battle music

    private void BeginBattleMusic()
    {
        bool bossFight = boss != null && battleManager.CurrentMonster == boss;
        AudioClip clip = bossFight ? bossBattleMusic : guardBattleMusic;
        float volume = bossFight ? bossBattleVolume : guardBattleVolume;

        if (areaFade != null) StopCoroutine(areaFade);
        pendingAreaClip = null; // an interrupted area crossfade restarts after the battle
        areaFade = StartCoroutine(Fade(areaSource, 0f, fadeSeconds * 0.5f, true)); // pause to resume later

        if (clip == null) return;
        if (battleFade != null) StopCoroutine(battleFade);
        battleSource.Stop();
        battleSource.clip = clip;
        battleSource.volume = 0f;
        battleSource.Play();
        battleFade = StartCoroutine(Fade(battleSource, volume, fadeSeconds * 0.5f, false));
    }

    private void EndBattleMusic()
    {
        if (battleFade != null) StopCoroutine(battleFade);
        battleFade = StartCoroutine(FadeOutAndStop(battleSource));
        if (areaFade != null) StopCoroutine(areaFade);
        areaFade = null; // UpdateAreaMusic resumes (or switches) the area track
    }

    private IEnumerator FadeOutAndStop(AudioSource source)
    {
        yield return Fade(source, 0f, fadeSeconds, false);
        source.Stop();
        battleFade = null;
    }

    // ------------------------------------------------------------------ helpers

    private static IEnumerator Fade(AudioSource source, float target, float seconds, bool pauseWhenSilent)
    {
        float start = source.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime; // keeps fading even if the game is paused
            source.volume = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        source.volume = target;
        if (pauseWhenSilent && target <= 0.001f) source.Pause();
    }
}
