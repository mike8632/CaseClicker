using UnityEngine;
using UnityEngine.UI;

public class SkinInventoryDetailUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text weaponNameText;
    [SerializeField] private Text skinNameText;
    [SerializeField] private Text itemNameText;
    [SerializeField] private Text rarityText;
    [SerializeField] private Text wearText;
    [SerializeField] private Text floatText;
    [SerializeField] private Text valueText;
    [SerializeField] private GameObject statTrakBadge;

    [Header("Formats")]
    [SerializeField] private string floatFormat = "{0:0.000000}";
    [SerializeField] private string valueFormat = "${0:F2}";

    public void Show(SkinInventoryEntry entry)
    {
        if (entry == null)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (itemIcon != null) itemIcon.sprite = entry.itemIcon;
        if (weaponNameText != null) weaponNameText.text = entry.weaponName;
        if (skinNameText != null) skinNameText.text = entry.skinName;
        if (itemNameText != null) itemNameText.text = entry.itemName;
        if (rarityText != null) rarityText.text = entry.rarity.ToString();
        if (wearText != null) wearText.text = entry.wear.ToString();
        if (floatText != null) floatText.text = string.Format(floatFormat, entry.floatValue);
        if (valueText != null) valueText.text = string.Format(valueFormat, entry.marketValue);
        if (statTrakBadge != null) statTrakBadge.SetActive(entry.isStatTrak);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
