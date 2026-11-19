using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Attach to the active BattleManager object. UI Parent is your BattleHUD RectTransform.
public sealed class ChargedStrikeController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform uiParent;
    [SerializeField] private TMP_FontAsset menuFont;
    [SerializeField] private Vector2 panelSize = new Vector2(1760f, 440f);
    [SerializeField] private float panelBottom = 120f;
    [SerializeField, Min(100f)] private float ringSize = 320f;

    [Header("Charge")]
    [SerializeField, Min(1)] private int mpCost = 5;
    [SerializeField, Min(0.5f)] private float chargeDuration = 2.2f;

    public event Action<float, string> Finished;
    public event Action Unavailable;
    public int MPCost => Mathf.Max(1, mpCost);
    public bool SelectedCharged { get; private set; }
    public bool IsSelecting => mode == Mode.Selecting;
    public bool CanCancel => mode == Mode.Ready || mode == Mode.WaitForRelease;
    public bool IsReady => isActiveAndEnabled && uiParent != null &&
        uiParent.gameObject.activeInHierarchy;

    private enum Mode { Hidden, Selecting, WaitForRelease, Ready, Charging, Result }
    private Mode mode;
    private CharacterStats stats;
    private GameObject root, selectionPanel, chargePanel;
    private TMP_Text basicText, chargedText, selectionNote, chargeTitle, centerText, prompt, footer;
    private ChargeRingGraphic ring;
    private float startedAt;
    private int openedFrame;
    private static readonly Color Ivory = new Color32(245, 236, 213, 255);
    private static readonly Color Cyan = new Color32(86, 230, 236, 255);
    private static readonly Color Gold = new Color32(255, 212, 90, 255);

    public bool ShowSelection(CharacterStats playerStats, bool reset)
    {
        if (playerStats == null || !EnsureUI()) return false;
        stats = playerStats;
        if (reset) SelectedCharged = false;
        mode = Mode.Selecting;
        openedFrame = Time.frameCount;
        root.SetActive(true);
        selectionPanel.SetActive(true);
        chargePanel.SetActive(false);
        UpdateSelection();
        UpdateFooter();
        return true;
    }

    public void ShowNotEnoughMP()
    {
        selectionNote.text = $"Not enough MP. Need {MPCost} MP.";
        BattleTowerAudio.Play(TowerSound.CannotUse);
    }

    public bool BeginAttack(CharacterStats playerStats)
    {
        if (playerStats == null || playerStats.CurrentMP < MPCost || !EnsureUI()) return false;
        stats = playerStats;
        mode = Mode.WaitForRelease;
        openedFrame = Time.frameCount;
        root.SetActive(true);
        selectionPanel.SetActive(false);
        chargePanel.SetActive(true);
        chargeTitle.text = "CHARGED STRIKE";
        centerText.text = "";
        centerText.color = Ivory;
        prompt.text = "Release Z, then hold Z to charge.";
        ring.SetCharge(0f, Cyan);
        UpdateFooter();
        return true;
    }

    private void Update()
    {
        if (mode == Mode.Hidden || stats == null) return;
        UpdateFooter();
        if (Time.timeScale <= 0f || Time.frameCount == openedFrame) return;
        Keyboard keys = Keyboard.current;
        if (keys == null) return;
        if (mode == Mode.Selecting)
        {
            if (keys.upArrowKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame)
            {
                SelectedCharged = !SelectedCharged;
                UpdateSelection();
                BattleTowerAudio.Play(TowerSound.MenuMove);
            }
            return;
        }
        if (mode == Mode.WaitForRelease)
        {
            if (!keys.zKey.isPressed)
            {
                mode = Mode.Ready;
                prompt.text = "Hold Z. Release in the gold band.";
            }
            return;
        }
        if (mode == Mode.Ready)
        {
            if (!keys.zKey.wasPressedThisFrame) return;
            if (!stats.TrySpendMP(MPCost))
            {
                CancelAttack();
                Unavailable?.Invoke();
                return;
            }
            mode = Mode.Charging;
            startedAt = Time.time;
            prompt.text = "Release in the gold band.";
            UpdateFooter();
        }
        if (mode != Mode.Charging) return;
        float progress = (Time.time - startedAt) / Mathf.Max(0.5f, chargeDuration);
        ring.SetCharge(progress, Cyan);
        if (progress >= 1f)
        {
            Complete(0f, "MISS");
            return;
        }
        if (keys.zKey.wasReleasedThisFrame || !keys.zKey.isPressed)
        {
            float score = ChargeRingGraphic.Score(progress);
            Complete(score, score > 1f ? "PERFECT!" : score > 0f ? "GOOD HIT" : "MISS");
        }
    }

    private void Complete(float multiplier, string label)
    {
        if (mode != Mode.Charging) return;
        mode = Mode.Result; // Stops all charging input before calling battle code.
        Finished?.Invoke(multiplier, label);
    }

    public void ShowResult(string label, int actualDamage)
    {
        if (root == null) return;
        mode = Mode.Result;
        chargeTitle.text = label;
        centerText.text = $"{actualDamage}\nDAMAGE";
        centerText.color = actualDamage == 0 ? new Color32(239, 115, 124, 255) : Gold;
        prompt.text = "Press Z to continue.";
        UpdateFooter();
    }

    public void CancelAttack()
    {
        mode = Mode.Hidden;
        if (root != null) root.SetActive(false);
    }

    private void UpdateSelection()
    {
        basicText.text = (SelectedCharged ? "  " : "> ") + "Physical Attack     0 MP";
        chargedText.text = (SelectedCharged ? "> " : "  ") + $"Charged Strike      {MPCost} MP";
        basicText.color = SelectedCharged ? Ivory : Cyan;
        chargedText.color = SelectedCharged ? Cyan : Ivory;
        selectionNote.text = SelectedCharged ? "Hold and release. One attempt." : "Stop the timing bar.";
    }

    private void UpdateFooter()
    {
        if (footer != null && stats != null)
            footer.text = $"HP {stats.CurrentHP}/{stats.MaxHP}          MP {stats.CurrentMP}/{stats.MaxMP}";
    }

    private bool EnsureUI()
    {
        if (!IsReady)
        {
            Debug.LogError("Charged Strike: assign the active BattleHUD RectTransform as UI Parent.", this);
            return false;
        }
        if (root != null) return true;
        RectTransform holder = MakeRect("Charged Strike UI", uiParent, Vector2.zero, Vector2.zero);
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.offsetMin = holder.offsetMax = Vector2.zero;
        root = holder.gameObject;
        root.transform.SetAsLastSibling();

        RectTransform choose = MakePanel("Attack Choices", holder, new Vector2(panelSize.x, 250f), panelBottom);
        selectionPanel = choose.gameObject;
        Label("Title", choose, new Vector2(0f, 85f), new Vector2(panelSize.x - 80f, 45f), "CHOOSE ATTACK", 32f);
        basicText = Label("Basic", choose, new Vector2(0f, 25f), new Vector2(panelSize.x - 120f, 45f), "", 30f);
        chargedText = Label("Charged", choose, new Vector2(0f, -30f), new Vector2(panelSize.x - 120f, 45f), "", 30f);
        selectionNote = Label("Description", choose, new Vector2(0f, -90f), new Vector2(panelSize.x - 80f, 40f), "", 24f);

        RectTransform panel = MakePanel("Charge Ring Panel", holder, panelSize, panelBottom);
        chargePanel = panel.gameObject;
        chargeTitle = Label("Title", panel, new Vector2(0f, panelSize.y * 0.5f - 40f), new Vector2(panelSize.x - 80f, 45f), "CHARGED STRIKE", 32f);
        RectTransform ringRect = MakeRect("Charge Ring", panel, Vector2.zero, new Vector2(ringSize, ringSize));
        ring = ringRect.gameObject.AddComponent<ChargeRingGraphic>();
        ring.raycastTarget = false;
        centerText = Label("Damage Result", panel, Vector2.zero, new Vector2(210f, 100f), "", 30f);
        prompt = Label("Charge Prompt", panel, new Vector2(0f, -panelSize.y * 0.5f + 40f), new Vector2(panelSize.x - 80f, 45f), "", 26f);
        RectTransform status = MakePanel("Charge Status", holder, new Vector2(panelSize.x, 60f), panelBottom - 80f);
        footer = Label("HP and MP", status, Vector2.zero, new Vector2(panelSize.x - 60f, 45f), "", 28f);
        root.SetActive(false);
        return true;
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform MakePanel(string name, Transform parent, Vector2 size, float bottom)
    {
        RectTransform rect = MakeRect(name, parent, new Vector2(0f, bottom + size.y * 0.5f), size);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        Image border = rect.gameObject.AddComponent<Image>();
        border.color = Ivory;
        border.raycastTarget = false;
        RectTransform interior = MakeRect("Black Fill", rect, Vector2.zero, size - Vector2.one * 8f);
        Image fill = interior.gameObject.AddComponent<Image>();
        fill.color = Color.black;
        fill.raycastTarget = false;
        return rect;
    }

    private TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text, float fontSize)
    {
        RectTransform rect = MakeRect(name, parent, position, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (menuFont != null) label.font = menuFont;
        label.fontSize = fontSize;
        label.color = Ivory;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.richText = false;
        label.text = text;
        return label;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && mode == Mode.Charging) Complete(0f, "MISS");
    }
    private void OnDisable() { CancelAttack(); }
    private void OnDestroy() { if (root != null) Destroy(root); }
}
