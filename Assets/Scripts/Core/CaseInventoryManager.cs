using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks owned counts, unlock state, and handles purchasing keys and cases.
/// Provides queries for UI and operations. Integrates with BalanceManager for payments.
/// </summary>
public class CaseInventoryManager : MonoBehaviour
{
    public static CaseInventoryManager Instance { get; private set; }

    [System.Serializable]
    public class CaseEntry
    {
        public string caseId;
        public int ownedCount;
        public bool unlocked;
        public float keyPrice; // override if you want a custom key price per caseId
        public float casePriceOverride; // optional override for case purchase price
        public float caseSellPriceOverride; // optional override for selling price
    }

    [Header("Initial Data (optional)")]
    public List<CaseEntry> initialEntries = new List<CaseEntry>();

    private readonly Dictionary<string, CaseEntry> _entries = new Dictionary<string, CaseEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Seed from initial entries
        foreach (var e in initialEntries)
        {
            if (e == null || string.IsNullOrEmpty(e.caseId)) continue;
            _entries[e.caseId] = new CaseEntry {
                caseId = e.caseId,
                ownedCount = e.ownedCount,
                unlocked = e.unlocked,
                keyPrice = e.keyPrice,
                casePriceOverride = e.casePriceOverride,
                caseSellPriceOverride = e.caseSellPriceOverride
            };
        }
    }

    public bool IsCaseUnlocked(string caseId)
    {
        if (string.IsNullOrEmpty(caseId)) return false;
        if (_entries.TryGetValue(caseId, out var e)) return e.unlocked;
        return false;
    }

    public void SetUnlocked(string caseId, bool unlocked)
    {
        if (string.IsNullOrEmpty(caseId)) return;
        if (!_entries.TryGetValue(caseId, out var e))
        {
            e = new CaseEntry { caseId = caseId, unlocked = unlocked };
            _entries[caseId] = e;
        }
        e.unlocked = unlocked;
    }

    public int GetCaseCount(string caseId)
    {
        if (string.IsNullOrEmpty(caseId)) return 0;
        if (_entries.TryGetValue(caseId, out var e)) return e.ownedCount;
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
        if (string.IsNullOrEmpty(caseId) || amount <= 0) return;
        if (!_entries.TryGetValue(caseId, out var e))
        {
            e = new CaseEntry { caseId = caseId, unlocked = true };
            _entries[caseId] = e;
        }
        e.ownedCount += amount;
    }

    public void RemoveCases(string caseId, int amount)
    {
        if (string.IsNullOrEmpty(caseId) || amount <= 0) return;
        if (_entries.TryGetValue(caseId, out var e))
        {
            e.ownedCount = Mathf.Max(0, e.ownedCount - amount);
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
        AddCases(data.caseId, 1);
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
        if (string.IsNullOrEmpty(caseId)) return defaultPrice;
        if (_entries.TryGetValue(caseId, out var e) && e.keyPrice > 0f) return e.keyPrice;
        return defaultPrice;
    }

    public void SetKeyPrice(string caseId, float price)
    {
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
}
