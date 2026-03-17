using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public enum UpgradeEffectType
{
    None,
    MoneyPerSecond,
    MoneyPerTap
}

[System.Serializable]
public class UpgradeDefinition
{
    public string id;
    public string displayName;
    public Sprite icon;

    [Header("Upgrade Values")]
    public float cost = 0f;
    public int requiredCasesOpened = 0;
    [TextArea] public string description = "";

    [Header("Effect")]
    public UpgradeEffectType effectType = UpgradeEffectType.None;
    public float effectAmount = 0f;

    [Header("Progression")]
    [Min(1)] public int maxLevel = 1;

    [HideInInspector]
    public int currentLevel = 0; // number of levels already purchased

    public int MaxLevel => Mathf.Max(1, maxLevel);

    public bool IsMaxed => currentLevel >= MaxLevel;

    public float CurrentCost => cost;
    public int CurrentRequiredCases => requiredCasesOpened;
    public string CurrentDescription => description ?? string.Empty;
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

    [Header("Testing")]
    [SerializeField] private bool grantTestMoneyOnPurchase = true;
    [SerializeField] private float testMoneyAmount = 10000f;

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

        ValidateDefinitions();
    }

    private void ValidateDefinitions()
    {
        if (upgrades == null) return;
        foreach (var def in upgrades)
        {
            if (def == null) continue;
            if (def.maxLevel <= 0) def.maxLevel = 1;
            if (def.cost < 0f) def.cost = 0f;
            if (def.requiredCasesOpened < 0) def.requiredCasesOpened = 0;
        }
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

        // Apply gameplay effect
        ApplyUpgradeEffect(def);

        // Increase level
        def.currentLevel++;

        // Test reward money (for quick verification)
        if (grantTestMoneyOnPurchase && testMoneyAmount > 0f && BalanceManager.Instance != null)
        {
            BalanceManager.Instance.AddMoney(testMoneyAmount);
            Debug.Log($"[UpgradeManager] Test reward: +${testMoneyAmount:F2}");
        }

        // Record stats
        StatisticsManager.Instance?.RecordUpgradePurchased(def.id, cost);

        // Notify
        OnUpgradePurchased?.Invoke(def.id, def.currentLevel);
        OnUpgradesChanged?.Invoke();

        return true;
    }

    private void ApplyUpgradeEffect(UpgradeDefinition def)
    {
        if (def == null) return;
        if (def.effectAmount == 0f) return;

        switch (def.effectType)
        {
            case UpgradeEffectType.MoneyPerSecond:
                BalanceManager.Instance?.UpgradeBaseMoneyPerSecond(def.effectAmount);
                break;
            case UpgradeEffectType.MoneyPerTap:
                ClickerController.Instance?.UpgradeBaseMoneyPerClick(def.effectAmount);
                break;
            case UpgradeEffectType.None:
            default:
                break;
        }
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

    /// <summary>
    /// DEV: Reset all upgrade levels back to 0.
    /// You can hook this to a UI Button OnClick.
    /// </summary>
    public void ResetAllUpgrades()
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            var def = upgrades[i];
            if (def == null) continue;
            def.currentLevel = 0;
        }

        Debug.Log("[UpgradeManager] All upgrades reset to level 0.");
        OnUpgradesChanged?.Invoke();
    }

    [ContextMenu("DEV/Reset All Upgrades")]
    private void ResetAllUpgradesFromContextMenu()
    {
        ResetAllUpgrades();
    }
}
