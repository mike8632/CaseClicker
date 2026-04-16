using UnityEngine;
using UnityEngine;
using UnityEngine.UI;

public class SkinInventoryCardUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Image skinImage;
    [SerializeField] private Text weaponNameText;
    [SerializeField] private Text skinNameText;
    [SerializeField] private Text rarityText;
    [SerializeField] private Text conditionText;
    [SerializeField] private Text sellPriceText;
    [SerializeField] private GameObject statTrakBadge;
    [SerializeField] private Text statTrakText;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image rarityBackgroundSecondary;

    [Header("Formats")]
    [SerializeField] private string sellPriceFormat = "SELL FOR ${0:F2}";
    [SerializeField] private string statTrakFormat = "StatTrak™";
    [SerializeField] private string statTrakNameSuffix = " (StatTrak)";

    [Header("Rarity Display")]
    [SerializeField] private bool showRarityText = false;

    public void Bind(SkinInventoryEntry entry)
    {
        if (entry == null) return;

        if (skinImage != null) skinImage.sprite = entry.itemIcon;
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
        if (sellPriceText != null) sellPriceText.text = string.Format(sellPriceFormat, entry.marketValue);

        if (statTrakBadge != null) statTrakBadge.SetActive(entry.isStatTrak);
        if (statTrakText != null)
        {
            statTrakText.gameObject.SetActive(entry.isStatTrak);
            if (entry.isStatTrak)
                statTrakText.text = statTrakFormat;
        }

        if (rarityBackground != null)
        {
            rarityBackground.color = GetRarityColor(entry.rarity);
        }
        if (rarityBackgroundSecondary != null)
        {
            rarityBackgroundSecondary.color = GetRarityColor(entry.rarity);
        }
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
            default: return Color.white;
        }
    }
}
