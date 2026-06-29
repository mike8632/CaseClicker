using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single skin/item row inside ContentsPanelUI.
/// Shows: rarity-colored bar, item icon, item name.
/// Name text is yellow when the player owns that skin, grey when not owned.
///
/// Prefab layout suggestion:
///   Root (Image background)
///   ├── RarityBar  (Image – thin strip, ~6 px, left edge)  ← rarityBar
///   ├── SkinIcon   (Image – ~48×48)                        ← skinIcon
///   └── SkinName   (TMP_Text)                              ← skinName
/// </summary>
public class ContentsSkinRowUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Thin colored strip on the left; color is set from the item's rarity.")]
    [SerializeField] private Image    rarityBar;
    [SerializeField] private Image    skinIcon;
    [SerializeField] private TMP_Text skinName;

    [Header("Owned Colors")]
    [SerializeField] private Color ownedColor   = new Color(1.00f, 0.85f, 0.00f); // yellow
    [SerializeField] private Color unownedColor = new Color(0.55f, 0.55f, 0.55f); // grey

    // ── Public API (called by ContentsPanelUI) ─────────────────────────────────

    public void Setup(CaseItemData item, bool isOwned)
    {
        if (rarityBar != null)
            rarityBar.color = RarityColor(item.GetEffectiveRarity());

        if (skinIcon != null)
        {
            skinIcon.sprite = item.itemIcon;
            skinIcon.gameObject.SetActive(item.itemIcon != null);
        }

        if (skinName != null)
            skinName.text = DisplayName(item);

        SetOwned(isOwned);
    }

    /// <summary>Update owned highlight without rebuilding the row (fast-path on inventory change).</summary>
    public void SetOwned(bool isOwned)
    {
        if (skinName != null)
            skinName.color = isOwned ? ownedColor : unownedColor;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string DisplayName(CaseItemData item)
    {
        if (!string.IsNullOrEmpty(item.itemName))
            return item.itemName;

        if (!string.IsNullOrEmpty(item.weaponName) && !string.IsNullOrEmpty(item.skinName))
            return $"{item.weaponName} | {item.skinName}";

        return item.skinName ?? item.weaponName ?? "Unknown";
    }

    // Matches SkinInventoryCardUI.GetRarityColor() values exactly.
    private static Color RarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.ConsumerGrade:   return new Color(0.50f, 0.50f, 0.50f);
            case ItemRarity.IndustrialGrade: return new Color(0.78f, 0.78f, 0.78f);
            case ItemRarity.MilSpec:         return new Color(0.20f, 0.40f, 1.00f);
            case ItemRarity.Restricted:      return new Color(0.55f, 0.25f, 0.90f);
            case ItemRarity.Classified:      return new Color(0.95f, 0.35f, 0.75f);
            case ItemRarity.Covert:          return new Color(0.90f, 0.20f, 0.20f);
            case ItemRarity.Contraband:      return new Color(0.95f, 0.75f, 0.20f);
            case ItemRarity.Knife:           return new Color(0.98f, 0.85f, 0.10f);
            default:                         return Color.white;
        }
    }
}
