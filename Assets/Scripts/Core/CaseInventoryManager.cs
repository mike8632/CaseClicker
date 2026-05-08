using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks owned counts, unlock state, and handles purchasing keys and cases.
/// Provides queries for UI and operations. Integrates with BalanceManager for payments.
/// </summary>
public class CaseInventoryManager : MonoBehaviour
{
    public static CaseInventoryManager Instance { get; private set; }

    private static string NormalizeCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId)) return null;
        return caseId.Trim();
    }

    // Unlock requests made before the inventory manager exists are queued here.
    private static readonly HashSet<string> _pendingUnlockCaseIds = new HashSet<string>();

    [System.Serializable]
    public class CaseEntry
    {
        public string caseId;
        public int ownedCount;            // number of cases owned
        public int ownedKeys;             // number of keys owned for this case
        public bool unlocked;
        public float keyPrice;            // override key price per caseId
        public float casePriceOverride;   // optional override for case purchase price
        public float caseSellPriceOverride; // optional override for selling price
    }

    [Header("Initial Data (optional)")]
    public List<CaseEntry> initialEntries = new List<CaseEntry>();

    [Header("Default Drop Chances (used when item dropChance is 0)")]
    [SerializeField] private float milSpecDropChance = 79.92f;
    [SerializeField] private float restrictedDropChance = 15.98f;
    [SerializeField] private float classifiedDropChance = 3.2f;
    [SerializeField] private float covertDropChance = 0.62f;
    [SerializeField] private float knifeDropChance = 0.26f;

    private readonly Dictionary<string, CaseEntry> _entries = new Dictionary<string, CaseEntry>();

    // Event fired when a case count changes (caseId provided)
    public UnityEngine.Events.UnityEvent<string> OnCaseCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize event
        OnCaseCountChanged ??= new UnityEngine.Events.UnityEvent<string>();

        // Seed from initial entries
        foreach (var e in initialEntries)
        {
            if (e == null) continue;
            string id = NormalizeCaseId(e.caseId);
            if (string.IsNullOrEmpty(id)) continue;

            _entries[id] = new CaseEntry {
                caseId = id,
                ownedCount = e.ownedCount,
                ownedKeys = e.ownedKeys,
                unlocked = e.unlocked,
                keyPrice = e.keyPrice,
                casePriceOverride = e.casePriceOverride,
                caseSellPriceOverride = e.caseSellPriceOverride
            };
        }

        ApplyPendingUnlocks();
    }

    public static void UnlockCaseNowOrQueue(string caseId)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId)) return;

        if (Instance != null)
        {
            Instance.SetUnlocked(caseId, true);
        }
        else
        {
            _pendingUnlockCaseIds.Add(caseId);
        }
    }

    private void ApplyPendingUnlocks()
    {
        if (_pendingUnlockCaseIds.Count == 0) return;

        foreach (var caseId in _pendingUnlockCaseIds)
        {
            SetUnlocked(caseId, true);
        }
        _pendingUnlockCaseIds.Clear();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameInitialized.AddListener(OnGameInitialized);
        }
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnLoadCompleted.AddListener(OnSaveLoadCompleted);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameInitialized.RemoveListener(OnGameInitialized);
        }
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnLoadCompleted.RemoveListener(OnSaveLoadCompleted);
        }
    }

    private void OnGameInitialized()
    {
        BroadcastInventoryChanged();
    }

    private void OnSaveLoadCompleted()
    {
        BroadcastInventoryChanged();
    }

    /// <summary>
    /// Emit OnCaseCountChanged for all known entries to force UI refresh.
    /// </summary>
    public void BroadcastInventoryChanged()
    {
        foreach (var kv in _entries)
        {
            var id = kv.Key;
            OnCaseCountChanged?.Invoke(id);
        }
    }

    public bool IsCaseUnlocked(string caseId)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId)) return false;
        if (_entries.TryGetValue(caseId, out var e)) return e.unlocked;
        return false;
    }

    public void SetUnlocked(string caseId, bool unlocked)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId)) return;
        if (!_entries.TryGetValue(caseId, out var e))
        {
            e = new CaseEntry { caseId = caseId, unlocked = unlocked };
            _entries[caseId] = e;
        }
        e.unlocked = unlocked;

        // Notify UI even if only unlock state changed (count may be unchanged)
        OnCaseCountChanged?.Invoke(caseId);
    }

    public int GetCaseCount(string caseId)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId)) return 0;
        if (_entries.TryGetValue(caseId, out var e)) return e.ownedCount;
        return 0;
    }

    public int GetKeyCount(string caseId)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId)) return 0;
        if (_entries.TryGetValue(caseId, out var e)) return e.ownedKeys;
        return 0;
    }

    public int GetTotalOwnedCounts()
    {
        int sum = 0;
        foreach (var kv in _entries)
        {
            sum += kv.Value.ownedCount;
        }
        return sum;
    }

    public int GetTotalDistinctUnlocked()
    {
        int count = 0;
        foreach (var kv in _entries)
        {
            if (kv.Value.unlocked) count++;
        }
        return count;
    }

    public void AddCases(string caseId, int amount)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId) || amount <= 0) return;
        if (!_entries.TryGetValue(caseId, out var e))
        {
            e = new CaseEntry { caseId = caseId, unlocked = true };
            _entries[caseId] = e;
        }
        e.ownedCount += amount;

        // auto-unlock if we now own at least 1
        if (e.ownedCount > 0)
            e.unlocked = true;

        OnCaseCountChanged?.Invoke(caseId);
    }


    public void RemoveCases(string caseId, int amount)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId) || amount <= 0) return;
        if (_entries.TryGetValue(caseId, out var e))
        {
            e.ownedCount = Mathf.Max(0, e.ownedCount - amount);
            OnCaseCountChanged?.Invoke(caseId);
        }
    }

    public void AddKeys(string caseId, int amount)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId) || amount <= 0) return;
        if (!_entries.TryGetValue(caseId, out var e))
        {
            e = new CaseEntry { caseId = caseId, unlocked = true };
            _entries[caseId] = e;
        }
        e.ownedKeys += amount;
    }

    public void RemoveKeys(string caseId, int amount)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId) || amount <= 0) return;
        if (_entries.TryGetValue(caseId, out var e))
        {
            e.ownedKeys = Mathf.Max(0, e.ownedKeys - amount);
        }
    }

    public bool TryBuyCaseKey(CaseData data)
    {
        if (data == null || string.IsNullOrEmpty(data.caseId)) return false;
        // Locked cases cannot be purchased
        if (!IsCaseUnlocked(data.caseId)) return false;

        double money = BalanceManager.Instance != null ? BalanceManager.Instance.CurrentMoney : 0.0;
        float price = GetKeyPrice(data.caseId, data.keyPrice);
        if (money < price) return false;

        BalanceManager.Instance?.SpendMoney(price);
        AddKeys(data.caseId, 1); // keys, not cases
        return true;
    }

    public void BuyCaseKey(CaseData data)
    {
        if (!TryBuyCaseKey(data))
        {
            Debug.LogWarning($"[CaseInventory] Could not buy key for case '{data?.caseName}' (locked or insufficient funds)");
        }
    }

    public float GetKeyPrice(string caseId, float defaultPrice)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId)) return defaultPrice;
        if (_entries.TryGetValue(caseId, out var e) && e.keyPrice > 0f) return e.keyPrice;
        return defaultPrice;
    }

    public void SetKeyPrice(string caseId, float price)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId) || price <= 0f) return;
        if (!_entries.TryGetValue(caseId, out var e))
        {
            e = new CaseEntry { caseId = caseId };
            _entries[caseId] = e;
        }
        e.keyPrice = price;
    }

    // Case purchase (not key). Uses CaseData.casePrice or override.
    public bool TryBuyCase(CaseData data)
    {
        if (data == null || string.IsNullOrEmpty(data.caseId)) return false;
        if (!IsCaseUnlocked(data.caseId)) return false;
        double money = BalanceManager.Instance != null ? BalanceManager.Instance.CurrentMoney : 0.0;
        float price = GetCasePrice(data);
        if (money < price) return false;
        BalanceManager.Instance?.SpendMoney(price);
        AddCases(data.caseId, 1);
        return true;
    }

    public void BuyCase(CaseData data)
    {
        if (!TryBuyCase(data))
        {
            Debug.LogWarning($"[CaseInventory] Could not buy case '{data?.caseName}' (locked or insufficient funds)");
        }
    }

    public float GetCasePrice(CaseData data)
    {
        if (data == null) return 0f;
        if (_entries.TryGetValue(data.caseId, out var e) && e.casePriceOverride > 0f) return e.casePriceOverride;
        return data.casePrice > 0f ? data.casePrice : data.keyPrice; // fallback
    }

    // Selling case back for money
    public float GetCaseSellPrice(CaseData data)
    {
        if (data == null) return 0f;
        if (_entries.TryGetValue(data.caseId, out var e) && e.caseSellPriceOverride > 0f) return e.caseSellPriceOverride;
        return Mathf.Max(0.01f, GetCasePrice(data) * 0.25f); // default 25% of buy price
    }

    public bool TrySellCase(CaseData data)
    {
        if (data == null || string.IsNullOrEmpty(data.caseId)) return false;
        int count = GetCaseCount(data.caseId);
        if (count <= 0) return false;
        float sellPrice = GetCaseSellPrice(data);
        RemoveCases(data.caseId, 1);
        BalanceManager.Instance?.AddMoney(sellPrice);
        return true;
    }

    public void SellCase(CaseData data)
    {
        if (!TrySellCase(data))
        {
            Debug.LogWarning($"[CaseInventory] Could not sell case '{data?.caseName}' (none owned)");
        }
    }

    // Opening a case: requires at least 1 case and 1 key, consumes both
    public bool TryOpenCase(CaseData data)
    {
        if (data == null || string.IsNullOrEmpty(data.caseId)) return false;
        if (GetCaseCount(data.caseId) <= 0) return false;
        if (GetKeyCount(data.caseId) <= 0) return false;

        CaseData caseDataForOpen = ResolveCaseDataForOpen(data);
        int configuredCount = caseDataForOpen.possibleItems != null ? caseDataForOpen.possibleItems.Count : -1;
        Debug.Log($"[CaseInventory] TryOpenCase caseId='{caseDataForOpen.caseId}' caseName='{caseDataForOpen.caseName}' possibleItemsCount={configuredCount}");

        // Roll item result first (based on case item pool)
        CaseItemData rolledItem = RollRandomItem(caseDataForOpen);
        float rolledValue = 0f;
        float rolledFloat = 0f;
        if (rolledItem == null)
        {
            Debug.LogWarning($"[CaseInventory] Opened case '{caseDataForOpen.caseId}' but no valid items are configured in possibleItems. No skin was added.");
        }
        else
        {
            float floatMin = Mathf.Clamp01(Mathf.Min(rolledItem.floatMin, rolledItem.floatMax));
            float floatMax = Mathf.Clamp01(Mathf.Max(rolledItem.floatMin, rolledItem.floatMax));
            rolledFloat = floatMax > floatMin ? Random.Range(floatMin, floatMax) : floatMin;
            rolledValue = rolledItem.GetValueForFloat(rolledFloat);
        }

        RemoveCases(data.caseId, 1);
        RemoveKeys(data.caseId, 1);

        // Add rolled skin/item to skin inventory if available
        if (rolledItem != null && SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.AddSkin(rolledItem, caseDataForOpen.caseId, rolledValue, rolledFloat);
        }
        else if (rolledItem != null)
        {
            Debug.LogWarning("[CaseInventory] SkinInventoryManager is missing; rolled item was not added to UI inventory.");
        }

        // Trigger whatever system opens the case (not implemented here)
        if (GameManager.Instance != null)
            GameManager.Instance.RaiseCaseOpened(caseDataForOpen, rolledItem);

        // Record case opened statistic with rolled item details
        StatisticsManager.Instance?.RecordCaseOpened(caseDataForOpen, rolledItem, rolledValue);

        Debug.Log($"[CaseInventory] Opened case '{caseDataForOpen.caseId}' - total opened now: {StatisticsManager.Instance?.TotalCasesOpened}");

        return true;
    }

    private static CaseData ResolveCaseDataForOpen(CaseData input)
    {
        if (input == null) return null;

        if (input.possibleItems != null && input.possibleItems.Count > 0)
            return input;

        var cards = Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            var cardData = card != null ? card.data : null;
            if (cardData == null) continue;
            if (cardData.caseId != input.caseId) continue;
            if (cardData.possibleItems == null || cardData.possibleItems.Count == 0) continue;

            Debug.LogWarning($"[CaseInventory] Resolved case data fallback for caseId='{input.caseId}' using active CaseCardUI '{card.name}'.");
            return cardData;
        }

        return input;
    }

    private CaseItemData RollRandomItem(CaseData data)
    {
        if (data == null || data.possibleItems == null || data.possibleItems.Count == 0)
        {
            Debug.LogWarning($"[CaseInventory] RollRandomItem failed early for caseId='{data?.caseId}'. possibleItems is null or empty.");
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < data.possibleItems.Count; i++)
        {
            var item = data.possibleItems[i];
            if (item == null)
            {
                Debug.LogWarning($"[CaseInventory] RollRandomItem item[{i}] is null for caseId='{data.caseId}'.");
                continue;
            }
            float chance = GetEffectiveDropChance(item);
            Debug.Log($"[CaseInventory] RollRandomItem item[{i}] id='{item.itemId}' name='{item.itemName}' chance={chance}");
            totalWeight += Mathf.Max(0f, chance);
        }

        if (totalWeight <= 0f)
            return data.possibleItems[0];

        float roll = Random.Range(0f, totalWeight);
        float running = 0f;

        for (int i = 0; i < data.possibleItems.Count; i++)
        {
            var item = data.possibleItems[i];
            if (item == null) continue;

            running += Mathf.Max(0f, GetEffectiveDropChance(item));
            if (roll <= running)
                return item;
        }

        return data.possibleItems[0];
    }

    public float GetEffectiveDropChance(CaseItemData item)
    {
        if (item == null)
            return 0f;

        if (item.dropChance > 0f)
            return item.dropChance;

        return GetDefaultDropChance(item.GetEffectiveRarity());
    }

    private float GetDefaultDropChance(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.MilSpec:
                return milSpecDropChance;
            case ItemRarity.Restricted:
                return restrictedDropChance;
            case ItemRarity.Classified:
                return classifiedDropChance;
            case ItemRarity.Covert:
                return covertDropChance;
            case ItemRarity.Knife:
                return knifeDropChance;
            default:
                return 0f;
        }
    }

    public void OpenCase(CaseData data)
    {
        if (!TryOpenCase(data))
        {
            Debug.LogWarning($"[CaseInventory] Could not open case '{data?.caseName}' (requires 1 case and 1 key)");
        }
    }
    public string GetRandomUnlockedCaseId(bool requireAtLeastOneOwned = false)
    {
        var candidates = new List<string>();

        foreach (var kv in _entries)
        {
            var e = kv.Value;
            if (!e.unlocked)
                continue;

            if (requireAtLeastOneOwned && e.ownedCount <= 0)
                continue;

            // use the caseId stored in the entry
            if (!string.IsNullOrEmpty(e.caseId))
                candidates.Add(e.caseId);
        }

        if (candidates.Count == 0)
            return null;

        int index = Random.Range(0, candidates.Count);
        return candidates[index];
    }


    public System.Collections.Generic.List<CaseEntryDTO> GetSnapshot()
    {
        var list = new System.Collections.Generic.List<CaseEntryDTO>();
        foreach (var kv in _entries)
        {
            var e = kv.Value;
            list.Add(new CaseEntryDTO
            {
                caseId = e.caseId,
                ownedCount = e.ownedCount,
                ownedKeys = e.ownedKeys,
                unlocked = e.unlocked,
                keyPrice = e.keyPrice,
                casePriceOverride = e.casePriceOverride,
                caseSellPriceOverride = e.caseSellPriceOverride
            });
        }
        return list;
    }

    public bool HasEntry(string caseId)
    {
        caseId = NormalizeCaseId(caseId);
        if (string.IsNullOrEmpty(caseId)) return false;
        return _entries.ContainsKey(caseId);
    }
}
