using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class GameOverController : MonoBehaviour
{
    // Set by BattleManager before opening Game Over.
    public static string RetrySceneName { get; set; }

    [Header("Scene names")]
    [SerializeField] private string mainMenuScene = "Main Menu";

    [Header("Menu text")]
    [SerializeField] private TMP_Text retryText;
    [SerializeField] private TMP_Text mainMenuText;

    [Header("Colors")]
    [SerializeField] private Color selectedColor =
        new Color(0.2f, 1f, 1f, 1f);

    [SerializeField] private Color unselectedColor = Color.white;

    private int selectedIndex;
    private int openedFrame;
    private bool isLoading;

    private void OnEnable()
    {
        selectedIndex = 0;
        openedFrame = Time.frameCount;
        isLoading = false;

        RefreshSelection();
    }

    private void Update()
    {
        // Prevent the Z press that opened this scene
        // from immediately selecting Retry.
        if (isLoading || Time.frameCount <= openedFrame)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame ||
            keyboard.downArrowKey.wasPressedThisFrame)
        {
            selectedIndex = 1 - selectedIndex;
            RefreshSelection();
        }

        if (keyboard.zKey.wasPressedThisFrame)
            ConfirmSelection();
    }

    private void RefreshSelection()
    {
        if (retryText != null)
        {
            retryText.text = selectedIndex == 0
                ? "> RETRY"
                : "  RETRY";

            retryText.color = selectedIndex == 0
                ? selectedColor
                : unselectedColor;
        }

        if (mainMenuText != null)
        {
            mainMenuText.text = selectedIndex == 1
                ? "> MAIN MENU"
                : "  MAIN MENU";

            mainMenuText.color = selectedIndex == 1
                ? selectedColor
                : unselectedColor;
        }
    }

    private void ConfirmSelection()
    {
        string destination = selectedIndex == 0
            ? RetrySceneName
            : mainMenuScene;

        if (string.IsNullOrWhiteSpace(destination))
        {
            Debug.LogWarning(
                "No retry level was recorded. Start a battle and " +
                "reach Game Over through player defeat to test Retry.",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError(
                $"Cannot load '{destination}'. Add it to the build scene list.",
                this
            );
            return;
        }

        isLoading = true;
        Time.timeScale = 1f;

        SceneManager.LoadScene(destination);
    }
}