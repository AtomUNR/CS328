using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum BattleCommand
{
    Fight,
    Item,
    Stats,
    Flee
}

/// <summary>
/// Displays the Battle Tower battle HUD and routes keyboard choices to the
/// battle system. The encounter/battle logic subscribes to CommandChosen.
/// </summary>
public sealed class BattleHudController : MonoBehaviour
{
    [Header("HUD root")]
    [SerializeField] private CanvasGroup hudGroup;

    [Header("Enemy and dialogue")]
    [SerializeField] private Image enemyImage;
    [SerializeField] private TMP_Text enemyNameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Player status")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text mpText;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image mpFill;

    [Header("Commands in order: Fight, Item, Stats, Flee")]
    [SerializeField] private Image[] commandImages = new Image[4];
    [SerializeField] private Color selectedTint = Color.white;
    [SerializeField] private Color unselectedTint = new Color(0.58f, 0.58f, 0.58f, 1f);

    [Header("Battle input actions")]
    [Tooltip("Assign UI/Navigate (Vector2), UI/Submit (Z), and UI/Cancel (X).")]
    [SerializeField] private InputActionReference navigateAction;
    [SerializeField] private InputActionReference submitAction;
    [SerializeField] private InputActionReference cancelAction;

    public event Action<BattleCommand> CommandChosen;
    public event Action CancelChosen;

    private int selectedIndex;
    private bool isVisible;
    private Vector2 lastNavigate;

    private static readonly BattleCommand[] Commands =
    {
        BattleCommand.Fight,
        BattleCommand.Item,
        BattleCommand.Stats,
        BattleCommand.Flee
    };

    private void Awake()
    {
        if (hudGroup == null)
            hudGroup = GetComponent<CanvasGroup>();

        SetVisible(false);
        RefreshCommandHighlight();
    }

    private void OnEnable()
    {
        EnableAction(navigateAction, true);
        EnableAction(submitAction, true);
        EnableAction(cancelAction, true);

        if (submitAction != null)
            submitAction.action.performed += OnSubmit;
        if (cancelAction != null)
            cancelAction.action.performed += OnCancel;
    }

    private void OnDisable()
    {
        if (submitAction != null)
            submitAction.action.performed -= OnSubmit;
        if (cancelAction != null)
            cancelAction.action.performed -= OnCancel;

        EnableAction(navigateAction, false);
        EnableAction(submitAction, false);
        EnableAction(cancelAction, false);
    }

    private void Update()
    {
        if (!isVisible || navigateAction == null)
            return;

        Vector2 input = navigateAction.action.ReadValue<Vector2>();
        if (Mathf.Abs(input.x) < 0.5f)
        {
            lastNavigate = Vector2.zero;
            return;
        }

        // Move once per key press; release and press again to move again.
        if (Mathf.Abs(lastNavigate.x) < 0.5f)
        {
            selectedIndex = (selectedIndex + (input.x > 0 ? 1 : -1) + Commands.Length)
                            % Commands.Length;
            RefreshCommandHighlight();
        }
        lastNavigate = input;
    }

    public void BeginBattle(Sprite enemySprite, string enemyName, string openingLine)
    {
        if (enemyImage != null)
        {
            enemyImage.sprite = enemySprite;
            enemyImage.enabled = enemySprite != null;
        }

        if (enemyNameText != null)
            enemyNameText.text = enemyName;
        SetDialogue(openingLine);
        selectedIndex = 0;
        RefreshCommandHighlight();
        SetVisible(true);
    }

    public void SetDialogue(string line)
    {
        if (dialogueText != null)
            dialogueText.text = line;
    }

    public void SetPlayerStatus(string playerName, int level,
        int currentHp, int maximumHp, int currentMp, int maximumMp)
    {
        if (playerNameText != null)
            playerNameText.text = playerName;
        if (levelText != null)
            levelText.text = $"LV {level}";

        SetMeter(hpText, hpFill, "HP", currentHp, maximumHp);
        SetMeter(mpText, mpFill, "MP", currentMp, maximumMp);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (hudGroup == null)
            return;

        hudGroup.alpha = visible ? 1f : 0f;
        hudGroup.interactable = visible;
        hudGroup.blocksRaycasts = visible;
    }

    public void EndBattle()
    {
        SetVisible(false);
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (isVisible)
            CommandChosen?.Invoke(Commands[selectedIndex]);
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (isVisible)
            CancelChosen?.Invoke();
    }

    private void RefreshCommandHighlight()
    {
        if (commandImages == null)
            return;

        for (int i = 0; i < commandImages.Length; i++)
        {
            Image image = commandImages[i];
            if (image == null)
                continue;

            image.color = i == selectedIndex ? selectedTint : unselectedTint;
        }
    }

    private static void SetMeter(TMP_Text label, Image fill, string prefix,
        int current, int maximum)
    {
        maximum = Mathf.Max(0, maximum);
        current = Mathf.Clamp(current, 0, maximum);

        if (label != null)
            label.text = $"{prefix}  {current}/{maximum}";
        if (fill != null)
            fill.fillAmount = maximum == 0 ? 0f : (float)current / maximum;
    }

    private static void EnableAction(InputActionReference actionReference, bool enable)
    {
        if (actionReference == null)
            return;

        if (enable)
            actionReference.action.Enable();
        else
            actionReference.action.Disable();
    }
}
