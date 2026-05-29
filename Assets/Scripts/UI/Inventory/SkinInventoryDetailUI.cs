using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class SkinInventoryDetailUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Image rarityBoxImage;
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

    private void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }
    }

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Hide();
        }
    }

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
        if (rarityBoxImage != null) rarityBoxImage.color = GetRarityColor(entry.rarity);
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

    private static Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.ConsumerGrade: return new Color(0.50f, 0.50f, 0.50f);
            case ItemRarity.IndustrialGrade: return new Color(0.78f, 0.78f, 0.78f);
            case ItemRarity.MilSpec: return new Color(0.20f, 0.40f, 1.00f);
            case ItemRarity.Restricted: return new Color(0.55f, 0.25f, 0.90f);
            case ItemRarity.Classified: return new Color(0.95f, 0.35f, 0.75f);
            case ItemRarity.Covert: return new Color(0.90f, 0.20f, 0.20f);
            case ItemRarity.Contraband: return new Color(0.95f, 0.75f, 0.20f);
            case ItemRarity.Knife: return new Color(0.98f, 0.85f, 0.10f);
            default: return Color.white;
        }
    }
}
