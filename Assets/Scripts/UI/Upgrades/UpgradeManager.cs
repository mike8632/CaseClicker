using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class UpgradeDefinition
{
    public string id;
    public string displayName;
    public Sprite icon;

    // Level arrays: index 0 = level 1, etc.
    public float[] costs = new float[0];
    public int[] requiredCasesOpened = new int[0];
    public string[] descriptions = new string[0];

    [HideInInspector]
    public int currentLevel = 0; // number of levels already purchased

    public int MaxLevel => costs != null ? costs.Length : 0;

    public bool IsMaxed => currentLevel >= MaxLevel;

    public float CurrentCost => (currentLevel < MaxLevel) ? costs[currentLevel] : 0f;
    public int CurrentRequiredCases => (currentLevel < MaxLevel) ? requiredCasesOpened[currentLevel] : 0;
    public string CurrentDescription => (currentLevel < MaxLevel && descriptions != null && descriptions.Length > currentLevel) ? descriptions[currentLevel] : string.Empty;
}

/// <summary>
/// Simple Upgrade manager for the upgrades UI. Stores definitions and handles purchases.
/// Other systems can listen to OnUpgradePurchased to apply effects.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Definitions")]
    public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

    // Fired when an upgrade is purchased: (upgradeId, newLevel)
    public UnityEvent<string, int> OnUpgradePurchased;

    // Fired whenever any upgrade state changes (useful for UI to refresh)
    public UnityEvent OnUpgradesChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        OnUpgradePurchased ??= new UnityEvent<string, int>();
        OnUpgradesChanged ??= new UnityEvent();
    }

    public UpgradeDefinition GetUpgrade(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return upgrades.Find(u => u != null && u.id == id);
    }

    /// <summary>
    /// Attempt to purchase the next level for an upgrade.
    /// Returns true when purchase succeeds.
    /// </summary>
    public bool PurchaseUpgrade(string id)
    {
        var def = GetUpgrade(id);
        if (def == null) return false;
        if (def.IsMaxed) return false;

        // Requirement: cases opened
        int required = def.CurrentRequiredCases;
        int opened = StatisticsManager.Instance != null ? StatisticsManager.Instance.TotalCasesOpened : 0;
        if (opened < required) return false;

        float cost = def.CurrentCost;

        // Check money
        double money = 0.0;
        if (BalanceManager.Instance != null)
            money = BalanceManager.Instance.CurrentMoney;
        if (money < cost) return false;

        // Spend money
        if (BalanceManager.Instance != null)
            BalanceManager.Instance.SpendMoney(cost);

        // Increase level
        def.currentLevel++;

        // Record stats
        StatisticsManager.Instance?.RecordUpgradePurchased(def.id, cost);

        // Notify
        OnUpgradePurchased?.Invoke(def.id, def.currentLevel);
        OnUpgradesChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// Helper to get progress string for UI (opened / required).
    /// </summary>
    public string GetRequirementText(UpgradeDefinition def)
    {
        if (def == null) return string.Empty;
        if (def.IsMaxed) return "MAXED";
        int required = def.CurrentRequiredCases;
        int opened = StatisticsManager.Instance != null ? StatisticsManager.Instance.TotalCasesOpened : 0;
        return $"OPENED CASES {opened}/{required}";
    }
}
