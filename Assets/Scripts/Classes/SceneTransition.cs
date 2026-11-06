using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fade-to-black scene change. Call SceneTransition.Load("Scene Name").
/// Builds its own overlay in code, so there is nothing to set up in a scene.
/// Carries the Player (stats, XP, money, inventory) across via PersistentPlayer.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    private static SceneTransition instance;
    private static bool busy;

    private Image overlay;

    public static bool IsBusy => busy;

    public static void Load(string sceneName, float fadeDuration = 0.6f)
    {
        if (busy || string.IsNullOrWhiteSpace(sceneName)) return;
        if (instance == null) Create();
        instance.StartCoroutine(instance.Run(sceneName, Mathf.Max(0f, fadeDuration)));
    }

    private static void Create()
    {
        var go = new GameObject("SceneTransition");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneTransition>();

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var imgGo = new GameObject("Fade", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        instance.overlay = imgGo.AddComponent<Image>();
        instance.overlay.color = new Color(0, 0, 0, 0);
        instance.overlay.raycastTarget = false;
    }

    private IEnumerator Run(string sceneName, float duration)
    {
        busy = true;

        // Freeze the player so they can't walk during the fade.
        Player player = FindAnyObjectByType<Player>();
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (movement != null) movement.enabled = false;
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        overlay.raycastTarget = true;
        yield return Fade(0f, 1f, duration);

        if (player != null) PersistentPlayer.Keep(player, sceneName);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError("SceneTransition: scene '" + sceneName + "' is not in Build Settings.");
            if (movement != null) movement.enabled = true;
            yield return Fade(1f, 0f, duration);
            overlay.raycastTarget = false;
            busy = false;
            yield break;
        }
        while (!op.isDone) yield return null;
        yield return null;   // let Start() / SpawnAtMarker run before revealing

        if (movement != null) movement.enabled = true;
        yield return Fade(1f, 0f, duration);
        overlay.raycastTarget = false;
        busy = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        Color c = overlay.color; c.a = a; overlay.color = c;
    }
}
