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
    [SerializeField] private string statTrakFormat = "StatTrakù";
    [SerializeField] private string statTrakNameSuffix = " (StatTrak)";

    [Header("Rarity Display")]
    [SerializeField] private bool showRarityText = false;
    [SerializeField] private bool hideKnifeImage = false;

    [Header("Sell")]
    [SerializeField] private Button sellButton;

    [Header("Selection")]
    [SerializeField] private Button clickButton;
    [SerializeField] private GameObject selectionBorders;
    public SkinInventoryEntryEvent OnSelected;
    public event Action<SkinInventoryCardUI, SkinInventoryEntry, bool> SelectionChanged;

    private SkinInventoryEntry currentEntry;

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
    }

    private void HandleClick()
    {
        if (currentEntry == null) return;
        if (selectionBorders != null)
            selectionBorders.SetActive(!selectionBorders.activeSelf);
        SelectionChanged?.Invoke(this, currentEntry, IsSelected);
        Debug.Log($"[SkinInventoryCardUI] Selected '{currentEntry.itemName}' ({currentEntry.itemId})");
        OnSelected?.Invoke(currentEntry);
    }

    private void HandleSell()
    {
        if (currentEntry == null || SkinInventoryManager.Instance == null) return;
        if (SkinInventoryManager.Instance.SellSkin(currentEntry))
            Destroy(gameObject);
    }

    public bool IsSelected => selectionBorders != null && selectionBorders.activeSelf;

    public SkinInventoryEntry CurrentEntry => currentEntry;

    public void SetSelected(bool selected)
    {
        if (selectionBorders != null)
            selectionBorders.SetActive(selected);
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
