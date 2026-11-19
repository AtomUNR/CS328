using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu music that survives scene changes. Put it on an object in the Main Menu scene
/// (and optionally the Level Select scene; extra copies delete themselves).
/// The music keeps playing, without restarting, while the loaded scene is listed in
/// "Play In Scenes", and fades out and removes itself when any other scene loads.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PersistantMenuMusic : MonoBehaviour
{
    private static PersistantMenuMusic instance;

    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float volume = 0.4f;
    [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1.2f;
    [SerializeField] private string[] playInScenes = { "Main Menu", "Level Select" };

    private AudioSource source;
    private bool fadingOut;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);   // already playing from an earlier scene: keep that one
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.clip = music;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        if (music != null) source.Play();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (fadingOut) return;
        foreach (string name in playInScenes)
            if (scene.name == name) return;
        StartCoroutine(FadeOutAndDie());
    }

    private IEnumerator FadeOutAndDie()
    {
        fadingOut = true;
        float start = source.volume;
        float t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, 0f, t / fadeOutSeconds);
            yield return null;
        }
        Destroy(gameObject);
    }
}
