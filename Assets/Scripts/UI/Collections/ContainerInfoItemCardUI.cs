using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives a single item card inside ContainerInfoPanelUI.
///
/// Normal skin cards:
///   Layout (sizeDelta, anchoredPosition, preserveAspect, ContentSizeFitter) is preserved
///   exactly as configured on the template in the scene.
///
/// Special / Rare card (SetupSpecial):
///   ContentSizeFitter is disabled so the knife icon cannot grow to its native PNG size.
///   A fixed size (specialImageWidth × specialImageHeight) is applied instead.
///   Normal cards are never affected.
///
/// Rarity display:
///   If raritySprites contains an entry for the item's rarity the sprite is shown with
///   Color.white (no tinting). Otherwise a flat colour tint is applied.
/// </summary>
public class ContainerInfoItemCardUI : MonoBehaviour
{
    // ── Rarity sprite mapping ──────────────────────────────────────────────────
    [System.Serializable]
    public class RaritySpriteEntry
    {
        public ItemRarity rarity;
        public Sprite     sprite;
    }

    [Header("Rarity Sprites (optional — assign in order: Gray, LightBlue, Blue, Purple, Pink, Red, Gold)")]
    [Tooltip("When a sprite is found for the item's rarity it is used as-is (color = white).\n" +
             "Leave empty to fall back to flat colour tints.")]
    [SerializeField] private RaritySpriteEntry[] raritySprites;

    // ── References ─────────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Bottom rarity band Image — receives the full rarity color / sprite.\n" +
             "Wire to the 'Bottom' child Image in the card.")]
    [SerializeField] private Image    rarityBackground;
    [Tooltip("Top image-area background — receives a darkened/desaturated rarity tint.\n" +
             "Wire to the 'Background' child Image in the card.\n" +
             "Leave empty to keep the original single-background look.")]
    [SerializeField] private Image    imageAreaBackground;
    [SerializeField] private Image    itemImage;
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private TMP_Text ownedText;
    [Tooltip("Optional Button on this card's root. When assigned and this is a normal skin card,\n" +
             "clicking opens SkinInfoPanelUI for the stored item.\n" +
             "Special gold cards clear the listener so they never open the price panel.")]
    [SerializeField] private Button   cardButton;

    [Header("Image Area Background Tint")]
    [Tooltip("Base dark color for the image area (used when no rarity tint is applied).")]
    [SerializeField] private Color imageBackgroundBaseColor = new Color(0.12f, 0.12f, 0.15f, 1f);
    [Tooltip("How much the rarity color bleeds into the image area. 0 = pure base color, 1 = full rarity color.")]
    [SerializeField, Range(0f, 1f)] private float imageBackgroundRarityTint = 0.30f;
    [Tooltip("Brightness multiplier applied after blending. Keeps the image area darker than the bottom band.")]
    [SerializeField, Range(0f, 1f)] private float imageBackgroundDarken     = 0.55f;

    // ── Labels / Colors ────────────────────────────────────────────────────────
    [Header("Owned Labels")]
    [SerializeField] private string ownedLabel    = "Owned";
    [SerializeField] private string notOwnedLabel = "Not owned";

    [Header("Owned Colors")]
    [SerializeField] private Color ownedColor    = new Color(1.00f, 0.85f, 0.00f); // gold/yellow
    [SerializeField] private Color notOwnedColor = new Color(1.00f, 1.00f, 1.00f); // white

    [Header("Flat Colour Tint (used when no sprite is mapped for this rarity)")]
    [Tooltip("Alpha applied only when falling back to colour tint (no sprite).")]
    [SerializeField, Range(0f, 1f)] private float rarityBackgroundAlpha = 0.85f;

    [Header("Special Card Image Size")]
    [Tooltip("Fixed width of the knife icon on the special gold card only.\n" +
             "ContentSizeFitter (if present) is disabled for the special card so this size is respected.\n" +
             "Normal skin cards keep their original template sizing.")]
    [SerializeField] private float specialImageWidth   = 80f;
    [SerializeField] private float specialImageHeight  = 80f;
    [Tooltip("Vertical shift (anchoredPosition.y) for the special card image. 0 = no shift.")]
    [SerializeField] private float specialImageYOffset = 0f;

    // ── Cached original image layout (captured once in Awake per clone) ────────
    private Vector2           _normalSizeDelta;
    private Vector2           _normalAnchoredPosition;
    private bool              _normalPreserveAspect;
    private ContentSizeFitter _itemImageCsf;
    private bool              _normalCsfEnabled;

    // ── Stored item data (used by card-click handler) ─────────────────────────
    private CaseItemData _item;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (itemImage == null) return;

        // Cache the template's original layout so Setup() can restore it perfectly.
        // Awake() fires on each clone when SetActive(true) is called, before Setup/SetupSpecial,
        // so these values always reflect the untouched template configuration.
        RectTransform rt        = itemImage.rectTransform;
        _normalSizeDelta        = rt.sizeDelta;
        _normalAnchoredPosition = rt.anchoredPosition;
        _normalPreserveAspect   = itemImage.preserveAspect;

        _itemImageCsf     = itemImage.GetComponent<ContentSizeFitter>();
        _normalCsfEnabled = _itemImageCsf != null && _itemImageCsf.enabled;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Populate this card with item data and ownership state.</summary>
    public void Setup(CaseItemData item, bool isOwned)
    {
        _item = item;

        ApplyRarity(item.GetEffectiveRarity());

        if (itemImage != null)
        {
            // Restore original layout before assigning the sprite so ContentSizeFitter
            // (if present) can do its normal job for weapon skin images.
            RestoreNormalImageLayout();

            itemImage.sprite = item.itemIcon;
            itemImage.gameObject.SetActive(item.itemIcon != null);
        }

        if (weaponNameText != null)
            weaponNameText.text = !string.IsNullOrEmpty(item.weaponName) ? item.weaponName
                                : !string.IsNullOrEmpty(item.itemName)   ? item.itemName
                                : string.Empty;

        if (skinNameText != null)
            skinNameText.text = !string.IsNullOrEmpty(item.skinName) ? item.skinName
                              : !string.IsNullOrEmpty(item.itemName)  ? item.itemName
                              : string.Empty;

        if (ownedText != null) ownedText.gameObject.SetActive(true);
        SetOwned(isOwned);

        // Wire click → SkinInfoPanelUI (normal cards only)
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    /// <summary>
    /// Populate this card as the "Special / Rare" slot (knives, gloves).
    /// Always uses gold rarity. Not counted for completion.
    /// ContentSizeFitter is disabled so the knife icon uses a fixed Inspector-controlled size.
    /// </summary>
    public void SetupSpecial(Sprite icon, string weaponLine, string skinLine)
    {
        ApplyRarity(ItemRarity.Knife); // gold

        if (itemImage != null)
        {
            // Disable ContentSizeFitter FIRST — if left on, it will override sizeDelta
            // to the sprite's native PNG dimensions (e.g. 1254×1254) after we set the sprite.
            if (_itemImageCsf != null)
                _itemImageCsf.enabled = false;

            itemImage.sprite         = icon;
            itemImage.enabled        = true;        // always show the slot
            itemImage.color          = Color.white;  // no tinting
            itemImage.preserveAspect = true;         // keep knife proportions

            // Apply the fixed special card size now that CSF cannot override it
            RectTransform rt    = itemImage.rectTransform;
            rt.sizeDelta        = new Vector2(specialImageWidth, specialImageHeight);
            rt.anchoredPosition = new Vector2(_normalAnchoredPosition.x, specialImageYOffset);
        }

        if (weaponNameText != null) weaponNameText.text = weaponLine;
        if (skinNameText   != null) skinNameText.text   = skinLine;

        // Hide owned text — this card is informational only
        if (ownedText != null) ownedText.gameObject.SetActive(false);

        // Special cards must never open the skin price panel
        if (cardButton != null)
            cardButton.onClick.RemoveAllListeners();
    }

    /// <summary>Update ownership display without rebuilding the card (fast-path).</summary>
    public void SetOwned(bool isOwned)
    {
        if (ownedText == null) return;
        ownedText.text  = isOwned ? ownedLabel : notOwnedLabel;
        ownedText.color = isOwned ? ownedColor : notOwnedColor;
    }

    // ── Card click ─────────────────────────────────────────────────────────────

    private void OnCardClicked()
    {
        if (_item == null) return;

        if (SkinInfoPanelUI.Instance != null)
            SkinInfoPanelUI.Instance.Show(_item);
        else
            Debug.LogWarning("[ContainerInfoItemCardUI] SkinInfoPanelUI is not in the scene — " +
                             "add it to open skin price details.");
    }

    // ── Layout helpers ─────────────────────────────────────────────────────────

    /// <summary>Restore the image layout to the values captured from the template in Awake.</summary>
    private void RestoreNormalImageLayout()
    {
        if (itemImage == null) return;

        RectTransform rt        = itemImage.rectTransform;
        rt.sizeDelta             = _normalSizeDelta;
        rt.anchoredPosition      = _normalAnchoredPosition;
        itemImage.preserveAspect = _normalPreserveAspect;

        if (_itemImageCsf != null)
            _itemImageCsf.enabled = _normalCsfEnabled;
    }

    // ── Rarity application ─────────────────────────────────────────────────────

    private void ApplyRarity(ItemRarity rarity)
    {
        Color rarityCol = RarityColor(rarity);

        // ── Bottom rarity band (full color / sprite) ──────────────────────────
        if (rarityBackground != null)
        {
            Sprite sprite = FindRaritySprite(rarity);
            if (sprite != null)
            {
                rarityBackground.sprite = sprite;
                rarityBackground.color  = Color.white;
            }
            else
            {
                rarityBackground.sprite = null;
                Color c = rarityCol;
                c.a = rarityBackgroundAlpha;
                rarityBackground.color = c;
            }
        }

        // ── Image area background (darkened rarity tint) ──────────────────────
        if (imageAreaBackground != null)
        {
            // Always color-based — no rarity sprite on the image area so it stays subtle
            imageAreaBackground.sprite = null;

            Color blended = Color.Lerp(imageBackgroundBaseColor, rarityCol, imageBackgroundRarityTint);
            blended = new Color(
                blended.r * imageBackgroundDarken,
                blended.g * imageBackgroundDarken,
                blended.b * imageBackgroundDarken,
                1f);
            imageAreaBackground.color = blended;
        }
    }

    private Sprite FindRaritySprite(ItemRarity rarity)
    {
        if (raritySprites == null) return null;
        foreach (var entry in raritySprites)
            if (entry.rarity == rarity) return entry.sprite;
        return null;
    }

    // ── Static helpers ─────────────────────────────────────────────────────────

    /// <summary>Flat colour fallback. Matches SkinInventoryCardUI.GetRarityColor() exactly.</summary>
    public static Color RarityColor(ItemRarity rarity)
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

    /// <summary>Sort key: higher = shown first (Knife/Gold first → ConsumerGrade last).</summary>
    public static int RaritySortKey(ItemRarity rarity) => (int)rarity;
}
