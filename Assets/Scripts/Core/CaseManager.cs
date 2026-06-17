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
    /// Drop a +1 to a random unlocked case in the inventory.
    /// </summary>
    private void DropCase()
    {
        totalCasesDropped++;

        CaseData droppedCase = null;

        // Make sure we have an inventory
        if (CaseInventoryManager.Instance != null)
        {
            // Pick a random unlocked caseId
            string caseId = CaseInventoryManager.Instance
                .GetRandomUnlockedCaseId(requireAtLeastOneOwned: false);

            if (!string.IsNullOrEmpty(caseId))
            {
                // Give +1 of that case
                CaseInventoryManager.Instance.AddCases(caseId, 1);

                // Optional: try to find a matching CaseData for stats / UI.
                // If you don't maintain availableCases, this will just stay null.
                droppedCase = FindCaseById(caseId);
            }
        }

        if (droppedCase != null)
        {
            Debug.Log($"[CaseProgress] Case Dropped: {droppedCase.caseName}!");
            OnCaseDropped?.Invoke(droppedCase);
            GameManager.Instance?.OnCaseDropped?.Invoke(droppedCase);
            GameManager.Instance?.Statistics?.RecordCaseDrop(droppedCase);
        }
        else
        {
            Debug.Log($"[CaseProgress] Case Dropped! (No unlocked case to award - Drop #{totalCasesDropped})");
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

    /// <summary>
    /// Award one random unlocked case from the available pool, accepting a caseId parameter for compatibility.
    /// The provided caseId is ignored; a random unlocked case will be incremented by +1.
    /// </summary>
    public void AwardRandomUnlockedCase(string _)
    {
        AwardRandomUnlockedCase();
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

    // ── Case-level collection defaults ────────────────────────────────────────
    /// <summary>
    /// Collection for all items in this case (e.g. "dreams_nightmares").
    /// Items use this by default unless CaseItemData.overrideCollection is true.
    /// </summary>
    public string collectionId;
    public string collectionName;
    public Sprite collectionIcon;
}

/// <summary>
/// Data class for items that can be obtained from cases.
/// </summary>
[System.Serializable]
public class CaseItemData
{
    public string weaponName;
    public string skinName;
    public string itemName;
    public string itemId;
    public Sprite itemIcon;
    public float dropChance;            // Percentage chance (0-100)
    public float minValue;              // Minimum market value
    public float maxValue;              // Maximum market value
    public float floatMin = 0f;          // Minimum float (0-1)
    public float floatMax = 1f;          // Maximum float (0-1)
    public ItemRarity rarity;
    /// <summary>
    /// Inspector-overridable weapon category. Leave Unknown to auto-infer from weaponName.
    /// </summary>
    public WeaponCategory weaponCategory = WeaponCategory.Unknown;

    // ── Collection ────────────────────────────────────────────────────────────
    /// <summary>
    /// When false (default), collection data is inherited from the parent CaseData.
    /// When true, the fields below override the parent case's collection for this item only.
    /// </summary>
    public bool overrideCollection = false;
    /// <summary>Item-level collection id. Only used when overrideCollection = true.</summary>
    public string collectionId;
    /// <summary>Item-level display name. Only used when overrideCollection = true.</summary>
    public string collectionName;
    /// <summary>Item-level icon. Only used when overrideCollection = true. Runtime-only; not saved to JSON.</summary>
    public Sprite collectionIcon;

    /// <summary>
    /// Returns the collection id, name, and icon to use for this item.
    /// Uses item-level values when overrideCollection is true; otherwise inherits from parentCase.
    /// Safe to call with parentCase = null (falls back to item-level values).
    /// </summary>
    public void GetEffectiveCollection(CaseData parentCase,
                                       out string outId,
                                       out string outName,
                                       out Sprite outIcon)
    {
        if (overrideCollection || parentCase == null)
        {
            outId   = collectionId   ?? string.Empty;
            outName = collectionName ?? string.Empty;
            outIcon = collectionIcon;
        }
        else
        {
            outId   = parentCase.collectionId   ?? string.Empty;
            outName = parentCase.collectionName ?? string.Empty;
            outIcon = parentCase.collectionIcon;
        }
    }

    private static readonly string[] KnifeNameKeywords =
    {
        "bayonet",
        "bowie knife",
        "butterfly knife",
        "classic knife",
        "falchion knife",
        "flip knife",
        "gut knife",
        "huntsman knife",
        "karambit",
        "kukri knife",
        "m9 bayonet",
        "navaja knife",
        "nomad knife",
        "paracord knife",
        "shadow daggers",
        "skeleton knife",
        "stiletto knife",
        "survival knife",
        "talon knife",
        "ursus knife",
        "knife"
    };

    public ItemRarity GetEffectiveRarity()
    {
        if (rarity == ItemRarity.Knife)
            return rarity;

        if (HasKnifeKeyword(weaponName) || HasKnifeKeyword(itemName) || HasKnifeKeyword(skinName))
            return ItemRarity.Knife;

        return rarity;
    }

    private static bool HasKnifeKeyword(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.ToLowerInvariant();
        for (int i = 0; i < KnifeNameKeywords.Length; i++)
        {
            if (normalized.Contains(KnifeNameKeywords[i]))
                return true;
        }

        return false;
    }

    public void GetDisplayNames(out string resolvedWeaponName, out string resolvedSkinName, out string resolvedItemName)
    {
        resolvedItemName = itemName;
        resolvedWeaponName = weaponName;
        resolvedSkinName = skinName;

        if (!string.IsNullOrEmpty(resolvedWeaponName) || !string.IsNullOrEmpty(resolvedSkinName))
            return;

        if (string.IsNullOrEmpty(resolvedItemName))
            return;

        int pipeIndex = resolvedItemName.IndexOf('|');
        if (pipeIndex >= 0)
        {
            resolvedWeaponName = resolvedItemName.Substring(0, pipeIndex).Trim();
            resolvedSkinName = resolvedItemName.Substring(pipeIndex + 1).Trim();
            return;
        }

        resolvedWeaponName = resolvedItemName;
        resolvedSkinName = string.Empty;
    }

    public float GetValueForFloat(float floatValue)
    {
        float min = Mathf.Min(minValue, maxValue);
        float max = Mathf.Max(minValue, maxValue);
        float t = 1f - Mathf.Clamp01(floatValue);
        return Mathf.Lerp(min, max, t);
    }

    /// <summary>
    /// Returns the weapon category for this item. Uses the Inspector override when set,
    /// otherwise infers from weaponName/itemName.
    /// </summary>
    public WeaponCategory GetWeaponCategory()
    {
        if (weaponCategory != WeaponCategory.Unknown)
            return weaponCategory;

        return InferWeaponCategory(weaponName, itemName);
    }

    // ── Static inference lookup ───────────────────────────────────────────────

    private static readonly System.Collections.Generic.Dictionary<string, WeaponCategory> s_WeaponLookup =
        new System.Collections.Generic.Dictionary<string, WeaponCategory>(System.StringComparer.OrdinalIgnoreCase)
    {
        // Pistols
        { "Glock-18",        WeaponCategory.Pistol },
        { "USP-S",           WeaponCategory.Pistol },
        { "P2000",           WeaponCategory.Pistol },
        { "P250",            WeaponCategory.Pistol },
        { "Five-SeveN",      WeaponCategory.Pistol },
        { "Tec-9",           WeaponCategory.Pistol },
        { "CZ75-Auto",       WeaponCategory.Pistol },
        { "Dual Berettas",   WeaponCategory.Pistol },
        { "Desert Eagle",    WeaponCategory.Pistol },
        { "R8 Revolver",     WeaponCategory.Pistol },
        // Rifles
        { "AK-47",           WeaponCategory.Rifle },
        { "M4A4",            WeaponCategory.Rifle },
        { "M4A1-S",          WeaponCategory.Rifle },
        { "FAMAS",           WeaponCategory.Rifle },
        { "Galil AR",        WeaponCategory.Rifle },
        { "AUG",             WeaponCategory.Rifle },
        { "SG 553",          WeaponCategory.Rifle },
        // SMGs
        { "MAC-10",          WeaponCategory.SMG },
        { "MP9",             WeaponCategory.SMG },
        { "MP7",             WeaponCategory.SMG },
        { "MP5-SD",          WeaponCategory.SMG },
        { "UMP-45",          WeaponCategory.SMG },
        { "P90",             WeaponCategory.SMG },
        { "PP-Bizon",        WeaponCategory.SMG },
        // Snipers
        { "AWP",             WeaponCategory.Sniper },
        { "SSG 08",          WeaponCategory.Sniper },
        { "SCAR-20",         WeaponCategory.Sniper },
        { "G3SG1",           WeaponCategory.Sniper },
        // Heavy (Shotguns + LMGs)
        { "Nova",            WeaponCategory.Heavy },
        { "XM1014",          WeaponCategory.Heavy },
        { "MAG-7",           WeaponCategory.Heavy },
        { "Sawed-Off",       WeaponCategory.Heavy },
        { "M249",            WeaponCategory.Heavy },
        { "Negev",           WeaponCategory.Heavy },
        // Knives
        { "Karambit",        WeaponCategory.Knife },
        { "M9 Bayonet",      WeaponCategory.Knife },
        { "Bayonet",         WeaponCategory.Knife },
        { "Butterfly Knife", WeaponCategory.Knife },
        { "Flip Knife",      WeaponCategory.Knife },
        { "Gut Knife",       WeaponCategory.Knife },
        { "Huntsman Knife",  WeaponCategory.Knife },
        { "Falchion Knife",  WeaponCategory.Knife },
        { "Bowie Knife",     WeaponCategory.Knife },
        { "Shadow Daggers",  WeaponCategory.Knife },
        { "Navaja Knife",    WeaponCategory.Knife },
        { "Stiletto Knife",  WeaponCategory.Knife },
        { "Talon Knife",     WeaponCategory.Knife },
        { "Ursus Knife",     WeaponCategory.Knife },
        { "Paracord Knife",  WeaponCategory.Knife },
        { "Survival Knife",  WeaponCategory.Knife },
        { "Nomad Knife",     WeaponCategory.Knife },
        { "Skeleton Knife",  WeaponCategory.Knife },
        { "Kukri Knife",     WeaponCategory.Knife },
        { "Classic Knife",   WeaponCategory.Knife },
        // Gloves
        { "Hand Wraps",           WeaponCategory.Glove },
        { "Driver Gloves",        WeaponCategory.Glove },
        { "Specialist Gloves",    WeaponCategory.Glove },
        { "Sport Gloves",         WeaponCategory.Glove },
        { "Moto Gloves",          WeaponCategory.Glove },
        { "Bloodhound Gloves",    WeaponCategory.Glove },
        { "Hydra Gloves",         WeaponCategory.Glove },
        { "Broken Fang Gloves",   WeaponCategory.Glove },
    };

    /// <summary>
    /// Infers WeaponCategory from a weapon name or item name.
    /// Tries exact lookup first, then substring fallback for gloves/knives.
    /// Returns Unknown for unrecognised weapons.
    /// Also used by SkinInventoryManager to migrate old save entries.
    /// </summary>
    public static WeaponCategory InferWeaponCategory(string weaponName, string itemName = "")
    {
        // Exact match on weaponName
        if (!string.IsNullOrWhiteSpace(weaponName) &&
            s_WeaponLookup.TryGetValue(weaponName.Trim(), out WeaponCategory cat))
            return cat;

        // Exact match on itemName (handles cases where only itemName is populated)
        if (!string.IsNullOrWhiteSpace(itemName) &&
            s_WeaponLookup.TryGetValue(itemName.Trim(), out cat))
            return cat;

        // Substring fallback for gloves and knives (covers modded/custom names)
        string combined = ((weaponName ?? "") + " " + (itemName ?? "")).ToLowerInvariant();
        if (combined.Contains("glove") || combined.Contains("hand wrap"))
            return WeaponCategory.Glove;
        if (combined.Contains("knife") || combined.Contains("bayonet") ||
            combined.Contains("karambit") || combined.Contains("daggers"))
            return WeaponCategory.Knife;

        return WeaponCategory.Unknown;
    }
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
    Contraband,         // Gold (Legacy)
    Knife               // Yellow (Knives)
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

/// <summary>
/// Weapon category used for inventory filtering.
/// Unknown (= 0) is the safe default for old saves; it is inferred on load.
/// </summary>
public enum WeaponCategory
{
    Unknown = 0,
    Pistol,
    Rifle,
    SMG,
    Sniper,
    Knife,
    Glove,
    Heavy   // Shotguns (XM1014, Nova, MAG-7, Sawed-Off) + LMGs (M249, Negev)
}