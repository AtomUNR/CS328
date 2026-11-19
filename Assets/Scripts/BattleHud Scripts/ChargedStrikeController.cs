using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Attach to the active BattleManager object. UI Parent is your BattleHUD RectTransform.
// All positions are relative to the bottom-centre of the UI Parent.
public sealed class ChargedStrikeController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform uiParent;
    [SerializeField] private TMP_FontAsset menuFont;
    [SerializeField] private Vector2 panelSize = new Vector2(1200f, 468f);
    [Tooltip("Distance from the bottom of the UI Parent to the bottom of the charge panel.")]
    [SerializeField] private float panelBottom = 11f;
    [SerializeField, Min(100f)] private float ringSize = 288f;

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
    private Player statsOwner;
    private GameObject root, selectionPanel, chargePanel;
    private TMP_Text basicText, chargedText, selectionNote, chargeTitle, centerMain, centerSub,
        resultLine, prompt, nameText, hpText, mpText;
    private RectTransform hpFill, mpFill;
    private ChargeRingGraphic ring;
    private float startedAt;
    private float shownProgress; // progress of the ring as last drawn
    private int openedFrame;

    private const float FooterHeight = 80f;
    private const float FooterGap = 15f;
    private static readonly Color Ivory = new Color32(245, 236, 213, 255);
    private static readonly Color Cyan = new Color32(86, 230, 236, 255);
    private static readonly Color Gold = new Color32(255, 212, 90, 255);
    private static readonly Color Red = new Color32(239, 115, 124, 255);
    private static readonly Color Muted = new Color32(176, 170, 150, 255);
    private static readonly Color Tick = new Color32(110, 106, 98, 255);
    private static readonly Color ButtonFill = new Color32(24, 24, 24, 255);

    public bool ShowSelection(CharacterStats playerStats, bool reset)
    {
        if (playerStats == null || !EnsureUI()) return false;
        SetStats(playerStats);
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
        SetStats(playerStats);
        mode = Mode.WaitForRelease;
        openedFrame = Time.frameCount;
        root.SetActive(true);
        selectionPanel.SetActive(false);
        chargePanel.SetActive(true);
        chargeTitle.text = "CHARGED STRIKE";
        SetCenter("", "", Ivory);
        SetResultLine("Release Z, then hold Z to charge.", Muted);
        prompt.text = "RELEASE Z";
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
                SetResultLine("Hold Z. Release in the gold band.", Muted);
                prompt.text = "HOLD Z";
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
            shownProgress = 0f;
            SetCenter("", "", Ivory);
            SetResultLine("Release in the gold band.", Muted);
            prompt.text = "RELEASE Z";
            UpdateFooter();
        }
        if (mode != Mode.Charging) return;
        float progress = (Time.time - startedAt) / Mathf.Max(0.5f, chargeDuration);
        if (progress >= 1f)
        {
            Complete(0f, "MISS");
            return;
        }
        if (keys.zKey.wasReleasedThisFrame || !keys.zKey.isPressed)
        {
            // Score the ring exactly as the player last saw it, not a frame further along.
            float score = ChargeRingGraphic.Score(shownProgress);
            Complete(score, score >= ChargeRingGraphic.PerfectMultiplier ? "PERFECT!" : score > 0f ? "GOOD HIT" : "MISS");
            return;
        }
        shownProgress = progress;
        ring.SetCharge(progress, Cyan);
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
        bool missed = actualDamage <= 0;
        Color tint = missed ? Red : label.StartsWith("PERFECT") ? Gold : Cyan;
        chargeTitle.text = "CHARGED STRIKE";
        SetCenter($"{actualDamage} DMG", missed ? "missed" : "released", missed ? Red : Ivory);
        SetResultLine(label, tint);
        prompt.text = $"PRESS Z {Glyph('·', "-")} CONTINUE";
        UpdateFooter();
    }

    public void CancelAttack()
    {
        mode = Mode.Hidden;
        if (root != null) root.SetActive(false);
    }

    private void SetStats(CharacterStats playerStats)
    {
        stats = playerStats;
        statsOwner = playerStats.GetComponentInParent<Player>();
    }

    private void SetCenter(string main, string sub, Color mainColor)
    {
        centerMain.text = main;
        centerMain.color = mainColor;
        centerSub.text = sub;
    }

    private void SetResultLine(string text, Color color)
    {
        resultLine.text = text;
        resultLine.color = color;
    }

    // Pixel fonts often lack typographic symbols; fall back to plain ASCII.
    private string Glyph(char symbol, string fallback)
    {
        if (menuFont == null) return symbol.ToString();
        return menuFont.HasCharacter(symbol, true, true) ? symbol.ToString() : fallback;
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
        if (stats == null || hpText == null) return;
        nameText.text = statsOwner != null ? $"{statsOwner.DisplayName}  LV {statsOwner.Level}" : "";
        hpText.text = $"HP {stats.CurrentHP}/{stats.MaxHP}";
        mpText.text = $"MP {stats.CurrentMP}/{stats.MaxMP}";
        SetBar(hpFill, Fraction(stats.CurrentHP, stats.MaxHP));
        SetBar(mpFill, Fraction(stats.CurrentMP, stats.MaxMP));
    }

    private static void SetBar(RectTransform fill, float fraction)
    {
        fill.gameObject.SetActive(fraction > 0.001f);
        fill.anchorMax = new Vector2(fraction, 1f);
    }

    private static float Fraction(int current, int max)
    {
        return max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
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

        float footerBottom = panelBottom - FooterHeight - FooterGap;
        float backdropBottom = footerBottom - 40f;
        float backdropTop = panelBottom + panelSize.y + 6f;

        // Opaque backdrop so the normal battle menu never shows around the panels.
        RectTransform backdrop = MakeRect("Backdrop", holder,
            new Vector2(0f, (backdropBottom + backdropTop) * 0.5f),
            new Vector2(2400f, backdropTop - backdropBottom));
        backdrop.anchorMin = backdrop.anchorMax = new Vector2(0.5f, 0f);
        AddImage(backdrop.gameObject, Color.black);

        // Attack choice panel, centred in the same area as the charge panel.
        Vector2 chooseSize = new Vector2(panelSize.x, 250f);
        RectTransform choose = MakePanel("Attack Choices", holder, chooseSize,
            panelBottom + (panelSize.y - chooseSize.y) * 0.5f);
        selectionPanel = choose.gameObject;
        Label("Title", choose, new Vector2(0f, 85f), new Vector2(panelSize.x - 80f, 45f), "CHOOSE ATTACK", 32f);
        basicText = Label("Basic", choose, new Vector2(0f, 25f), new Vector2(panelSize.x - 120f, 45f), "", 30f);
        chargedText = Label("Charged", choose, new Vector2(0f, -30f), new Vector2(panelSize.x - 120f, 45f), "", 30f);
        selectionNote = Label("Description", choose, new Vector2(0f, -90f), new Vector2(panelSize.x - 80f, 40f), "", 24f);

        // Charge panel: title, ring, centre read-out, result line, prompt button.
        RectTransform panel = MakePanel("Charge Ring Panel", holder, panelSize, panelBottom);
        chargePanel = panel.gameObject;
        float half = panelSize.y * 0.5f;
        float ringY = half - 210f;
        chargeTitle = Label("Title", panel, new Vector2(0f, half - 32f), new Vector2(panelSize.x - 80f, 45f), "CHARGED STRIKE", 34f);

        RectTransform ringRect = MakeRect("Charge Ring", panel, new Vector2(0f, ringY), new Vector2(ringSize, ringSize));
        ringRect.gameObject.AddComponent<CanvasRenderer>(); // Graphic subclasses are not guaranteed to add one.
        ring = ringRect.gameObject.AddComponent<ChargeRingGraphic>();
        ring.raycastTarget = false;

        // Compass ticks sit just outside the target band.
        float tickRadius = (ChargeRingGraphic.TargetRadius + ChargeRingGraphic.GoodHalfWidth + 8f) * (ringSize / 192f);
        MakeTick(panel, new Vector2(0f, ringY + tickRadius));
        MakeTick(panel, new Vector2(0f, ringY - tickRadius));
        MakeTick(panel, new Vector2(tickRadius, ringY));
        MakeTick(panel, new Vector2(-tickRadius, ringY));

        centerMain = Label("Center Main", panel, new Vector2(0f, ringY + 12f), new Vector2(ringSize * 0.7f, 36f), "", 26f);
        centerSub = Label("Center Sub", panel, new Vector2(0f, ringY - 18f), new Vector2(ringSize * 0.7f, 26f), "", 15f);
        centerSub.color = Muted;
        resultLine = Label("Result Line", panel, new Vector2(0f, -half + 106f), new Vector2(panelSize.x - 80f, 40f), "", 28f);
        resultLine.richText = true;

        RectTransform button = MakeBox("Prompt Button", panel, new Vector2(0f, -half + 41f), new Vector2(540f, 54f), 3f, ButtonFill);
        prompt = Label("Prompt", button, Vector2.zero, new Vector2(520f, 46f), "", 26f);

        // Footer with name, level, HP and MP bars (matches the mock-up).
        RectTransform footer = MakePanel("Charge Status", holder, new Vector2(panelSize.x, FooterHeight), footerBottom);
        nameText = Label("Name", footer, new Vector2(-410f, 0f), new Vector2(340f, 45f), "", 26f);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        hpText = Label("HP", footer, new Vector2(-95f, 0f), new Vector2(230f, 45f), "", 26f);
        hpFill = MakeBar("HP Bar", footer, new Vector2(100f, 0f), new Vector2(120f, 26f), Red);
        mpText = Label("MP", footer, new Vector2(288f, 0f), new Vector2(230f, 45f), "", 26f);
        mpFill = MakeBar("MP Bar", footer, new Vector2(481f, 0f), new Vector2(120f, 26f), Cyan);

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

    private static Image AddImage(GameObject target, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform MakePanel(string name, Transform parent, Vector2 size, float bottom)
    {
        RectTransform rect = MakeBox(name, parent, new Vector2(0f, bottom + size.y * 0.5f), size, 4f, Color.black);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        return rect;
    }

    // Ivory border with a filled interior.
    private static RectTransform MakeBox(string name, Transform parent, Vector2 position, Vector2 size, float border, Color fill)
    {
        RectTransform rect = MakeRect(name, parent, position, size);
        AddImage(rect.gameObject, Ivory);
        RectTransform interior = MakeRect("Fill", rect, Vector2.zero, size - Vector2.one * border * 2f);
        AddImage(interior.gameObject, fill);
        return rect;
    }

    private static void MakeTick(Transform parent, Vector2 position)
    {
        AddImage(MakeRect("Tick", parent, position, new Vector2(8f, 8f)).gameObject, Tick);
    }

    // Returns the fill rect; its anchorMax.x is driven by the current value.
    private static RectTransform MakeBar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        RectTransform bar = MakeBox(name, parent, position, size, 3f, new Color32(20, 20, 20, 255));
        RectTransform fill = MakeRect("Value", bar, Vector2.zero, Vector2.zero);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        AddImage(fill.gameObject, color);
        // Shrink from the right by moving the anchor; offsets stay as the inner padding.
        return fill;
    }

    private TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text, float fontSize)
    {
        RectTransform rect = MakeRect(name, parent, position, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (menuFont != null) label.font = menuFont;
        label.fontSize = fontSize;
        label.color = Ivory;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
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
