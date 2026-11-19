using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Add to the scene Player, beside PlayerInventory and the movement components.
public sealed class ExplorationInventoryMenu : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_FontAsset menuFont;
    [SerializeField] private Color selectionColor = new Color(0f, 0.95f, 1f);

    private PlayerInventory inventory;
    private CharacterStats stats;
    private CharacterMovement characterMovement;
    private PlayerMovement playerMovement;
    private Rigidbody2D body;
    private bool characterWasEnabled, playerWasEnabled;
    private GameObject canvasObject, panel;
    private TMP_Text countText, descriptionText, healthText, resultText;
    private readonly TMP_Text[] rows = new TMP_Text[PlayerInventory.Capacity];
    private int selectedIndex;
    public bool IsOpen { get; private set; }

    private void Start()
    {
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<CharacterStats>();
        characterMovement = GetComponent<CharacterMovement>();
        playerMovement = GetComponent<PlayerMovement>();
        body = GetComponent<Rigidbody2D>();
        if (inventory == null || stats == null || battleManager == null ||
            (characterMovement == null && playerMovement == null))
        {
            Debug.LogError("Inventory menu: assign Battle Manager and add this component to the Player with inventory, stats and movement.", this);
            enabled = false;
            return;
        }
        BuildMenu();
    }

    private void Update()
    {
        if (panel == null) return;
        if (battleManager == null || battleManager.IsInBattle || stats.CurrentHP <= 0)
        {
            CloseMenu();
            return;
        }
        Keyboard keys = Keyboard.current;
        if (keys == null) return;
        if (keys.cKey.wasPressedThisFrame)
        {
            if (IsOpen) CloseMenu(); else OpenMenu();
            return;
        }
        if (!IsOpen) return;
        if (keys.xKey.wasPressedThisFrame)
        {
            CloseMenu();
            return;
        }
        int direction = keys.downArrowKey.wasPressedThisFrame ? 1 :
            keys.upArrowKey.wasPressedThisFrame ? -1 : 0;
        if (inventory.Count > 0 && direction != 0)
            selectedIndex = (selectedIndex + direction + inventory.Count) % inventory.Count;
        if (keys.zKey.wasPressedThisFrame) UseSelectedItem();
        RefreshMenu();
    }

    private void OpenMenu()
    {
        if (Time.timeScale == 0f || battleManager.IsInBattle || stats.CurrentHP <= 0) return;
        characterWasEnabled = characterMovement != null && characterMovement.enabled;
        playerWasEnabled = playerMovement != null && playerMovement.enabled;
        // Do not open while another interaction has already stopped movement.
        if (!characterWasEnabled && !playerWasEnabled) return;
        if (characterMovement != null) characterMovement.enabled = false;
        if (playerMovement != null) playerMovement.enabled = false;
        if (body != null) body.linearVelocity = Vector2.zero;
        selectedIndex = 0;
        resultText.text = "";
        IsOpen = true;
        panel.SetActive(true);
        RefreshMenu();
    }

    public void CloseMenu()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (panel != null) panel.SetActive(false);
        if (characterMovement != null) characterMovement.enabled = characterWasEnabled;
        if (playerMovement != null) playerMovement.enabled = playerWasEnabled;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    private void OnDisable() { CloseMenu(); }
    private void OnDestroy()
    {
        CloseMenu();
        if (canvasObject != null) Destroy(canvasObject);
    }

    private void UseSelectedItem()
    {
        if (!IsOpen || battleManager.IsInBattle || stats.CurrentHP <= 0 || inventory.Count == 0)
            return;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, inventory.Count - 1);
        ItemData item = inventory.Items[selectedIndex];
        if (item == null) return;
        bool heals = item.Effect == ItemEffect.RestoreHP ||
                     item.Effect == ItemEffect.RestoreHPAndDodgeSpeed;
        if (!heals)
        {
            resultText.text = "This item cannot be used here.";
            return;
        }
        int amount = Mathf.Min(Mathf.Max(0, item.RestoreAmount), stats.MaxHP - stats.CurrentHP);
        if (amount <= 0)
        {
            resultText.text = stats.CurrentHP >= stats.MaxHP ?
                "HP is already full." : "This item cannot restore HP.";
            return;
        }
        int oldHP = stats.CurrentHP;
        if (!inventory.TryRemoveItem(selectedIndex)) return;
        // Exploration uses only the heal. Never apply or queue a dodge speed bonus.
        stats.RestoreHP(amount);
        resultText.text = $"Used {item.ItemName}.\nRestored {stats.CurrentHP - oldHP} HP: {oldHP} -> {stats.CurrentHP}/{stats.MaxHP}";
    }

    private void RefreshMenu()
    {
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, inventory.Count - 1));
        healthText.text = $"HP  {stats.CurrentHP}/{stats.MaxHP}";
        countText.text = $"ITEMS  {inventory.Count}/{PlayerInventory.Capacity}";
        var items = inventory.Items;
        for (int i = 0; i < rows.Length; i++)
        {
            bool exists = i < inventory.Count;
            rows[i].gameObject.SetActive(exists);
            if (!exists) continue;
            ItemData item = items[i];
            rows[i].text = (i == selectedIndex ? "> " : "  ") +
                (item != null ? item.ItemName : "Unavailable item");
            rows[i].color = i == selectedIndex ? selectionColor : new Color(1f, 0.96f, 0.85f);
        }
        if (inventory.Count == 0)
        {
            descriptionText.text = "Your inventory is empty.";
            return;
        }
        ItemData selected = items[selectedIndex];
        descriptionText.text = selected == null ? "Unavailable item." :
            string.IsNullOrWhiteSpace(selected.Description) ? selected.ItemName : selected.Description;
        if (selected != null && selected.Effect == ItemEffect.RestoreHPAndDodgeSpeed)
            descriptionText.text += "\n\nOutside battle: heals only. No speed bonus.";
    }

    private void BuildMenu()
    {
        canvasObject = new GameObject("Exploration Inventory Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        panel = Box("Inventory Panel", canvasObject.transform, Vector2.zero, new Vector2(1460, 900), new Color(1f, 0.96f, 0.85f));
        GameObject interior = Box("Interior", panel.transform, Vector2.zero, new Vector2(1448, 888), Color.black);
        Label("Title", interior.transform, new Vector2(0, 370), new Vector2(1300, 65), "INVENTORY", 38);
        countText = Label("Count", interior.transform, new Vector2(-300, 285), new Vector2(650, 50), "", 26);
        Box("Divider", interior.transform, new Vector2(60, -25), new Vector2(3, 590), new Color(1f, 0.96f, 0.85f));
        for (int i = 0; i < rows.Length; i++)
            rows[i] = Label("Item " + i, interior.transform, new Vector2(-300, 220 - i * 51), new Vector2(650, 48), "", 24);
        descriptionText = Label("Description", interior.transform, new Vector2(380, 0), new Vector2(540, 500), "", 27);
        healthText = Label("Health", interior.transform, new Vector2(380, 285), new Vector2(540, 50), "", 30);
        resultText = Label("Use Result", interior.transform, new Vector2(0, -355), new Vector2(1300, 120), "", 24);
        panel.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static GameObject Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = Rect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect.gameObject;
    }

    private TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, float fontSize)
    {
        RectTransform rect = Rect(name, parent, position, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (menuFont != null) label.font = menuFont;
        label.fontSize = fontSize;
        label.color = new Color(1f, 0.96f, 0.85f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.richText = false;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = value;
        return label;
    }
}
