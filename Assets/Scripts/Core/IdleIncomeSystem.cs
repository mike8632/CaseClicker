using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Manages idle/passive income and offline earnings.
/// Handles both Money Per Second (MPS) and Case % Per Second (CPS).
/// Default cap: 60 minutes, upgradeable to 6 hours max.
/// </summary>
public class IdleIncomeSystem : MonoBehaviour
{
    public static IdleIncomeSystem Instance { get; private set; }

    [Header("Idle Time Settings")]
    [SerializeField] private float defaultMaxIdleMinutes = 60f;      // 1 hour default
    [SerializeField] private float absoluteMaxIdleMinutes = 360f;    // 6 hours max with upgrades

    [Header("Offline Calculation")]
    [SerializeField] private float offlineEfficiency = 1.0f;         // Multiplier for offline earnings (can be < 1)

    // Events
    public UnityEvent<double, float> OnOfflineEarningsCollected;     // (money, caseProgress)
    public UnityEvent<float> OnIdleTimeCapChanged;
    public UnityEvent<float, float> OnIdleTick;                      // (mps earned, cps earned) this frame

    // Current settings
    private float currentMaxIdleMinutes;
    private DateTime lastActiveTime;

    // Statistics
    private double totalIdleMoneyEarned = 0;
    private float totalIdleCaseProgressEarned = 0f;
    private float totalIdleTimeSeconds = 0f;

    // Guard: offline earnings may only run after save data has been fully applied
    private bool _saveDataLoaded = false;

    #region Properties

    public float CurrentMaxIdleMinutes => currentMaxIdleMinutes;
    public float CurrentMaxIdleSeconds => currentMaxIdleMinutes * 60f;
    public float AbsoluteMaxIdleMinutes => absoluteMaxIdleMinutes;
    public float OfflineEfficiency => offlineEfficiency;
    public double TotalIdleMoneyEarned => totalIdleMoneyEarned;
    public float TotalIdleCaseProgressEarned => totalIdleCaseProgressEarned;
    public float TotalIdleTimeSeconds => totalIdleTimeSeconds;

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
        OnOfflineEarningsCollected ??= new UnityEvent<double, float>();
        OnIdleTimeCapChanged ??= new UnityEvent<float>();
        OnIdleTick ??= new UnityEvent<float, float>();

        // Set initial max idle time
        currentMaxIdleMinutes = defaultMaxIdleMinutes;

        // Load last active time
        LoadLastActiveTime();
    }

    private void Update()
    {
        // Process idle income each frame
        ProcessIdleIncome(Time.deltaTime);
    }

    #region Idle Income Processing

    /// <summary>
    /// Process idle income each frame (real-time passive income).
    /// </summary>
    private void ProcessIdleIncome(float deltaTime)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGamePaused)
            return;

        // Get current MPS and CPS from respective managers
        float mps = GameManager.Instance.Balance?.CurrentMoneyPerSecond ?? 0f;
        float cps = GameManager.Instance.CaseProgress?.CurrentCasePercentPerSecond ?? 0f;

        if (mps <= 0 && cps <= 0)
            return;

        // Apply idle income
        if (mps > 0)
        {
            GameManager.Instance.Balance?.AddIdleIncome(deltaTime);
            totalIdleMoneyEarned += mps * deltaTime;
        }

        if (cps > 0)
        {
            GameManager.Instance.CaseProgress?.AddIdleProgress(deltaTime);
            totalIdleCaseProgressEarned += cps * deltaTime;
        }

        totalIdleTimeSeconds += deltaTime;

        // Trigger event for UI updates
        OnIdleTick?.Invoke(mps * deltaTime, cps * deltaTime);
    }

    #endregion

    #region Offline Earnings

    /// <summary>
    /// Called by GameManager after save data is fully applied.
    /// Offline earnings will not run until this is called.
    /// </summary>
    public void NotifySaveDataLoaded()
    {
        _saveDataLoaded = true;
    }

    /// <summary>
    /// Calculate and award earnings accumulated while offline.
    /// Safe to call multiple times — only runs after save data is loaded,
    /// and resets the clock after awarding to prevent double-awards.
    /// </summary>
    public void CalculateAndAwardOfflineEarnings()
    {
        if (!_saveDataLoaded)
        {
            Debug.Log("[IdleIncome] Skipping offline earnings — save data not yet loaded.");
            return;
        }

        if (lastActiveTime == DateTime.MinValue)
        {
            return;
        }

        TimeSpan timeSinceLastActive = DateTime.Now - lastActiveTime;
        float offlineSeconds = (float)timeSinceLastActive.TotalSeconds;

        // Cap offline time
        float maxOfflineSeconds = CurrentMaxIdleSeconds;
        float effectiveOfflineSeconds = Mathf.Min(offlineSeconds, maxOfflineSeconds);

        if (effectiveOfflineSeconds < 1f)
        {
            return;
        }

        // Calculate earnings
        float mps = GameManager.Instance?.Balance?.CurrentMoneyPerSecond ?? 0f;
        float cps = GameManager.Instance?.CaseProgress?.CurrentCasePercentPerSecond ?? 0f;

        double offlineMoney = mps * effectiveOfflineSeconds * offlineEfficiency;
        float offlineCaseProgress = cps * effectiveOfflineSeconds * offlineEfficiency;

        // Award earnings
        if (offlineMoney > 0)
        {
            GameManager.Instance?.Balance?.AddMoney(offlineMoney);
            totalIdleMoneyEarned += offlineMoney;
        }

        if (offlineCaseProgress > 0)
        {
            GameManager.Instance?.CaseProgress?.AddProgress(offlineCaseProgress);
            totalIdleCaseProgressEarned += offlineCaseProgress;
        }

        totalIdleTimeSeconds += effectiveOfflineSeconds;

        Debug.Log($"[IdleIncome] Offline for {FormatTime(effectiveOfflineSeconds)} " +
                  $"(capped from {FormatTime(offlineSeconds)}). " +
                  $"Earned: ${offlineMoney:F2}, {offlineCaseProgress:F2}% case progress");

        // Reset the clock so a second call this session cannot re-award the same period
        SaveLastActiveTime();

        // Trigger event for UI popup
        OnOfflineEarningsCollected?.Invoke(offlineMoney, offlineCaseProgress);
    }

    /// <summary>
    /// Get offline earnings without awarding them (for preview).
    /// </summary>
    public (double money, float caseProgress, float timeSeconds) PreviewOfflineEarnings()
    {
        if (lastActiveTime == DateTime.MinValue)
            return (0, 0, 0);

        TimeSpan timeSinceLastActive = DateTime.Now - lastActiveTime;
        float offlineSeconds = (float)timeSinceLastActive.TotalSeconds;
        float effectiveOfflineSeconds = Mathf.Min(offlineSeconds, CurrentMaxIdleSeconds);

        float mps = GameManager.Instance?.Balance?.CurrentMoneyPerSecond ?? 0f;
        float cps = GameManager.Instance?.CaseProgress?.CurrentCasePercentPerSecond ?? 0f;

        double offlineMoney = mps * effectiveOfflineSeconds * offlineEfficiency;
        float offlineCaseProgress = cps * effectiveOfflineSeconds * offlineEfficiency;

        return (offlineMoney, offlineCaseProgress, effectiveOfflineSeconds);
    }

    #endregion

    #region Time Management

    /// <summary>
    /// Save the current time as last active time.
    /// </summary>
    public void SaveLastActiveTime()
    {
        lastActiveTime = DateTime.Now;
        PlayerPrefs.SetString("LastActiveTime", lastActiveTime.ToBinary().ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load the last active time from storage.
    /// </summary>
    private void LoadLastActiveTime()
    {
        string savedTime = PlayerPrefs.GetString("LastActiveTime", "");

        if (!string.IsNullOrEmpty(savedTime) && long.TryParse(savedTime, out long binaryTime))
        {
            lastActiveTime = DateTime.FromBinary(binaryTime);
        }
        else
        {
            lastActiveTime = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Format seconds into a readable time string.
    /// </summary>
    public static string FormatTime(float totalSeconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(totalSeconds);

        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        else if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";
        else
            return $"{time.Seconds}s";
    }

    #endregion

    #region Upgrade Methods

    /// <summary>
    /// Upgrade maximum idle time cap.
    /// </summary>
    public void UpgradeMaxIdleTime(float additionalMinutes)
    {
        currentMaxIdleMinutes += additionalMinutes;
        currentMaxIdleMinutes = Mathf.Clamp(currentMaxIdleMinutes, defaultMaxIdleMinutes, absoluteMaxIdleMinutes);
        OnIdleTimeCapChanged?.Invoke(currentMaxIdleMinutes);
        Debug.Log($"[IdleIncome] Max idle time upgraded to: {currentMaxIdleMinutes} minutes");
    }

    /// <summary>
    /// Set max idle time directly (used for loading save data).
    /// </summary>
    public void SetMaxIdleTime(float minutes)
    {
        currentMaxIdleMinutes = Mathf.Clamp(minutes, defaultMaxIdleMinutes, absoluteMaxIdleMinutes);
        OnIdleTimeCapChanged?.Invoke(currentMaxIdleMinutes);
    }

    /// <summary>
    /// Upgrade offline efficiency multiplier.
    /// </summary>
    public void UpgradeOfflineEfficiency(float additionalEfficiency)
    {
        offlineEfficiency += additionalEfficiency;
        offlineEfficiency = Mathf.Clamp(offlineEfficiency, 0.1f, 2.0f);
        Debug.Log($"[IdleIncome] Offline efficiency upgraded to: {offlineEfficiency:P0}");
    }

    /// <summary>
    /// Set offline efficiency directly (used for loading save data).
    /// </summary>
    public void SetOfflineEfficiency(float efficiency)
    {
        offlineEfficiency = Mathf.Clamp(efficiency, 0.1f, 2.0f);
    }

    /// <summary>
    /// Set statistics (used for loading save data).
    /// </summary>
    public void SetStatistics(double idleMoney, float idleCaseProgress, float idleTime)
    {
        totalIdleMoneyEarned = idleMoney;
        totalIdleCaseProgressEarned = idleCaseProgress;
        totalIdleTimeSeconds = idleTime;
    }

    #endregion
}
