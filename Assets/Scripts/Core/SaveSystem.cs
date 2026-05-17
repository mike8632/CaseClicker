using UnityEngine;
using UnityEngine.Events;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Handles saving and loading all game data.
/// Uses JSON serialization with PlayerPrefs fallback.
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private float autoSaveIntervalSeconds = 60f;
    [SerializeField] private bool useJsonFile = true;  // If false, uses PlayerPrefs only

    // Events
    public UnityEvent OnSaveStarted;
    public UnityEvent OnSaveCompleted;
    public UnityEvent OnLoadStarted;
    public UnityEvent OnLoadCompleted;
    public UnityEvent<string> OnSaveError;
    public UnityEvent<string> OnLoadError;

    // Save file path
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "CaseClickerSave.json");

    // Auto-save timer
    private float autoSaveTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize events
        OnSaveStarted ??= new UnityEvent();
        OnSaveCompleted ??= new UnityEvent();
        OnLoadStarted ??= new UnityEvent();
        OnLoadCompleted ??= new UnityEvent();
        OnSaveError ??= new UnityEvent<string>();
        OnLoadError ??= new UnityEvent<string>();
    }

    private void Start()
    {
        // Loading is handled by GameManager to ensure proper initialization order
        // LoadGame() is called manually from GameManager. Start()
    }

    private void Update()
    {
        // Auto-save timer
        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer >= autoSaveIntervalSeconds)
        {
            autoSaveTimer = 0f;
            SaveGame();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGame();
        }
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    #region Save Operations

    /// <summary>
    /// Save all game data. 
    /// </summary>
    public void SaveGame()
    {
        try
        {
            OnSaveStarted?.Invoke();

            SaveData data = CollectSaveData();

            if (useJsonFile)
            {
                SaveToJson(data);
            }
            else
            {
                SaveToPlayerPrefs(data);
            }

            OnSaveCompleted?.Invoke();
            Debug.Log($"[SaveSystem] Game saved successfully at {DateTime.Now}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
            OnSaveError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Collect all data to save from game systems.
    /// </summary>
    private SaveData CollectSaveData()
    {
        SaveData data = new SaveData();
        data.saveTimestamp = DateTime.Now.ToBinary();
        data.gameVersion = Application.version;

        // Balance
        if (BalanceManager.Instance != null)
        {
            data.currentMoney = BalanceManager.Instance.CurrentMoney;
            data.totalMoneyEarned = BalanceManager.Instance.TotalMoneyEarned;
            data.totalMoneySpent = BalanceManager.Instance.TotalMoneySpent;
            data.baseMoneyPerSecond = BalanceManager.Instance.BaseMoneyPerSecond;
            data.moneyPerSecondMultiplier = BalanceManager.Instance.MoneyPerSecondMultiplier;
        }

        // Clicker
        if (ClickerController.Instance != null)
        {
            data.baseMoneyPerClick = ClickerController.Instance.BaseMoneyPerClick;
            data.moneyClickMultiplier = ClickerController.Instance.MoneyClickMultiplier;
            data.baseCasePercentPerClick = ClickerController.Instance.BaseCasePercentPerClick;
            data.caseClickMultiplier = ClickerController.Instance.CaseClickMultiplier;
            data.totalMoneyClicks = ClickerController.Instance.TotalMoneyClicks;
            data.totalCaseClicks = ClickerController.Instance.TotalCaseClicks;
        }

        // Case progress
        if (CaseProgressManager.Instance != null)
        {
            data.currentCaseProgress = CaseProgressManager.Instance.CurrentProgress;
            data.totalCasesDropped = CaseProgressManager.Instance.TotalCasesDropped;
            data.totalCaseProgressEarned = CaseProgressManager.Instance.TotalProgressEarned;
            data.baseCasePercentPerSecond = CaseProgressManager.Instance.BaseCasePercentPerSecond;
            data.casePerSecondMultiplier = CaseProgressManager.Instance.CasePerSecondMultiplier;
        }

        // Combo
        if (ComboSystem.Instance != null)
        {
            data.currentMaxComboMultiplier = ComboSystem.Instance.CurrentMaxMultiplier;
            data.comboDecayTime = ComboSystem.Instance.ComboDecayTime;
            data.highestComboReached = ComboSystem.Instance.HighestCoinComboReached;
            data.comboResetCount = ComboSystem.Instance.CoinComboResetCount;
        }

        // Idle income
        if (IdleIncomeSystem.Instance != null)
        {
            data.maxIdleMinutes = IdleIncomeSystem.Instance.CurrentMaxIdleMinutes;
            data.offlineEfficiency = IdleIncomeSystem.Instance.OfflineEfficiency;
            data.totalIdleMoneyEarned = IdleIncomeSystem.Instance.TotalIdleMoneyEarned;
            data.totalIdleCaseProgressEarned = IdleIncomeSystem.Instance.TotalIdleCaseProgressEarned;
            data.totalIdleTimeSeconds = IdleIncomeSystem.Instance.TotalIdleTimeSeconds;
        }

        // Statistics
        if (StatisticsManager.Instance != null)
        {
            data.statistics = StatisticsManager.Instance.GetAllStatistics();
        }

        // Case inventory
        if (CaseInventoryManager.Instance != null)
        {
            data.caseInventory = CaseInventoryManager.Instance.GetSnapshot();
        }

        // Skin inventory
        if (SkinInventoryManager.Instance != null)
        {
            data.skinInventory = SkinInventoryManager.Instance.GetSnapshot();
        }

        return data;
    }

    /// <summary>
    /// Save data to JSON file.
    /// </summary>
    private void SaveToJson(SaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SaveFilePath, json);
        Debug.Log($"[SaveSystem] Saved to: {SaveFilePath}");
    }

    /// <summary>
    /// Save data to PlayerPrefs. 
    /// </summary>
    private void SaveToPlayerPrefs(SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("CaseClickerSaveData", json);
        PlayerPrefs.Save();
    }

    #endregion

    #region Load Operations

    /// <summary>
    /// Load all game data.
    /// </summary>
    public void LoadGame()
    {
        try
        {
            OnLoadStarted?.Invoke();

            SaveData data = null;

            if (useJsonFile && File.Exists(SaveFilePath))
            {
                data = LoadFromJson();
            }
            else if (PlayerPrefs.HasKey("CaseClickerSaveData"))
            {
                data = LoadFromPlayerPrefs();
            }

            if (data != null)
            {
                // Validate save data before applying (fix for 0 values)
                ValidateSaveData(data);
                ApplySaveData(data);
                Debug.Log($"[SaveSystem] Game loaded successfully.  Save from: {DateTime.FromBinary(data.saveTimestamp)}");
            }
            else
            {
                Debug.Log("[SaveSystem] No save data found. Starting fresh.");
            }

            OnLoadCompleted?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
            OnLoadError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Load data from JSON file. 
    /// </summary>
    private SaveData LoadFromJson()
    {
        string json = File.ReadAllText(SaveFilePath);
        return JsonUtility.FromJson<SaveData>(json);
    }

    /// <summary>
    /// Load data from PlayerPrefs.
    /// </summary>
    private SaveData LoadFromPlayerPrefs()
    {
        string json = PlayerPrefs.GetString("CaseClickerSaveData");
        return JsonUtility.FromJson<SaveData>(json);
    }

    /// <summary>
    /// Validate and fix any 0 or invalid values in save data. 
    /// </summary>
    private void ValidateSaveData(SaveData data)
    {
        // Fix clicker values
        if (data.baseMoneyPerClick <= 0f) data.baseMoneyPerClick = 1f;
        if (data.moneyClickMultiplier <= 0f) data.moneyClickMultiplier = 1f;
        if (data.baseCasePercentPerClick <= 0f) data.baseCasePercentPerClick = 0.5f;
        if (data.caseClickMultiplier <= 0f) data.caseClickMultiplier = 1f;

        // Fix idle income multipliers
        if (data.moneyPerSecondMultiplier <= 0f) data.moneyPerSecondMultiplier = 1f;
        if (data.casePerSecondMultiplier <= 0f) data.casePerSecondMultiplier = 1f;

        // Fix combo values
        if (data.currentMaxComboMultiplier <= 0f) data.currentMaxComboMultiplier = 5f;
        if (data.comboDecayTime <= 0f) data.comboDecayTime = 3f;

        // Fix idle time settings
        if (data.maxIdleMinutes <= 0f) data.maxIdleMinutes = 60f;
        if (data.offlineEfficiency <= 0f) data.offlineEfficiency = 1f;

        Debug.Log("[SaveSystem] Save data validated and corrected if needed.");
    }

    /// <summary>
    /// Apply loaded data to all game systems.
    /// </summary>
    private void ApplySaveData(SaveData data)
    {
        // Apply to Balance
        if (BalanceManager.Instance != null)
        {
            BalanceManager.Instance.SetMoney(data.currentMoney);
            BalanceManager.Instance.SetStatistics(data.totalMoneyEarned, data.totalMoneySpent);
            BalanceManager.Instance.SetIdleValues(data.baseMoneyPerSecond, data.moneyPerSecondMultiplier);
        }

        // Apply to Clicker
        if (ClickerController.Instance != null)
        {
            ClickerController.Instance.SetClickValues(
                data.baseMoneyPerClick,
                data.moneyClickMultiplier,
                data.baseCasePercentPerClick,
                data.caseClickMultiplier
            );
            ClickerController.Instance.SetClickStats(data.totalMoneyClicks, data.totalCaseClicks);
        }

        // Apply to Case Progress
        if (CaseProgressManager.Instance != null)
        {
            CaseProgressManager.Instance.SetProgress(data.currentCaseProgress);
            CaseProgressManager.Instance.SetStatistics(data.totalCasesDropped, data.totalCaseProgressEarned);
            CaseProgressManager.Instance.SetIdleValues(data.baseCasePercentPerSecond, data.casePerSecondMultiplier);
        }

        // Apply to Combo (using coin combo stats)
        if (ComboSystem.Instance != null)
        {
            ComboSystem.Instance.SetMaxMultiplier(data.currentMaxComboMultiplier);
            ComboSystem.Instance.SetDecayTime(data.comboDecayTime);
            ComboSystem.Instance.SetStatistics(data.highestComboReached, data.comboResetCount, 0);
        }

        // Apply to Idle Income
        if (IdleIncomeSystem.Instance != null)
        {
            IdleIncomeSystem.Instance.SetMaxIdleTime(data.maxIdleMinutes);
            IdleIncomeSystem.Instance.SetOfflineEfficiency(data.offlineEfficiency);
            IdleIncomeSystem.Instance.SetStatistics(
                data.totalIdleMoneyEarned,
                data.totalIdleCaseProgressEarned,
                data.totalIdleTimeSeconds
            );
        }

        // Apply Statistics
        if (StatisticsManager.Instance != null && data.statistics != null)
        {
            StatisticsManager.Instance.SetAllStatistics(data.statistics);
        }

        // Case inventory
        if (CaseInventoryManager.Instance != null && data.caseInventory != null)
        {
            ApplyCaseInventory(data.caseInventory);
        }

        // Skin inventory
        if (SkinInventoryManager.Instance != null && data.skinInventory != null)
        {
            SkinInventoryManager.Instance.ApplySnapshot(data.skinInventory);
        }
    }

    private void ApplyCaseInventory(List<CaseEntryDTO> list)
    {
        if (list == null) return;
        // Clear existing counts/keys to avoid stacking, but preserve unlock flags for cases not in save
        var current = CaseInventoryManager.Instance.GetSnapshot();
        foreach (var dto in current)
        {
            if (!string.IsNullOrEmpty(dto.caseId))
            {
                int c = CaseInventoryManager.Instance.GetCaseCount(dto.caseId);
                if (c > 0) CaseInventoryManager.Instance.RemoveCases(dto.caseId, c);
                int k = CaseInventoryManager.Instance.GetKeyCount(dto.caseId);
                if (k > 0) CaseInventoryManager.Instance.RemoveKeys(dto.caseId, k);
                // Preserve unlock state; do not force lock here
            }
        }
        // Apply loaded entries
        foreach (var dto in list)
        {
            if (string.IsNullOrEmpty(dto.caseId)) continue;
            CaseInventoryManager.Instance.SetUnlocked(dto.caseId, dto.unlocked);
            if (dto.ownedCount > 0) CaseInventoryManager.Instance.AddCases(dto.caseId, dto.ownedCount);
            if (dto.ownedKeys > 0) CaseInventoryManager.Instance.AddKeys(dto.caseId, dto.ownedKeys);
            if (dto.keyPrice > 0f) CaseInventoryManager.Instance.SetKeyPrice(dto.caseId, dto.keyPrice);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Delete all save data (reset game).
    /// </summary>
    public void DeleteSaveData()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
        }

        PlayerPrefs.DeleteKey("CaseClickerSaveData");
        PlayerPrefs.DeleteKey("LastActiveTime");
        PlayerPrefs.Save();

        Debug.Log("[SaveSystem] All save data deleted.");
    }

    /// <summary>
    /// Check if save data exists.
    /// </summary>
    public bool HasSaveData()
    {
        return File.Exists(SaveFilePath) || PlayerPrefs.HasKey("CaseClickerSaveData");
    }

    /// <summary>
    /// Get the save file size in bytes.
    /// </summary>
    public long GetSaveFileSize()
    {
        if (File.Exists(SaveFilePath))
        {
            return new FileInfo(SaveFilePath).Length;
        }
        return 0;
    }

    /// <summary>
    /// Export save data as string (for cloud save or sharing).
    /// </summary>
    public string ExportSaveData()
    {
        SaveData data = CollectSaveData();
        return JsonUtility.ToJson(data);
    }

    /// <summary>
    /// Import save data from string. 
    /// </summary>
    public bool ImportSaveData(string json)
    {
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            ValidateSaveData(data);
            ApplySaveData(data);
            SaveGame();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Import failed: {e.Message}");
            return false;
        }
    }

    #endregion
}

/// <summary>
/// Container for all saveable game data. 
/// </summary>
[Serializable]
public class SaveData
{
    // Meta data
    public long saveTimestamp;
    public string gameVersion;

    // Balance
    public double currentMoney;
    public double totalMoneyEarned;
    public double totalMoneySpent;
    public float baseMoneyPerSecond;
    public float moneyPerSecondMultiplier = 1f;

    // Clicker
    public float baseMoneyPerClick = 1f;
    public float moneyClickMultiplier = 1f;
    public float baseCasePercentPerClick = 0.5f;
    public float caseClickMultiplier = 1f;
    public int totalMoneyClicks;
    public int totalCaseClicks;

    // Case Progress
    public float currentCaseProgress;
    public int totalCasesDropped;
    public float totalCaseProgressEarned;
    public float baseCasePercentPerSecond;
    public float casePerSecondMultiplier = 1f;

    // Combo (coin combo stats stored for backward compatibility)
    public float currentMaxComboMultiplier = 5f;
    public float comboDecayTime = 3f;
    public float highestComboReached = 1f;
    public int comboResetCount;

    // Idle Income
    public float maxIdleMinutes = 60f;
    public float offlineEfficiency = 1f;
    public double totalIdleMoneyEarned;
    public float totalIdleCaseProgressEarned;
    public float totalIdleTimeSeconds;

    // Statistics (serialized separately)
    public GameStatistics statistics;

    // Case inventory
    public System.Collections.Generic.List<CaseEntryDTO> caseInventory;

    // Skin inventory
    public System.Collections.Generic.List<SkinEntryDTO> skinInventory;
}

/// <summary>
/// Serializable DTO for case entries in inventory.
/// </summary>
[Serializable]
public class CaseEntryDTO
{
    public string caseId;
    public int ownedCount;
    public int ownedKeys;
    public bool unlocked;
    public float keyPrice;
    public float casePriceOverride;
    public float caseSellPriceOverride;
}

/// <summary>
/// Serializable DTO for skin inventory entries.
/// </summary>
[Serializable]
public class SkinEntryDTO
{
    public string instanceId;
    public string sourceCaseId;
    public string itemId;
    public string itemName;
    public string weaponName;
    public string skinName;
    public ItemRarity rarity;
    public ItemWear wear;
    public bool isStatTrak;
    public float marketValue;
    public float floatValue;
}