using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows per-wear and StatTrak prices for a single skin, opened by clicking a normal
/// skin card inside ContainerInfoPanelUI.
///
/// Wear availability:  If the skin's float range does not overlap a wear tier's range,
///                     that tier shows "-" regardless of cached price.
/// Missing price:      GetCachedValueForWear returns 0 when no price was imported → "-".
///
/// Open from code:  SkinInfoPanelUI.Instance.Show(item);
/// Close from code: SkinInfoPanelUI.Instance.Hide();
///
/// Hierarchy the script expects
/// (field names in parentheses — drag GameObjects/components into these Inspector slots):
///
///   Skin Info PanelUI          ← this MonoBehaviour lives here (always active)
///   └── Container info panel   ← panelRoot (starts inactive)
///       └── Case container UI
///           ├── TopUI / BoxUI / TextUI  (optional decorative)
///           └── Content
///               ├── OK button                   ← closeButton
///               ├── Skin + price + name
///               │   ├── Background rarity       ← rarityBackground  (Image)
///               │   └── Skin                    ← skinImage         (Image)
///               ├── Price + float
///               │   ├── Normal price box
///               │   │   ├── FN Price            ← fnNormalPriceText
///               │   │   ├── MW Price            ← mwNormalPriceText
///               │   │   ├── FT Price            ← ftNormalPriceText
///               │   │   ├── WW Price            ← wwNormalPriceText
///               │   │   └── BS Price            ← bsNormalPriceText
///               │   ├── Stattrak price box
///               │   │   ├── FN Price            ← fnStatTrakPriceText
///               │   │   ├── MW Price            ← mwStatTrakPriceText
///               │   │   ├── FT Price            ← ftStatTrakPriceText
///               │   │   ├── WW Price            ← wwStatTrakPriceText
///               │   │   └── BS Price            ← bsStatTrakPriceText
///               │   ├── Float Name              (static label, no binding needed)
///               │   └── Float Range             ← floatRangeText
///               └── Skin Name
///                   └── Skin name               ← skinNameText
/// </summary>
public class SkinInfoPanelUI : MonoBehaviour
{
    public static SkinInfoPanelUI Instance { get; private set; }

    // ── Rarity sprite mapping ─────────────────────────────────────────────────
    [System.Serializable]
    public class RaritySpriteEntry
    {
        public ItemRarity rarity;
        public Sprite     sprite;
    }

    [Header("Rarity Sprites (optional — same mapping as item cards)")]
    [Tooltip("If assigned for a rarity, uses the sprite with Color.white instead of a flat tint.")]
    [SerializeField] private RaritySpriteEntry[] raritySprites;

    // ── Panel / close ─────────────────────────────────────────────────────────
    [Header("Panel Root")]
    [Tooltip("The Container info panel child — hidden on start, shown by Show().")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("OK button that closes the panel.")]
    [SerializeField] private Button     closeButton;
    [Tooltip("Optional transparent backdrop button that also closes the panel.")]
    [SerializeField] private Button     backdropButton;

    // ── Header visuals ────────────────────────────────────────────────────────
    [Header("Skin Header")]
    [Tooltip("Image component on the 'Skin' child — displays item.itemIcon.")]
    [SerializeField] private Image    skinImage;
    [Tooltip("Image on 'Background rarity' — tinted/sprited with the item's rarity color.")]
    [SerializeField] private Image    rarityBackground;
    [Tooltip("TMP_Text on 'Skin name' — shows 'M4A1-S | Cyrex'.")]
    [SerializeField] private TMP_Text skinNameText;
    [Tooltip("TMP_Text on 'Float Range' — shows '0.00 - 0.50'.")]
    [SerializeField] private TMP_Text floatRangeText;

    // ── Normal wear prices ────────────────────────────────────────────────────
    [Header("Normal Wear Prices  (Factory New → Battle-Scarred)")]
    [SerializeField] private TMP_Text fnNormalPriceText;
    [SerializeField] private TMP_Text mwNormalPriceText;
    [SerializeField] private TMP_Text ftNormalPriceText;
    [SerializeField] private TMP_Text wwNormalPriceText;
    [SerializeField] private TMP_Text bsNormalPriceText;

    // ── StatTrak wear prices ──────────────────────────────────────────────────
    [Header("StatTrak Wear Prices")]
    [SerializeField] private TMP_Text fnStatTrakPriceText;
    [SerializeField] private TMP_Text mwStatTrakPriceText;
    [SerializeField] private TMP_Text ftStatTrakPriceText;
    [SerializeField] private TMP_Text wwStatTrakPriceText;
    [SerializeField] private TMP_Text bsStatTrakPriceText;

    // ── Rarity tint settings ──────────────────────────────────────────────────
    [Header("Rarity Background Tint")]
    [SerializeField, Range(0f, 1f)] private float rarityBackgroundAlpha = 0.85f;

    // ── Wear float ranges (CS standard) ──────────────────────────────────────
    // A wear is only shown if the skin's float range overlaps this tier's range.
    private static readonly (ItemWear wear, float min, float max)[] WearRanges =
    {
        (ItemWear.FactoryNew,    0.00f, 0.07f),
        (ItemWear.MinimalWear,   0.07f, 0.15f),
        (ItemWear.FieldTested,   0.15f, 0.38f),
        (ItemWear.WellWorn,      0.38f, 0.45f),
        (ItemWear.BattleScarred, 0.45f, 1.00f),
    };

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (closeButton    != null) closeButton.onClick.AddListener(Hide);
        if (backdropButton != null) backdropButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        if (closeButton    != null) closeButton.onClick.RemoveListener(Hide);
        if (backdropButton != null) backdropButton.onClick.RemoveListener(Hide);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Open the panel and fill all fields from <paramref name="item"/>.</summary>
    public void Show(CaseItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[SkinInfoPanelUI] Show() called with null CaseItemData.");
            return;
        }

        if (panelRoot != null) panelRoot.SetActive(true);

        // ── Skin name  ("M4A1-S | Cyrex") ────────────────────────────────────
        if (skinNameText != null)
        {
            string weapon = !string.IsNullOrEmpty(item.weaponName) ? item.weaponName
                          : !string.IsNullOrEmpty(item.itemName)   ? item.itemName
                          : string.Empty;
            string skin   = !string.IsNullOrEmpty(item.skinName)   ? item.skinName : string.Empty;
            skinNameText.text = string.IsNullOrEmpty(skin) ? weapon : $"{weapon} | {skin}";
        }

        // ── Skin image ────────────────────────────────────────────────────────
        if (skinImage != null)
        {
            skinImage.sprite  = item.itemIcon;
            skinImage.enabled = true;
            skinImage.color   = Color.white;
        }

        // ── Rarity background ─────────────────────────────────────────────────
        ApplyRarity(item.GetEffectiveRarity());

        // ── Float range  ("0.00 - 0.50") ─────────────────────────────────────
        if (floatRangeText != null)
        {
            float fMin = Mathf.Clamp01(Mathf.Min(item.floatMin, item.floatMax));
            float fMax = Mathf.Clamp01(Mathf.Max(item.floatMin, item.floatMax));
            // Treat 0-1 range as "unknown / full range"
            floatRangeText.text = $"{fMin:F2} - {fMax:F2}";
        }

        // ── Normal wear prices ────────────────────────────────────────────────
        FillPrice(fnNormalPriceText, item, ItemWear.FactoryNew,    isStatTrak: false);
        FillPrice(mwNormalPriceText, item, ItemWear.MinimalWear,   isStatTrak: false);
        FillPrice(ftNormalPriceText, item, ItemWear.FieldTested,   isStatTrak: false);
        FillPrice(wwNormalPriceText, item, ItemWear.WellWorn,      isStatTrak: false);
        FillPrice(bsNormalPriceText, item, ItemWear.BattleScarred, isStatTrak: false);

        // ── StatTrak wear prices ──────────────────────────────────────────────
        FillPrice(fnStatTrakPriceText, item, ItemWear.FactoryNew,    isStatTrak: true);
        FillPrice(mwStatTrakPriceText, item, ItemWear.MinimalWear,   isStatTrak: true);
        FillPrice(ftStatTrakPriceText, item, ItemWear.FieldTested,   isStatTrak: true);
        FillPrice(wwStatTrakPriceText, item, ItemWear.WellWorn,      isStatTrak: true);
        FillPrice(bsStatTrakPriceText, item, ItemWear.BattleScarred, isStatTrak: true);
    }

    /// <summary>Close the panel.</summary>
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Price filling ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fills one price label.
    /// Shows "-" if the wear tier is outside the skin's float range,
    /// or if no cached price exists for that tier.
    /// </summary>
    private static void FillPrice(TMP_Text label, CaseItemData item, ItemWear wear, bool isStatTrak)
    {
        if (label == null) return;

        // Wear not reachable for this skin → always "-"
        if (!WearAvailable(item, wear))
        {
            label.text = "-";
            return;
        }

        float price = item.GetCachedValueForWear(wear, isStatTrak);
        label.text = price > 0f ? $"${price:F2}" : "-";
    }

    /// <summary>
    /// Returns true if the skin's float range [floatMin, floatMax] overlaps the
    /// standard CS float range for <paramref name="wear"/>.
    /// </summary>
    private static bool WearAvailable(CaseItemData item, ItemWear wear)
    {
        float skinMin = Mathf.Clamp01(Mathf.Min(item.floatMin, item.floatMax));
        float skinMax = Mathf.Clamp01(Mathf.Max(item.floatMin, item.floatMax));

        foreach (var (w, wMin, wMax) in WearRanges)
        {
            if (w != wear) continue;
            // Two ranges [a,b] and [c,d] overlap when a < d && c < b
            return skinMin < wMax && skinMax > wMin;
        }
        return false;
    }

    // ── Rarity visuals ─────────────────────────────────────────────────────────

    private void ApplyRarity(ItemRarity rarity)
    {
        if (rarityBackground == null) return;

        Sprite sprite = FindRaritySprite(rarity);
        if (sprite != null)
        {
            rarityBackground.sprite = sprite;
            rarityBackground.color  = Color.white;
        }
        else
        {
            rarityBackground.sprite = null;
            Color c = ContainerInfoItemCardUI.RarityColor(rarity);
            c.a = rarityBackgroundAlpha;
            rarityBackground.color = c;
        }
    }

    private Sprite FindRaritySprite(ItemRarity rarity)
    {
        if (raritySprites == null) return null;
        foreach (var entry in raritySprites)
            if (entry.rarity == rarity) return entry.sprite;
        return null;
    }
}
