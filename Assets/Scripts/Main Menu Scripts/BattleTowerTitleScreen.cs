using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Connect the public methods to the title screen's UI Buttons in the Inspector.
/// </summary>
public class BattleTowerTitleScreen : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string firstGameScene = "Game";

    [Header("Begin transition")]
    [Tooltip("Optional full-screen background image. Set its RectTransform pivot to the tower entrance for a zoom toward the doorway.")]
    [SerializeField] private RectTransform backgroundToZoom;
    [SerializeField, Min(1f)] private float zoomScale = 1.35f;
    [SerializeField, Min(0f)] private float transitionDuration = 0.9f;

    [Header("Settings")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip clickSound;

    private bool isStarting;
    private Vector3 backgroundStartScale;

    private void Awake()
    {
        if (backgroundToZoom != null)
            backgroundStartScale = backgroundToZoom.localScale;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void BeginGame()
    {
        if (isStarting)
            return;

        isStarting = true;
        PlayClick();
        StartCoroutine(BeginGameTransition());
    }

    private IEnumerator BeginGameTransition()
    {
        if (backgroundToZoom != null && transitionDuration > 0f)
        {
            float elapsed = 0f;
            Vector3 targetScale = backgroundStartScale * zoomScale;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                // Ease in and out for a gentle title-screen transition.
                t = t * t * (3f - 2f * t);
                backgroundToZoom.localScale = Vector3.Lerp(backgroundStartScale, targetScale, t);
                yield return null;
            }
        }
        else if (transitionDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(transitionDuration);
        }

        if (!string.IsNullOrWhiteSpace(firstGameScene))
            SceneManager.LoadScene(firstGameScene);
        else
            Debug.LogError("BattleTowerTitleScreen: Set the first game scene name in the Inspector.");
    }

    public void OpenSettings()
    {
        PlayClick();
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        PlayClick();
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>Connect a UI Slider's On Value Changed event to this method.</summary>
    public void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
    }

    public void QuitGame()
    {
        PlayClick();
        Application.Quit();
    }

    private void PlayClick()
    {
        if (uiAudioSource != null && clickSound != null)
            uiAudioSource.PlayOneShot(clickSound);
    }
}
