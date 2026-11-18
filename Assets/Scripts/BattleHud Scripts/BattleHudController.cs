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

public sealed class BattleHudController : MonoBehaviour
{
    public bool CommandSelectionEnabled { get; set; } = true;

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
    [SerializeField] private Color unselectedTint =
        new Color(0.58f, 0.58f, 0.58f, 1f);

    [Header("Battle input actions")]
    [Tooltip("Assign UI/Navigate, UI/Submit, and UI/Cancel.")]
    [SerializeField] private InputActionReference navigateAction;
    [SerializeField] private InputActionReference submitAction;
    [SerializeField] private InputActionReference cancelAction;

    [Header("Battle menu panels")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject commandRow;
    [SerializeField] private GameObject attackSelectionPanel;
    [SerializeField] private GameObject targetSelectionPanel;
    [SerializeField] private GameObject playerStatusPanel;

    [Header("Attack selection")]
    [SerializeField] private TMP_Text magicAttackText;

    [Header("Target selection")]
    [SerializeField] private TMP_Text targetNameText;
    [SerializeField] private TMP_Text targetHPText;
    [SerializeField] private Image targetHPFill;

    [Header("Item selection")]
    [SerializeField] private GameObject itemSelectionPanel;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;
    [SerializeField] private GameObject itemSelectionArrow;

    [Header("Item result layout")]
    [SerializeField, Min(0f)] private float itemResultExtraHeight = 120f;
    [SerializeField, Min(1f)] private float itemResultFontSize = 32f;
    [SerializeField] private Vector2 itemResultStatusOffset = new Vector2(0f, -150f);
    [SerializeField, Min(0f)] private float itemResultTextPadding = 20f;

    private bool itemResultLayoutActive;
    private Vector2 savedDialogueSize, savedDialoguePosition;
    private Vector2 savedTextAnchorMin, savedTextAnchorMax;
    private Vector2 savedTextOffsetMin, savedTextOffsetMax;
    private float savedDialogueFontSize;
    private bool savedDialogueAutoSize;

    [Header("Dodge layout")]
    [SerializeField] private RectTransform dodgeStatusAnchor;

    private Vector3 normalStatusLocalPosition;
    private bool statusPositionSaved;
    public event Action<BattleCommand> CommandChosen;
    public event Action CancelChosen;
    public event Action<int> ItemNavigation;
    private bool itemSelectionActive;

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

        ShowMainMenu();
        SetVisible(false);
        RefreshCommandHighlight();
    }

    private void OnEnable()
    {
        if (submitAction != null && submitAction.action != null)
            submitAction.action.performed += OnSubmit;

        if (cancelAction != null && cancelAction.action != null)
            cancelAction.action.performed += OnCancel;

        EnableAction(navigateAction, true);
        EnableAction(submitAction, true);
        EnableAction(cancelAction, true);
    }

    private void OnDisable()
    {
        if (submitAction != null && submitAction.action != null)
            submitAction.action.performed -= OnSubmit;

        if (cancelAction != null && cancelAction.action != null)
            cancelAction.action.performed -= OnCancel;

        EnableAction(navigateAction, false);
        EnableAction(submitAction, false);
        EnableAction(cancelAction, false);
    }

    private void Update()
    {
        if (!isVisible || (!CommandSelectionEnabled && !itemSelectionActive) ||
            navigateAction == null || navigateAction.action == null)
        {
            lastNavigate = Vector2.zero;
            return;
        }

        Vector2 input = navigateAction.action.ReadValue<Vector2>();

        if (itemSelectionActive)
        {
            if (Mathf.Abs(input.y) < 0.5f)
            {
                lastNavigate = Vector2.zero;
                return;
            }
            if (Mathf.Abs(lastNavigate.y) < 0.5f)
                ItemNavigation?.Invoke(input.y > 0f ? -1 : 1);
            lastNavigate = input;
            return;
        }

        if (Mathf.Abs(input.x) < 0.5f)
        {
            lastNavigate = Vector2.zero;
            return;
        }

        // Move once per key press.
        if (Mathf.Abs(lastNavigate.x) < 0.5f)
        {
            int direction = input.x > 0f ? 1 : -1;

            selectedIndex =
                (selectedIndex + direction + Commands.Length)
                % Commands.Length;

            RefreshCommandHighlight();
        }

        lastNavigate = input;
    }

    public void BeginBattle(
        Sprite enemySprite,
        string enemyName,
        string openingLine)
    {
        ShowMainMenu();

        if (enemyImage != null)
        {
            enemyImage.sprite = enemySprite;
            enemyImage.enabled = enemySprite != null;
        }

        if (enemyNameText != null)
            enemyNameText.text = enemyName;

        SetDialogue(openingLine);

        selectedIndex = 0;
        lastNavigate = Vector2.zero;

        RefreshCommandHighlight();
        SetVisible(true);
    }

    public void ShowMainMenu()
    {
        SetMenuPanels(true, true, false, false);
        CommandSelectionEnabled = true;
        lastNavigate = Vector2.zero;
    }

    public void ShowAttackSelection()
    {
        SetMenuPanels(false, false, true, false);
        CommandSelectionEnabled = false;
        lastNavigate = Vector2.zero;

        // Physical Attack is the only available option for now.
        if (magicAttackText != null)
        {
            magicAttackText.color =
                new Color(0.4f, 0.4f, 0.4f, 1f);
        }
    }

    public void ShowTargetSelection(Monster target)
    {
        if (target == null || target.Stats == null)
            return;

        if (targetNameText != null)
            targetNameText.text = target.DisplayName;

        SetMeter(
            targetHPText,
            targetHPFill,
            "HP",
            target.Stats.CurrentHP,
            target.Stats.MaxHP
        );

        SetMenuPanels(false, false, false, true);
        CommandSelectionEnabled = false;
        lastNavigate = Vector2.zero;
    }

    public void ShowDialogueOnly()
    {
        SetMenuPanels(true, false, false, false);
        CommandSelectionEnabled = false;
        lastNavigate = Vector2.zero;
    }

    private void SetMenuPanels(
        bool showDialogue,
        bool showCommands,
        bool showAttacks,
        bool showTargets)
    {
        RestoreItemResultLayout();
        itemSelectionActive = false;
        if (itemSelectionPanel != null)
            itemSelectionPanel.SetActive(false);
        RestoreStatusPosition();
        if (dialoguePanel != null)
            dialoguePanel.SetActive(showDialogue);

        if (commandRow != null)
            commandRow.SetActive(showCommands);

        if (attackSelectionPanel != null)
            attackSelectionPanel.SetActive(showAttacks);

        if (targetSelectionPanel != null)
            targetSelectionPanel.SetActive(showTargets);

        if (playerStatusPanel != null)
            playerStatusPanel.SetActive(showCommands || showDialogue);
    }

    public void ShowItemSelection(string itemName, string description, bool hasItem = true)
    {
        bool panelReady = itemSelectionPanel != null &&
            itemNameText != null && itemDescriptionText != null;

        if (!itemSelectionActive)
        {
            SetMenuPanels(!panelReady, false, false, false);
            CommandSelectionEnabled = false;
            lastNavigate = Vector2.zero;
            itemSelectionActive = true;

            // Hide player status while browsing items.
            if (playerStatusPanel != null)
                playerStatusPanel.SetActive(false);
        }

        if (panelReady)
        {
            itemSelectionPanel.SetActive(true);
            itemNameText.text = itemName;
            itemDescriptionText.text = description;
            if (itemSelectionArrow != null)
                itemSelectionArrow.SetActive(hasItem);
        }
        else
        {
            // Keep the menu usable until its new Inspector fields are assigned.
            SetDialogue(itemName + "\n" + description);
        }
    }

    public void ShowItemResult(string message)
    {
        ShowDialogueOnly();
        SetDialogue(message);

        RectTransform panel = dialoguePanel != null
            ? dialoguePanel.GetComponent<RectTransform>() : null;
        if (panel == null || dialogueText == null)
            return;

        savedDialogueSize = panel.sizeDelta;
        savedDialoguePosition = panel.anchoredPosition;
        RectTransform textRect = dialogueText.rectTransform;
        savedTextAnchorMin = textRect.anchorMin;
        savedTextAnchorMax = textRect.anchorMax;
        savedTextOffsetMin = textRect.offsetMin;
        savedTextOffsetMax = textRect.offsetMax;
        savedDialogueFontSize = dialogueText.fontSize;
        savedDialogueAutoSize = dialogueText.enableAutoSizing;
        itemResultLayoutActive = true;

        // Extend downward, keeping the top edge below the enemy.
        float extraHeight = Mathf.Max(0f, itemResultExtraHeight);
        panel.sizeDelta = savedDialogueSize + new Vector2(0f, extraHeight);
        panel.anchoredPosition = savedDialoguePosition -
            new Vector2(0f, extraHeight * (1f - panel.pivot.y));

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        float padding = Mathf.Max(0f, itemResultTextPadding);
        textRect.offsetMin = new Vector2(padding, padding);
        textRect.offsetMax = new Vector2(-padding, -padding);
        dialogueText.enableAutoSizing = false;
        dialogueText.fontSize = Mathf.Max(1f, itemResultFontSize);

        if (playerStatusPanel != null)
        {
            Transform status = playerStatusPanel.transform;
            normalStatusLocalPosition = status.localPosition;
            statusPositionSaved = true;
            status.localPosition = normalStatusLocalPosition +
                new Vector3(itemResultStatusOffset.x, itemResultStatusOffset.y, 0f);
        }
    }

    private void RestoreItemResultLayout()
    {
        if (!itemResultLayoutActive)
            return;

        if (dialoguePanel != null)
        {
            RectTransform panel = dialoguePanel.GetComponent<RectTransform>();
            if (panel != null)
            {
                panel.sizeDelta = savedDialogueSize;
                panel.anchoredPosition = savedDialoguePosition;
            }
        }
        if (dialogueText != null)
        {
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = savedTextAnchorMin;
            textRect.anchorMax = savedTextAnchorMax;
            textRect.offsetMin = savedTextOffsetMin;
            textRect.offsetMax = savedTextOffsetMax;
            dialogueText.fontSize = savedDialogueFontSize;
            dialogueText.enableAutoSizing = savedDialogueAutoSize;
        }
        itemResultLayoutActive = false;
    }

    public void SetDialogue(string line)
    {
        if (dialogueText != null)
            dialogueText.text = line;
    }

    public void SetPlayerStatus(
        string playerName,
        int level,
        int currentHp,
        int maximumHp,
        int currentMp,
        int maximumMp)
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
        ShowMainMenu();
        SetVisible(false);
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        // Keep sending Z presses during submenus and dialogue.
        // BattleManager decides what they mean in the current state.
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
            Image commandImage = commandImages[i];

            if (commandImage == null)
                continue;

            commandImage.color =
                i == selectedIndex ? selectedTint : unselectedTint;
        }
    }

    private static void SetMeter(
        TMP_Text label,
        Image fill,
        string prefix,
        int current,
        int maximum)
    {
        maximum = Mathf.Max(0, maximum);
        current = Mathf.Clamp(current, 0, maximum);

        if (label != null)
            label.text = $"{prefix}  {current}/{maximum}";

        if (fill != null)
        {
            fill.fillAmount =
                maximum == 0 ? 0f : (float)current / maximum;
        }
    }

    private static void EnableAction(
        InputActionReference actionReference,
        bool enable)
    {
        if (actionReference == null || actionReference.action == null)
            return;

        if (enable)
            actionReference.action.Enable();
        else
            actionReference.action.Disable();
    }
    public void HideActionPanels()
    {   
    SetMenuPanels(false, false, false, false);
    CommandSelectionEnabled = false;
    lastNavigate = Vector2.zero;
    }
    public void ShowDodgePhase()
    {
    SetMenuPanels(false, false, false, false);

    CommandSelectionEnabled = false;
    lastNavigate = Vector2.zero;

    if (playerStatusPanel != null)
    {
        playerStatusPanel.SetActive(true);
        MoveStatusBelowArena();
    }
    }
    private void MoveStatusBelowArena()
{
    if (playerStatusPanel == null || dodgeStatusAnchor == null)
        return;

    RectTransform status =
        playerStatusPanel.GetComponent<RectTransform>();

    if (!statusPositionSaved)
    {
        normalStatusLocalPosition = status.localPosition;
        statusPositionSaved = true;
    }

    status.position = dodgeStatusAnchor.position;
}

    private void RestoreStatusPosition()
    {
    if (!statusPositionSaved || playerStatusPanel == null)
        return;

    playerStatusPanel.transform.localPosition =
        normalStatusLocalPosition;

    statusPositionSaved = false;
    }
}