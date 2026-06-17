using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class SkinInventoryEntry
{
    public string instanceId;
    public string sourceCaseId;
    public string itemId;
    public string itemName;
    public string weaponName;
    public string skinName;
    public Sprite itemIcon;
    public ItemRarity rarity;
    public ItemWear wear;
    public bool isStatTrak;
    public bool isLocked;
    public float marketValue;
    /// <summary>Price at the time the skin was opened. Never updated by repricing, so historical value is preserved.</summary>
    public float valueAtOpen;
    public float floatValue;
    public WeaponCategory weaponCategory;
    public string collectionId;
    public string collectionName;
    public Sprite collectionIcon;   // runtime only — not saved to JSON
}

/// <summary>
/// Stores skins/items obtained from opening cases.
/// Each drop is stored as its own entry so UI can create a new card per drop.
/// </summary>
public class SkinInventoryManager : MonoBehaviour
{
    public static SkinInventoryManager Instance { get; private set; }

    [Header("Drop Settings")]
    [SerializeField, Range(0f, 1f)] private float statTrakChance = 0.1f;
    [SerializeField, Min(1f)] private float statTrakValueMultiplier = 1.5f;

    public float StatTrakValueMultiplier => statTrakValueMultiplier;

    public UnityEvent<SkinInventoryEntry> OnSkinAdded;
    public UnityEvent<SkinInventoryEntry> OnSkinRemoved;
    public UnityEvent<SkinInventoryEntry> OnSkinLockChanged;
    /// <summary>Fired for each entry whose marketValue was updated by RepriceInventoryFromCachedPrices.</summary>
    public UnityEvent<SkinInventoryEntry> OnSkinValueChanged;

    private readonly List<SkinInventoryEntry> entries = new List<SkinInventoryEntry>();

    public IReadOnlyList<SkinInventoryEntry> Entries => entries;

    public List<SkinEntryDTO> GetSnapshot()
    {
        var snapshot = new List<SkinEntryDTO>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;
            snapshot.Add(new SkinEntryDTO
            {
                instanceId = entry.instanceId,
                sourceCaseId = entry.sourceCaseId,
                itemId = entry.itemId,
                itemName = entry.itemName,
                weaponName = entry.weaponName,
                skinName = entry.skinName,
                rarity = entry.rarity,
                wear = entry.wear,
                isStatTrak = entry.isStatTrak,
                isLocked = entry.isLocked,
                marketValue = entry.marketValue,
                valueAtOpen = entry.valueAtOpen,
                floatValue = entry.floatValue,
                weaponCategory  = entry.weaponCategory,
                collectionId   = entry.collectionId,
                collectionName = entry.collectionName
            });
        }

        return snapshot;
    }

    public void ApplySnapshot(List<SkinEntryDTO> snapshot)
    {
        entries.Clear();

        if (snapshot == null)
            return;

        for (int i = 0; i < snapshot.Count; i++)
        {
            var dto = snapshot[i];
            if (dto == null) continue;

            // Migrate old saves: if category was not stored, infer it from weapon name.
            WeaponCategory resolvedCategory = dto.weaponCategory != WeaponCategory.Unknown
                ? dto.weaponCategory
                : CaseItemData.InferWeaponCategory(dto.weaponName, dto.itemName);

            // Resolve collection data and icon from the matching item definition.
            // This handles old saves (collectionId/Name missing) and all saves (icon can't be in JSON).
            string resolvedCollectionId   = dto.collectionId   ?? string.Empty;
            string resolvedCollectionName = dto.collectionName ?? string.Empty;
            Sprite resolvedCollectionIcon = null;

            var matchedItem = FindMatchingCaseItem(dto.sourceCaseId, dto.itemId, dto.itemName);
            if (matchedItem != null)
            {
                // Respect item-level override vs. parent case collection.
                var matchedCase = FindCaseData(dto.sourceCaseId);
                matchedItem.GetEffectiveCollection(matchedCase,
                    out string itemCollId, out string itemCollName, out Sprite itemCollIcon);

                if (string.IsNullOrEmpty(resolvedCollectionId))   resolvedCollectionId   = itemCollId;
                if (string.IsNullOrEmpty(resolvedCollectionName)) resolvedCollectionName = itemCollName;
                resolvedCollectionIcon = itemCollIcon;
            }

            var entry = new SkinInventoryEntry
            {
                instanceId     = string.IsNullOrEmpty(dto.instanceId) ? Guid.NewGuid().ToString("N") : dto.instanceId,
                sourceCaseId   = dto.sourceCaseId,
                itemId         = dto.itemId,
                itemName       = dto.itemName,
                weaponName     = dto.weaponName,
                skinName       = dto.skinName,
                rarity         = dto.rarity,
                wear           = dto.wear,
                isStatTrak     = dto.isStatTrak,
                isLocked       = dto.isLocked,
                marketValue    = dto.marketValue,
                // Migrate old saves: valueAtOpen not present → default it to the saved marketValue.
                valueAtOpen    = dto.valueAtOpen > 0f ? dto.valueAtOpen : dto.marketValue,
                floatValue     = dto.floatValue,
                weaponCategory  = resolvedCategory,
                itemIcon        = matchedItem?.itemIcon ?? ResolveItemIcon(dto.sourceCaseId, dto.itemId, dto.itemName),
                collectionId    = resolvedCollectionId,
                collectionName  = resolvedCollectionName,
                collectionIcon  = resolvedCollectionIcon
            };

            entries.Add(entry);
            OnSkinAdded?.Invoke(entry);
        }
    }
    public int RemoveEntries(IEnumerable<SkinInventoryEntry> entriesToRemove)
    {
        if (entriesToRemove == null || entries.Count == 0)
            return 0;

        var ids = new HashSet<string>();
        foreach (var entry in entriesToRemove)
        {
            if (entry == null || string.IsNullOrEmpty(entry.instanceId))
                continue;
            ids.Add(entry.instanceId);
        }

        if (ids.Count == 0)
            return 0;

        return entries.RemoveAll(entry => entry != null && ids.Contains(entry.instanceId));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        OnSkinAdded ??= new UnityEvent<SkinInventoryEntry>();
        OnSkinRemoved ??= new UnityEvent<SkinInventoryEntry>();
        OnSkinLockChanged ??= new UnityEvent<SkinInventoryEntry>();
        OnSkinValueChanged ??= new UnityEvent<SkinInventoryEntry>();
    }

    /// <summary>
    /// Removes a skin from inventory and credits its market value to BalanceManager.
    /// Returns true only if the skin was found and successfully removed.
    /// </summary>
    public bool SellSkin(SkinInventoryEntry entry)
    {
        if (entry == null) return false;
        if (entry.isLocked) return false;

        int removed = RemoveEntries(new[] { entry });
        if (removed == 0) return false;

        BalanceManager.Instance?.AddMoney(entry.marketValue);
        OnSkinRemoved?.Invoke(entry);
        SaveSystem.Instance?.RequestSave();
        return true;
    }

    /// <summary>
    /// Toggle the locked/favorite state of a skin. Locked skins cannot be sold or used in trade-up.
    /// </summary>
    public void SetLocked(SkinInventoryEntry entry, bool locked)
    {
        if (entry == null) return;
        entry.isLocked = locked;
        OnSkinLockChanged?.Invoke(entry);
        SaveSystem.Instance?.RequestSave();
    }

    public void AddSkin(CaseItemData item, string sourceCaseId, float marketValue, float floatValue)
    {
        if (item == null) return;

        bool isStatTrak = UnityEngine.Random.value <= statTrakChance;
        ItemWear rolledWear = GetWearFromFloat(floatValue);

        if (isStatTrak)
        {
            // Prefer cached StatTrak price for the rolled wear tier.
            float stCached = item.GetCachedValueForWear(rolledWear, isStatTrak: true);
            if (stCached > 0f)
                marketValue = stCached;
            else
                marketValue *= Mathf.Max(1f, statTrakValueMultiplier);
        }

        item.GetDisplayNames(out var weaponName, out var skinName, out _);

        // Use item-level override if set, otherwise inherit from parent case.
        var parentCase = FindCaseData(sourceCaseId);
        item.GetEffectiveCollection(parentCase, out string collId, out string collName, out Sprite collIcon);

        var entry = new SkinInventoryEntry
        {
            instanceId = Guid.NewGuid().ToString("N"),
            sourceCaseId = sourceCaseId,
            itemId = item.itemId,
            itemName = item.itemName,
            weaponName = weaponName,
            skinName = skinName,
            itemIcon = item.itemIcon,
            rarity = item.GetEffectiveRarity(),
            wear = rolledWear,
            isStatTrak = isStatTrak,
            marketValue = marketValue,
            valueAtOpen = marketValue,
            floatValue = floatValue,
            weaponCategory  = item.GetWeaponCategory(),
            collectionId    = collId,
            collectionName  = collName,
            collectionIcon  = collIcon
        };

        entries.Add(entry);
        OnSkinAdded?.Invoke(entry);
    }

    /// <summary>
    /// Debug/test entry point. Reprices all inventory skins and requests a save if any changed.
    /// </summary>
    [ContextMenu("Reprice Inventory From Cached Prices")]
    public void RepriceInventoryFromCachedPrices() => DoReprice(requestSave: true);

    /// <summary>
    /// Called automatically after loading inventory. Same reprice logic but does not
    /// request an immediate save — the next normal save will persist the updated values.
    /// </summary>
    internal void AutoRepriceAfterLoad() => DoReprice(requestSave: false);

    /// <summary>
    /// Updates marketValue for every inventory skin that has a matching cached price.
    /// Uses the skin's saved wear and StatTrak state for the lookup.
    /// Only cached prices update the value — minValue/maxValue fallback is NOT used here.
    /// valueAtOpen is never changed: it always reflects the price at roll time.
    /// </summary>
    private void DoReprice(bool requestSave)
    {
        int updated = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;

            // Find the item definition using the safest available identifiers.
            CaseItemData item = FindMatchingCaseItem(entry.sourceCaseId, entry.itemId, entry.itemName);

            // Fallback: match by weaponName + skinName if itemId/itemName lookup failed.
            if (item == null &&
                (!string.IsNullOrEmpty(entry.weaponName) || !string.IsNullOrEmpty(entry.skinName)))
            {
                item = FindMatchingCaseItemByNames(entry.sourceCaseId, entry.weaponName, entry.skinName);
            }

            if (item == null || item.cachedPrices == null || !item.cachedPrices.HasAnyPrice())
                continue;

            float cachedPrice = item.GetCachedValueForWear(entry.wear, entry.isStatTrak);
            if (cachedPrice <= 0f)
                continue;

            entry.marketValue = cachedPrice;
            OnSkinValueChanged?.Invoke(entry);
            updated++;
        }

        if (updated > 0)
        {
            Debug.Log($"[SkinInventoryManager] Auto repriced inventory from cached prices: {updated} / {entries.Count} skin(s).");
            if (requestSave)
                SaveSystem.Instance?.RequestSave();
        }
        else
        {
            Debug.Log("[SkinInventoryManager] RepriceInventoryFromCachedPrices: no skins had applicable cached prices.");
        }
    }

    /// <summary>
    /// Finds a CaseData by its caseId by scanning active CaseCardUI instances.
    /// Returns null if not found.
    /// </summary>
    private static CaseData FindCaseData(string caseId)
    {
        if (string.IsNullOrEmpty(caseId)) return null;
        var cards = UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            var caseData = cards[i]?.data;
            if (caseData != null && caseData.caseId == caseId)
                return caseData;
        }
        return null;
    }

    /// <summary>
    /// Finds the matching CaseItemData by sourceCaseId + itemId/itemName.
    /// Used to resolve runtime-only data (sprites, collection info) after a save-load.
    /// </summary>
    private static CaseItemData FindMatchingCaseItem(string sourceCaseId, string itemId, string itemName)
    {
        var cards = UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            var card     = cards[i];
            var caseData = card != null ? card.data : null;
            if (caseData == null) continue;

            if (!string.IsNullOrEmpty(sourceCaseId) && caseData.caseId != sourceCaseId)
                continue;

            var items = caseData.possibleItems;
            if (items == null) continue;

            for (int j = 0; j < items.Count; j++)
            {
                var item = items[j];
                if (item == null) continue;
                if (!string.IsNullOrEmpty(itemId)   && item.itemId   == itemId)   return item;
                if (!string.IsNullOrEmpty(itemName) && item.itemName == itemName) return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Fallback lookup by weaponName + skinName when itemId and itemName both fail.
    /// </summary>
    private static CaseItemData FindMatchingCaseItemByNames(string sourceCaseId, string weaponName, string skinName)
    {
        if (string.IsNullOrEmpty(weaponName) && string.IsNullOrEmpty(skinName))
            return null;

        var cards = UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            var caseData = cards[i]?.data;
            if (caseData == null) continue;
            if (!string.IsNullOrEmpty(sourceCaseId) && caseData.caseId != sourceCaseId)
                continue;

            var items = caseData.possibleItems;
            if (items == null) continue;

            for (int j = 0; j < items.Count; j++)
            {
                var item = items[j];
                if (item == null) continue;
                if (item.weaponName == weaponName && item.skinName == skinName)
                    return item;
            }
        }
        return null;
    }

    private static Sprite ResolveItemIcon(string sourceCaseId, string itemId, string itemName)
    {
        var cards = UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            var caseData = card != null ? card.data : null;
            if (caseData == null) continue;

            if (!string.IsNullOrEmpty(sourceCaseId) && caseData.caseId != sourceCaseId)
                continue;

            var items = caseData.possibleItems;
            if (items == null) continue;

            for (int j = 0; j < items.Count; j++)
            {
                var item = items[j];
                if (item == null) continue;

                if (!string.IsNullOrEmpty(itemId) && item.itemId == itemId)
                    return item.itemIcon;

                if (!string.IsNullOrEmpty(itemName) && item.itemName == itemName)
                    return item.itemIcon;
            }
        }

        return null;
    }

    private static ItemWear GetWearFromFloat(float value)
    {
        float v = Mathf.Clamp01(value);
        if (v < 0.07f) return ItemWear.FactoryNew;
        if (v < 0.15f) return ItemWear.MinimalWear;
        if (v < 0.38f) return ItemWear.FieldTested;
        if (v < 0.45f) return ItemWear.WellWorn;
        return ItemWear.BattleScarred;
    }

}
