using UnityEngine;
using UnityEngine.UI;

public class SkinInventoryCardUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Image icon;
    [SerializeField] private Text itemNameText;
    [SerializeField] private Text rarityText;
    [SerializeField] private Text wearText;
    [SerializeField] private Text valueText;

    [Header("Formats")]
    [SerializeField] private string valueFormat = "$ {0:F2}";

    public void Bind(SkinInventoryEntry entry)
    {
        if (entry == null) return;

        if (icon != null) icon.sprite = entry.itemIcon;
        if (itemNameText != null) itemNameText.text = entry.itemName;
        if (rarityText != null) rarityText.text = entry.rarity.ToString();
        if (wearText != null) wearText.text = entry.wear.ToString();
        if (valueText != null) valueText.text = string.Format(valueFormat, entry.marketValue);
    }
}
