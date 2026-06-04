using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryFilterController : MonoBehaviour
{
    [Serializable]
    public class RarityToggle
    {
        public ToggleBoxFillUI toggle;
        public ItemRarity rarity;
    }

    [Serializable]
    public class WearToggle
    {
        public ToggleBoxFillUI toggle;
        public ItemWear wear;
    }

    public enum WeaponType
    {
        Pistols,
        Rifles,
        SMGs,
        Snipers,
        Knives,
        Gloves
    }

    [Serializable]
    public class WeaponTypeToggle
    {
        public ToggleBoxFillUI toggle;
        public WeaponType type;
    }

    public enum InventorySortMode
    {
        Newest,
        Oldest,
        PriceLowHigh,
        PriceHighLow,
        FloatLowHigh,
        FloatHighLow,
        RarityLowHigh,
        RarityHighLow,
        NameAZ,
        NameZA
    }

    [Header("Inventory")]
    [SerializeField] private SkinInventoryUI inventoryUI;
    [SerializeField] private Transform inventoryContentParent;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;

    [Header("Sort")]
    [SerializeField] private TMP_Dropdown sortDropdownTmp;
    [SerializeField] private Dropdown sortDropdown;
    [SerializeField] private List<InventorySortMode> sortOptionsByIndex = new List<InventorySortMode>();

    [Header("Rarity")]
    [SerializeField] private List<RarityToggle> rarityToggles = new List<RarityToggle>();

    [Header("Wear")]
    [SerializeField] private List<WearToggle> wearToggles = new List<WearToggle>();
    [SerializeField] private List<WearSelectionGroup> wearSelectionGroups = new List<WearSelectionGroup>();

    [Header("Special")]
    [SerializeField] private ToggleBoxFillUI statTrakOnlyToggle;

    [Header("Weapon Type")]
    [SerializeField] private List<WeaponTypeToggle> weaponTypeToggles = new List<WeaponTypeToggle>();

    [Header("Price Range")]
    [SerializeField] private TMP_InputField minPriceInputTmp;
    [SerializeField] private TMP_InputField maxPriceInputTmp;
    [SerializeField] private InputField minPriceInput;
    [SerializeField] private InputField maxPriceInput;
    [SerializeField] private float defaultMinPrice = 0f;
    [SerializeField] private float defaultMaxPrice = 9999.99f;

    [Header("Float Range")]
    [SerializeField] private TMP_InputField minFloatInputTmp;
    [SerializeField] private TMP_InputField maxFloatInputTmp;
    [SerializeField] private InputField minFloatInput;
    [SerializeField] private InputField maxFloatInput;
    [SerializeField] private float defaultMinFloat = 0f;
    [SerializeField] private float defaultMaxFloat = 1f;

    private void OnEnable()
    {
        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplyFilters);
            applyButton.onClick.AddListener(ApplyFilters);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetFilters);
            resetButton.onClick.AddListener(ResetFilters);
        }
    }

    private void OnDisable()
    {
        if (applyButton != null)
            applyButton.onClick.RemoveListener(ApplyFilters);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetFilters);
    }

    private void Start()
    {
        ApplyFilters();
    }

    public void ApplyFilters()
    {
        var cards = GetCards();
        if (cards == null || cards.Length == 0)
            return;

        var selectedRarities = GetSelectedRarities();
        var selectedWears = GetSelectedWears();
        var selectedWeaponTypes = GetSelectedWeaponTypes();

        bool filterRarity = selectedRarities.Count > 0;
        bool filterWear = selectedWears.Count > 0;
        bool filterWeaponType = selectedWeaponTypes.Count > 0;
        bool filterStatTrak = statTrakOnlyToggle != null && statTrakOnlyToggle.IsFilled;

        bool hasMinPrice = TryGetPriceValue(true, out float minPrice);
        bool hasMaxPrice = TryGetPriceValue(false, out float maxPrice);
        bool hasMinFloat = TryGetFloatValue(true, out float minFloat);
        bool hasMaxFloat = TryGetFloatValue(false, out float maxFloat);

        if (hasMinPrice && hasMaxPrice && minPrice > maxPrice)
            (minPrice, maxPrice) = (maxPrice, minPrice);

        if (hasMinFloat && hasMaxFloat && minFloat > maxFloat)
            (minFloat, maxFloat) = (maxFloat, minFloat);

        var orderIndex = new Dictionary<SkinInventoryCardUI, int>();
        for (int i = 0; i < cards.Length; i++)
            orderIndex[cards[i]] = cards[i].transform.GetSiblingIndex();

        var filteredCards = new List<SkinInventoryCardUI>(cards.Length);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            if (card == null)
                continue;

            var entry = card.CurrentEntry;
            if (entry == null)
                continue;

            bool show = true;

            if (filterRarity && !selectedRarities.Contains(entry.rarity))
                show = false;

            if (show && filterWear && !selectedWears.Contains(entry.wear))
                show = false;

            if (show && filterWeaponType && !MatchesWeaponType(entry, selectedWeaponTypes))
                show = false;

            if (show && filterStatTrak && !entry.isStatTrak)
                show = false;

            if (show && hasMinPrice && entry.marketValue < minPrice)
                show = false;

            if (show && hasMaxPrice && entry.marketValue > maxPrice)
                show = false;

            if (show && hasMinFloat && entry.floatValue < minFloat)
                show = false;

            if (show && hasMaxFloat && entry.floatValue > maxFloat)
                show = false;

            card.gameObject.SetActive(show);
            filteredCards.Add(card);
        }

        ApplySort(cards, orderIndex);
    }

    public void ResetFilters()
    {
        SetDropdownIndex(0);

        for (int i = 0; i < rarityToggles.Count; i++)
            SetToggleFilled(rarityToggles[i].toggle, false);

        for (int i = 0; i < wearToggles.Count; i++)
            SetToggleFilled(wearToggles[i].toggle, false);

        for (int i = 0; i < wearSelectionGroups.Count; i++)
        {
            if (wearSelectionGroups[i] != null)
                wearSelectionGroups[i].SetSelected(-1, false);
        }

        for (int i = 0; i < weaponTypeToggles.Count; i++)
            SetToggleFilled(weaponTypeToggles[i].toggle, false);

        SetToggleFilled(statTrakOnlyToggle, false);

        SetPriceInputs(defaultMinPrice, defaultMaxPrice);
        SetFloatInputs(defaultMinFloat, defaultMaxFloat);

        ApplyFilters();
    }

    private SkinInventoryCardUI[] GetCards()
    {
        if (inventoryUI != null)
        {
            var cards = inventoryUI.GetCards(true);
            if (cards.Length > 0)
                return cards;
        }

        if (inventoryContentParent == null)
            return Array.Empty<SkinInventoryCardUI>();

        return inventoryContentParent.GetComponentsInChildren<SkinInventoryCardUI>(true);
    }

    private List<ItemRarity> GetSelectedRarities()
    {
        var list = new List<ItemRarity>();
        for (int i = 0; i < rarityToggles.Count; i++)
        {
            var toggle = rarityToggles[i];
            if (toggle != null && toggle.toggle != null && toggle.toggle.IsFilled)
                list.Add(toggle.rarity);
        }
        return list;
    }

    private List<ItemWear> GetSelectedWears()
    {
        var list = new List<ItemWear>();
        for (int i = 0; i < wearToggles.Count; i++)
        {
            var toggle = wearToggles[i];
            if (toggle != null && toggle.toggle != null && toggle.toggle.IsFilled)
                list.Add(toggle.wear);
        }
        return list;
    }

    private List<WeaponType> GetSelectedWeaponTypes()
    {
        var list = new List<WeaponType>();
        for (int i = 0; i < weaponTypeToggles.Count; i++)
        {
            var toggle = weaponTypeToggles[i];
            if (toggle != null && toggle.toggle != null && toggle.toggle.IsFilled)
                list.Add(toggle.type);
        }
        return list;
    }

    private static bool MatchesWeaponType(SkinInventoryEntry entry, List<WeaponType> selectedTypes)
    {
        if (entry == null || selectedTypes == null || selectedTypes.Count == 0)
            return true;

        string weapon = string.IsNullOrEmpty(entry.weaponName) ? entry.itemName : entry.weaponName;
        if (string.IsNullOrEmpty(weapon))
            weapon = string.Concat(entry.weaponName, " ", entry.itemName);

        weapon = weapon.ToLowerInvariant();

        for (int i = 0; i < selectedTypes.Count; i++)
        {
            switch (selectedTypes[i])
            {
                case WeaponType.Pistols:
                    if (weapon.Contains("pistol")) return true;
                    break;
                case WeaponType.Rifles:
                    if (weapon.Contains("rifle")) return true;
                    break;
                case WeaponType.SMGs:
                    if (weapon.Contains("smg")) return true;
                    break;
                case WeaponType.Snipers:
                    if (weapon.Contains("sniper") || weapon.Contains("rifle") && weapon.Contains("sniper")) return true;
                    break;
                case WeaponType.Knives:
                    if (weapon.Contains("knife")) return true;
                    break;
                case WeaponType.Gloves:
                    if (weapon.Contains("glove")) return true;
                    break;
            }
        }

        return false;
    }

    private void ApplySort(SkinInventoryCardUI[] cards, Dictionary<SkinInventoryCardUI, int> orderIndex)
    {
        if (cards == null || cards.Length == 0)
            return;

        InventorySortMode mode = GetSortMode();
        Array.Sort(cards, (a, b) => CompareCards(a, b, mode, orderIndex));

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null)
                cards[i].transform.SetSiblingIndex(i);
        }
    }

    private static int CompareCards(SkinInventoryCardUI a, SkinInventoryCardUI b, InventorySortMode mode, Dictionary<SkinInventoryCardUI, int> orderIndex)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        var entryA = a.CurrentEntry;
        var entryB = b.CurrentEntry;
        if (entryA == null && entryB == null) return 0;
        if (entryA == null) return 1;
        if (entryB == null) return -1;

        switch (mode)
        {
            case InventorySortMode.Oldest:
                return orderIndex[a].CompareTo(orderIndex[b]);
            case InventorySortMode.Newest:
                return orderIndex[b].CompareTo(orderIndex[a]);
            case InventorySortMode.PriceLowHigh:
                return entryA.marketValue.CompareTo(entryB.marketValue);
            case InventorySortMode.PriceHighLow:
                return entryB.marketValue.CompareTo(entryA.marketValue);
            case InventorySortMode.FloatLowHigh:
                return entryA.floatValue.CompareTo(entryB.floatValue);
            case InventorySortMode.FloatHighLow:
                return entryB.floatValue.CompareTo(entryA.floatValue);
            case InventorySortMode.RarityLowHigh:
                return entryA.rarity.CompareTo(entryB.rarity);
            case InventorySortMode.RarityHighLow:
                return entryB.rarity.CompareTo(entryA.rarity);
            case InventorySortMode.NameAZ:
                return string.Compare(GetName(entryA), GetName(entryB), StringComparison.OrdinalIgnoreCase);
            case InventorySortMode.NameZA:
                return string.Compare(GetName(entryB), GetName(entryA), StringComparison.OrdinalIgnoreCase);
            default:
                return 0;
        }
    }

    private static string GetName(SkinInventoryEntry entry)
    {
        if (entry == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(entry.itemName))
            return entry.itemName;

        return string.Concat(entry.weaponName, " ", entry.skinName).Trim();
    }

    private InventorySortMode GetSortMode()
    {
        int index = GetDropdownIndex();
        if (index >= 0 && index < sortOptionsByIndex.Count)
            return sortOptionsByIndex[index];

        return InventorySortMode.Newest;
    }

    private int GetDropdownIndex()
    {
        if (sortDropdownTmp != null)
            return sortDropdownTmp.value;
        if (sortDropdown != null)
            return sortDropdown.value;
        return 0;
    }

    private void SetDropdownIndex(int index)
    {
        if (sortDropdownTmp != null)
            sortDropdownTmp.SetValueWithoutNotify(index);
        if (sortDropdown != null)
            sortDropdown.SetValueWithoutNotify(index);
    }

    private bool TryGetPriceValue(bool isMin, out float value)
    {
        string text = GetInputText(isMin ? minPriceInputTmp : maxPriceInputTmp, isMin ? minPriceInput : maxPriceInput);
        return TryParseInput(text, out value);
    }

    private bool TryGetFloatValue(bool isMin, out float value)
    {
        string text = GetInputText(isMin ? minFloatInputTmp : maxFloatInputTmp, isMin ? minFloatInput : maxFloatInput);
        return TryParseInput(text, out value);
    }

    private static string GetInputText(TMP_InputField tmpInput, InputField input)
    {
        if (tmpInput != null)
            return tmpInput.text;
        if (input != null)
            return input.text;
        return string.Empty;
    }

    private static bool TryParseInput(string text, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        return float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)
               || float.TryParse(text, out value);
    }

    private void SetPriceInputs(float min, float max)
    {
        SetInputText(minPriceInputTmp, minPriceInput, min.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        SetInputText(maxPriceInputTmp, maxPriceInput, max.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void SetFloatInputs(float min, float max)
    {
        SetInputText(minFloatInputTmp, minFloatInput, min.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        SetInputText(maxFloatInputTmp, maxFloatInput, max.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void SetInputText(TMP_InputField tmpInput, InputField input, string value)
    {
        if (tmpInput != null)
            tmpInput.SetTextWithoutNotify(value);
        if (input != null)
            input.SetTextWithoutNotify(value);
    }

    private static void SetToggleFilled(ToggleBoxFillUI toggle, bool filled)
    {
        if (toggle != null)
            toggle.SetFilled(filled);
    }
}
