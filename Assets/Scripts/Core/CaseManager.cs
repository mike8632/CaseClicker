using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Manages Case Coin progress (0-100%) and case drops.
/// When progress reaches 100%, a random case is dropped.
/// </summary>
public class CaseProgressManager : MonoBehaviour
{
    public static CaseProgressManager Instance { get; private set; }

    [Header("Progress Settings")]
    [SerializeField] private float maxProgress = 100f;
    [SerializeField] private float baseCasePercentPerSecond = 0f; // Idle case progress

    // Events
    public UnityEvent<float> OnProgressChanged;           // Current progress %
    public UnityEvent<float, float> OnProgressGained;     // (amount gained, new total)
    public UnityEvent<CaseData> OnCaseDropped;            // When 100% reached
    public UnityEvent OnProgressReset;

    // Current progress (0-100)
    private float currentProgress = 0f;

    // Idle income multipliers
    private float casePerSecondMultiplier = 1f;

    // Statistics
    private int totalCasesDropped = 0;
    private float totalProgressEarned = 0f;

    // Case pool (to be populated with actual case data)
    [Header("Case Pool")]
    [SerializeField] private List<CaseData> availableCases = new List<CaseData>();

    #region Properties

    public float CurrentProgress => currentProgress;
    public float MaxProgress => maxProgress;
    public float ProgressPercentage => (currentProgress / maxProgress) * 100f;
    public int TotalCasesDropped => totalCasesDropped;
    public float TotalProgressEarned => totalProgressEarned;
    public float BaseCasePercentPerSecond => baseCasePercentPerSecond;
    public float CasePerSecondMultiplier => casePerSecondMultiplier;

    /// <summary>
    /// Current Case % Per Second including all multipliers. 
    /// </summary>
    public float CurrentCasePercentPerSecond => baseCasePercentPerSecond * casePerSecondMultiplier;

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize events
        OnProgressChanged ??= new UnityEvent<float>();
        OnProgressGained ??= new UnityEvent<float, float>();
        OnCaseDropped ??= new UnityEvent<CaseData>();
        OnProgressReset ??= new UnityEvent();

        // Ensure multiplier is never 0 (fixes 0 value bug from serialization)
        if (casePerSecondMultiplier <= 0f) casePerSecondMultiplier = 1f;
        if (maxProgress <= 0f) maxProgress = 100f;
    }

    private void Start()
    {
        // Notify UI of initial progress
        OnProgressChanged?.Invoke(currentProgress);
    }

    #region Progress Operations

    /// <summary>
    /// Add progress to the Case Coin (from clicking). 
    /// </summary>
    public void AddProgress(float amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[CaseProgress] Attempted to add zero or negative progress.");
            return;
        }

        currentProgress += amount;
        totalProgressEarned += amount;

        OnProgressGained?.Invoke(amount, currentProgress);
        OnProgressChanged?.Invoke(currentProgress);
        GameManager.Instance?.OnCaseProgressChanged?.Invoke(currentProgress);

        Debug.Log($"[CaseProgress] +{amount:F2}% | Total: {currentProgress:F2}%");

        // Check if we've reached 100%
        CheckForCaseDrop();
    }

    /// <summary>
    /// Check if progress has reached 100% and drop a case if so.
    /// </summary>
    private void CheckForCaseDrop()
    {
        while (currentProgress >= maxProgress)
        {
            // Drop a case
            DropCase();

            // Remove 100% from progress (allowing overflow for multiple cases)
            currentProgress -= maxProgress;
        }

        OnProgressChanged?.Invoke(currentProgress);
    }

    /// <summary>
    /// Drop a random case from the available pool.
    /// </summary>
    private void DropCase()
    {
        totalCasesDropped++;

        CaseData droppedCase = GetRandomCase();

        if (droppedCase != null)
        {
            // Add the dropped case to inventory (only if unlocked)
            if (CaseInventoryManager.Instance == null || CaseInventoryManager.Instance.IsCaseUnlocked(droppedCase.caseId))
            {
                CaseInventoryManager.Instance?.AddCases(droppedCase.caseId, 1);
            }

            Debug.Log($"[CaseProgress] Case Dropped: {droppedCase.caseName}!");
            OnCaseDropped?.Invoke(droppedCase);
            GameManager.Instance?.OnCaseDropped?.Invoke(droppedCase);

            // Record statistics
            GameManager.Instance?.Statistics?.RecordCaseDrop(droppedCase);
        }
        else
        {
            Debug.Log($"[CaseProgress] Case Dropped!  (No case data available - Case #{totalCasesDropped})");
            OnCaseDropped?.Invoke(null);
            GameManager.Instance?.OnCaseDropped?.Invoke(null);
        }

        OnProgressReset?.Invoke();
    }

    /// <summary>
    /// Get a random unlocked case from the available pool (falls back to all if none unlocked).
    /// Uses weighted random based on case rarity/drop rates.
    /// </summary>
    private CaseData GetRandomCase()
    {
        if (availableCases == null || availableCases.Count == 0)
            return null;

        // Build unlocked list if inventory available
        List<CaseData> pool = availableCases;
        if (CaseInventoryManager.Instance != null)
        {
            var unlocked = new List<CaseData>();
            foreach (var c in availableCases)
            {
                if (c != null && CaseInventoryManager.Instance.IsCaseUnlocked(c.caseId))
                    unlocked.Add(c);
            }
            if (unlocked.Count > 0)
                pool = unlocked;
        }

        // Calculate total weight
        float totalWeight = 0f;
        foreach (var caseData in pool)
        {
            if (caseData != null)
                totalWeight += caseData.dropWeight;
        }

        // Random selection based on weight
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var caseData in pool)
        {
            if (caseData == null) continue;

            currentWeight += caseData.dropWeight;
            if (randomValue <= currentWeight)
                return caseData;
        }

        // Fallback to first case
        return pool[0];
    }

    /// <summary>
    /// Force drop a case (for testing or special events).
    /// </summary>
    public void ForceDropCase()
    {
        DropCase();
    }

    /// <summary>
    /// Set progress directly (used for loading save data).
    /// </summary>
    public void SetProgress(float progress)
    {
        currentProgress = Mathf.Clamp(progress, 0f, maxProgress);
        OnProgressChanged?.Invoke(currentProgress);
        Debug.Log($"[CaseProgress] Progress set to: {currentProgress:F2}%");
    }

    /// <summary>
    /// Set statistics (used for loading save data).
    /// </summary>
    public void SetStatistics(int casesDropped, float progressEarned)
    {
        totalCasesDropped = casesDropped;
        totalProgressEarned = progressEarned;
    }

    #endregion

    #region Idle Income (Case % Per Second)

    /// <summary>
    /// Called by IdleIncomeSystem to add passive case progress.
    /// </summary>
    public void AddIdleProgress(float deltaTime)
    {
        if (CurrentCasePercentPerSecond <= 0) return;

        float idleProgress = CurrentCasePercentPerSecond * deltaTime;
        AddProgress(idleProgress);
    }

    /// <summary>
    /// Calculate offline case progress for a given time period.
    /// </summary>
    public float CalculateOfflineProgress(float seconds)
    {
        return CurrentCasePercentPerSecond * seconds;
    }

    #endregion

    #region Upgrade Methods

    /// <summary>
    /// Upgrade base Case % Per Second. 
    /// </summary>
    public void UpgradeBaseCasePercentPerSecond(float additionalCPS)
    {
        baseCasePercentPerSecond += additionalCPS;
        StatisticsManager.Instance?.OnStatisticChanged?.Invoke("casePerSecond", CurrentCasePercentPerSecond);
        Debug.Log($"[CaseProgress] Base CPS upgraded to: {baseCasePercentPerSecond:F2}%/s");
    }

    /// <summary>
    /// Upgrade Case Per Second multiplier. 
    /// </summary>
    public void UpgradeCasePerSecondMultiplier(float additionalMultiplier)
    {
        casePerSecondMultiplier += additionalMultiplier;
        StatisticsManager.Instance?.OnStatisticChanged?.Invoke("casePerSecond", CurrentCasePercentPerSecond);
        Debug.Log($"[CaseProgress] CPS Multiplier upgraded to: {casePerSecondMultiplier:F2}x");
    }

    /// <summary>
    /// Set idle values directly (used for loading save data).
    /// </summary>
    public void SetIdleValues(float baseCPS, float cpsMultiplier)
    {
        baseCasePercentPerSecond = baseCPS;
        casePerSecondMultiplier = cpsMultiplier > 0f ? cpsMultiplier : 1f;
        StatisticsManager.Instance?.OnStatisticChanged?.Invoke("casePerSecond", CurrentCasePercentPerSecond);
    }

    #endregion

    #region Case Pool Management

    /// <summary>
    /// Add a case to the available pool.
    /// </summary>
    public void AddCaseToPool(CaseData caseData)
    {
        if (caseData != null && !availableCases.Contains(caseData))
        {
            availableCases.Add(caseData);
        }
    }

    /// <summary>
    /// Remove a case from the available pool.
    /// </summary>
    public void RemoveCaseFromPool(CaseData caseData)
    {
        availableCases.Remove(caseData);
    }

    /// <summary>
    /// Award a case directly by its id (increments count if unlocked).
    /// </summary>
    public void AwardCaseById(string caseId)
    {
        if (string.IsNullOrEmpty(caseId) || CaseInventoryManager.Instance == null) return;
        if (!CaseInventoryManager.Instance.IsCaseUnlocked(caseId)) return;
        CaseInventoryManager.Instance.AddCases(caseId, 1);
        OnCaseDropped?.Invoke(FindCaseById(caseId));
        GameManager.Instance?.OnCaseDropped?.Invoke(FindCaseById(caseId));
    }

    /// <summary>
    /// Award one random unlocked case from the available pool.
    /// </summary>
    public void AwardRandomUnlockedCase()
    {
        var c = GetRandomCase();
        if (c == null) return;
        if (CaseInventoryManager.Instance == null) return;
        if (!CaseInventoryManager.Instance.IsCaseUnlocked(c.caseId)) return;
        CaseInventoryManager.Instance.AddCases(c.caseId, 1);
        OnCaseDropped?.Invoke(c);
        GameManager.Instance?.OnCaseDropped?.Invoke(c);
    }

    // Helper to find a case by id from the available pool
    private CaseData FindCaseById(string caseId)
    {
        if (string.IsNullOrEmpty(caseId)) return null;
        // search availableCases list
        foreach (var c in availableCases)
        {
            if (c != null && c.caseId == caseId) return c;
        }
        return null;
    }

    #endregion
}

/// <summary>
/// Data class for case information.
/// Create as ScriptableObject for easy case management.
/// </summary>
[System.Serializable]
public class CaseData
{
    public string caseName;
    public string caseId;
    public Sprite caseIcon;
    public float dropWeight = 1f;       // Higher = more common
    public float keyPrice = 2.50f;      // Cost to open the case
    public float casePrice = 0.60f;     // Cost to buy the case itself
    public CaseRarity rarity;

    // Item pool for this case (to be expanded)
    public List<CaseItemData> possibleItems = new List<CaseItemData>();
}

/// <summary>
/// Data class for items that can be obtained from cases.
/// </summary>
[System.Serializable]
public class CaseItemData
{
    public string itemName;
    public string itemId;
    public Sprite itemIcon;
    public float dropChance;            // Percentage chance (0-100)
    public float minValue;              // Minimum market value
    public float maxValue;              // Maximum market value
    public ItemRarity rarity;
    public ItemWear wear;
}

/// <summary>
/// Case rarity tiers.
/// </summary>
public enum CaseRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Item rarity tiers (CS:GO style).
/// </summary>
public enum ItemRarity
{
    ConsumerGrade,      // White
    IndustrialGrade,    // Light Blue
    MilSpec,            // Blue
    Restricted,         // Purple
    Classified,         // Pink
    Covert,             // Red
    Contraband          // Gold (Knives, Gloves)
}

/// <summary>
/// Item wear conditions.
/// </summary>
public enum ItemWear
{
    FactoryNew,
    MinimalWear,
    FieldTested,
    WellWorn,
    BattleScarred
}