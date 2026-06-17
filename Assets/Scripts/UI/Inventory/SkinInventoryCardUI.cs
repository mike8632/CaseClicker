using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.Events;

public class SkinInventoryCardUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Image skinImage;
    [SerializeField] private Text weaponNameText;
    [SerializeField] private Text skinNameText;
    [SerializeField] private Text rarityText;
    [SerializeField] private Text conditionText;
    [SerializeField] private Text floatValueText;
    [SerializeField] private Text sellPriceText;
    [SerializeField] private GameObject statTrakBadge;
    [SerializeField] private Text statTrakText;
    [SerializeField] private Image knifeBadge;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image rarityBackgroundSecondary;

    [Header("Formats")]
    [SerializeField] private string sellPriceFormat = "SELL FOR ${0:F2}";
    [SerializeField] private string floatValueFormat = "{0:0.000000}";
    [SerializeField] private string statTrakFormat = "StatTrak?";
    [SerializeField] private string statTrakNameSuffix = " (StatTrak)";

    [Header("Rarity Display")]
    [SerializeField] private bool showRarityText = false;
    [SerializeField] private bool hideKnifeImage = false;

    [Header("Sell")]
    [SerializeField] private Button sellButton;

    [Header("Lock")]
    [SerializeField] private GameObject lockedIcon;
    [SerializeField] private Image      skinBackgroundImage;
    [SerializeField] private Color      normalBackgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color      lockedBackgroundColor = new Color(0.40f, 0.40f, 0.40f, 1f);
    [SerializeField] private string     lockedSellText        = "LOCKED";

    [Header("Selection")]
    [SerializeField] private Button clickButton;
    [SerializeField] private GameObject selectionBorders;
    public SkinInventoryEntryEvent OnSelected;
    public event Action<SkinInventoryCardUI, SkinInventoryEntry, bool> SelectionChanged;

    public enum CardClickMode
    {
        Normal,        // click opens detail panel only (main inventory)
        BulkSelect,    // click toggles multi-select for bulk actions
        TradeUpSelect  // click toggles selection for trade-up contract
    }

    private SkinInventoryEntry currentEntry;
    private CardClickMode _clickMode = CardClickMode.Normal;

    private void Awake()
    {
        if (clickButton == null)
            clickButton = GetComponent<Button>();

        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(HandleClick);
            clickButton.onClick.AddListener(HandleClick);
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveListener(HandleSell);
            sellButton.onClick.AddListener(HandleSell);
        }
    }

    private void OnEnable()
    {
        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinLockChanged.AddListener(HandleSkinLockChanged);
            SkinInventoryManager.Instance.OnSkinValueChanged.AddListener(HandleSkinValueChanged);
        }
    }

    private void OnDisable()
    {
        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinLockChanged.RemoveListener(HandleSkinLockChanged);
            SkinInventoryManager.Instance.OnSkinValueChanged.RemoveListener(HandleSkinValueChanged);
        }
    }

    public void Bind(SkinInventoryEntry entry)
    {
        if (entry == null) return;

        currentEntry = entry;

        if (selectionBorders != null)
            selectionBorders.SetActive(false);

        if (skinImage != null)
        {
            bool showImage = !hideKnifeImage || entry.rarity != ItemRarity.Knife;
            skinImage.gameObject.SetActive(showImage);
            if (showImage)
                skinImage.sprite = entry.itemIcon;
        }
        if (weaponNameText != null)
        {
            string baseWeaponName = !string.IsNullOrEmpty(entry.weaponName) ? entry.weaponName : entry.itemName;
            weaponNameText.text = entry.isStatTrak ? baseWeaponName + statTrakNameSuffix : baseWeaponName;
        }
        if (skinNameText != null)
        {
            skinNameText.text = entry.skinName;
        }
        if (rarityText != null)
        {
            rarityText.gameObject.SetActive(showRarityText);
            if (showRarityText)
                rarityText.text = entry.rarity.ToString();
        }
        if (conditionText != null) conditionText.text = ToConditionShort(entry.wear);
        if (floatValueText != null)
        {
            string valueFormat = string.IsNullOrWhiteSpace(floatValueFormat) ? "{0:0.000000}" : floatValueFormat;
            floatValueText.gameObject.SetActive(true);
            floatValueText.text = string.Format(valueFormat, entry.floatValue);
        }
        if (sellPriceText != null) sellPriceText.text = string.Format(sellPriceFormat, entry.marketValue);

        if (statTrakBadge != null) statTrakBadge.SetActive(entry.isStatTrak);
        if (statTrakText != null)
        {
            statTrakText.gameObject.SetActive(entry.isStatTrak);
            if (entry.isStatTrak)
                statTrakText.text = statTrakFormat;
        }

        if (knifeBadge != null)
            knifeBadge.gameObject.SetActive(entry.rarity == ItemRarity.Knife);

        if (rarityBackground != null)
        {
            rarityBackground.color = GetRarityColor(entry.rarity);
        }
        if (rarityBackgroundSecondary != null)
        {
            rarityBackgroundSecondary.color = GetRarityColor(entry.rarity);
        }

        RefreshLockVisuals();
    }

    private void HandleClick()
    {
        if (currentEntry == null) return;

        switch (_clickMode)
        {
            case CardClickMode.Normal:
                // Open detail panel only ? no selection border toggling
                OnSelected?.Invoke(currentEntry);
                break;

            case CardClickMode.BulkSelect:
                // Toggle multi-select (locked skins may be selected for bulk unlock)
                SetSelected(!IsSelected);
                SelectionChanged?.Invoke(this, currentEntry, IsSelected);
                break;

            case CardClickMode.TradeUpSelect:
                // Toggle selection for trade-up contract
                SetSelected(!IsSelected);
                SelectionChanged?.Invoke(this, currentEntry, IsSelected);
                break;
        }
    }

    private void HandleSell()
    {
        if (currentEntry == null || SkinInventoryManager.Instance == null) return;
        if (SkinInventoryManager.Instance.SellSkin(currentEntry))
            Destroy(gameObject);
    }

    private void RefreshLockVisuals()
    {
        if (currentEntry == null) return;
        bool locked = currentEntry.isLocked;

        if (lockedIcon != null)
            lockedIcon.SetActive(locked);

        if (sellButton != null)
            sellButton.interactable = !locked;

        if (sellPriceText != null)
            sellPriceText.text = locked
                ? lockedSellText
                : string.Format(sellPriceFormat, currentEntry.marketValue);

        if (skinBackgroundImage != null)
            skinBackgroundImage.color = locked ? lockedBackgroundColor : normalBackgroundColor;
    }

    private void HandleSkinLockChanged(SkinInventoryEntry entry)
    {
        if (currentEntry == null || entry == null) return;
        if (entry.instanceId != currentEntry.instanceId) return;
        RefreshLockVisuals();
    }

    private void HandleSkinValueChanged(SkinInventoryEntry entry)
    {
        if (currentEntry == null || entry == null) return;
        if (entry.instanceId != currentEntry.instanceId) return;
        // Refresh only the sell price text; leave everything else as-is.
        if (sellPriceText != null && !currentEntry.isLocked)
            sellPriceText.text = string.Format(sellPriceFormat, currentEntry.marketValue);
    }

    public bool IsSelected => selectionBorders != null && selectionBorders.activeSelf;

    public SkinInventoryEntry CurrentEntry => currentEntry;

    public void SetSelected(bool selected)
    {
        if (selectionBorders != null)
            selectionBorders.SetActive(selected);
    }

    /// <summary>
    /// Switch the card's click behavior. Always clears the selection visual when the mode changes.
    /// </summary>
    public void SetClickMode(CardClickMode mode)
    {
        if (_clickMode == mode) return;
        _clickMode = mode;
        SetSelected(false); // clear selection visual on mode change
    }

    [System.Serializable]
    public class SkinInventoryEntryEvent : UnityEvent<SkinInventoryEntry>
    {
    }

    private static string ToConditionShort(ItemWear wear)
    {
        switch (wear)
        {
            case ItemWear.FactoryNew: return "FN";
            case ItemWear.MinimalWear: return "MW";
            case ItemWear.FieldTested: return "FT";
            case ItemWear.WellWorn: return "WW";
            case ItemWear.BattleScarred: return "BS";
            default: return wear.ToString();
        }
    }

    private static Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.ConsumerGrade: return new Color(0.50f, 0.50f, 0.50f); // grey
            case ItemRarity.IndustrialGrade: return new Color(0.78f, 0.78f, 0.78f); // lightgrey
            case ItemRarity.MilSpec: return new Color(0.20f, 0.40f, 1.00f); // blue
            case ItemRarity.Restricted: return new Color(0.55f, 0.25f, 0.90f); // purple
            case ItemRarity.Classified: return new Color(0.95f, 0.35f, 0.75f); // pink
            case ItemRarity.Covert: return new Color(0.90f, 0.20f, 0.20f); // red
            case ItemRarity.Contraband: return new Color(0.95f, 0.75f, 0.20f); // gold
            case ItemRarity.Knife: return new Color(0.98f, 0.85f, 0.10f); // yellow
            default: return Color.white;
        }
    }
}
