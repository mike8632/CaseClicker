using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the Momentum/Combo system.
/// Multiplier starts at 1.0x and builds up to max (default 5.0x, upgradeable to 10.0x).
/// Resets to 1.0x after 3 seconds of inactivity.
/// Building max combo takes approximately 20-30 minutes of continuous clicking.
/// </summary>
public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    [Header("Combo Settings")]
    [SerializeField] private float minMultiplier = 1.0f;
    [SerializeField] private float defaultMaxMultiplier = 5.0f;
    [SerializeField] private float absoluteMaxMultiplier = 10.0f;  // Upgraded max
    [SerializeField] private float comboDecayTime = 3.0f;          // Seconds before reset
    [SerializeField] private float timeToMaxCombo = 1500f;         // ~25 minutes to max (in seconds of active clicking)

    [Header("Combo Build Rate")]
    [SerializeField] private float baseComboGainPerClick = 0.01f;  // Adjusted based on timeToMaxCombo

    // Events
    public UnityEvent<float> OnComboChanged;          // Current multiplier
    public UnityEvent<float> OnComboDecayWarning;     // Time remaining before reset
    public UnityEvent OnComboReset;
    public UnityEvent OnComboMaxReached;

    // Current state
    private float currentMultiplier = 1.0f;
    private float currentMaxMultiplier;
    private float timeSinceLastClick = 0f;
    private bool isComboActive = false;
    private bool hasReachedMax = false;

    // Statistics
    private float highestComboReached = 1.0f;
    private int comboResetCount = 0;
    private int maxComboReachedCount = 0;

    #region Properties

    public float CurrentMultiplier => currentMultiplier;
    public float MinMultiplier => minMultiplier;
    public float CurrentMaxMultiplier => currentMaxMultiplier;
    public float AbsoluteMaxMultiplier => absoluteMaxMultiplier;
    public float ComboDecayTime => comboDecayTime;
    public float TimeSinceLastClick => timeSinceLastClick;
    public float TimeUntilReset => Mathf.Max(0, comboDecayTime - timeSinceLastClick);
    public bool IsComboActive => isComboActive;
    public float ComboPercentage => (currentMultiplier - minMultiplier) / (currentMaxMultiplier - minMultiplier);
    public float HighestComboReached => highestComboReached;
    public int ComboResetCount => comboResetCount;

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
        OnComboChanged ??= new UnityEvent<float>();
        OnComboDecayWarning ??= new UnityEvent<float>();
        OnComboReset ??= new UnityEvent();
        OnComboMaxReached ??= new UnityEvent();

        // Set initial max multiplier
        currentMaxMultiplier = defaultMaxMultiplier;
        currentMultiplier = minMultiplier;

        // Calculate combo gain rate based on desired time to max
        // If we want ~1500 seconds of clicking to reach max (25 min), and max is 5.0x (gain of 4.0):
        // We need (maxMultiplier - minMultiplier) / timeToMax = comboGainPerSecond
        // Assuming ~2-3 clicks per second: 4.0 / 1500 / 2.5 = ~0.001 per click
        CalculateComboGainRate();
    }

    private void CalculateComboGainRate()
    {
        // Assuming average click rate of 2-3 clicks per second
        float assumedClicksPerSecond = 2.5f;
        float totalGainNeeded = defaultMaxMultiplier - minMultiplier;
        float totalClicksToMax = timeToMaxCombo * assumedClicksPerSecond;
        baseComboGainPerClick = totalGainNeeded / totalClicksToMax;

        Debug.Log($"[Combo] Gain per click: {baseComboGainPerClick:F6}, Clicks to max: {totalClicksToMax:F0}");
    }

    private void Update()
    {
        if (!isComboActive) return;

        // Track time since last click
        timeSinceLastClick += Time.deltaTime;

        // Decay warning (when less than 1 second remaining)
        if (timeSinceLastClick >= comboDecayTime - 1f && timeSinceLastClick < comboDecayTime)
        {
            OnComboDecayWarning?.Invoke(TimeUntilReset);
        }

        // Check for combo reset
        if (timeSinceLastClick >= comboDecayTime)
        {
            ResetCombo();
        }
    }

    #region Combo Operations

    /// <summary>
    /// Register a click to build combo and reset decay timer.
    /// Called by ClickerController on each click.
    /// </summary>
    public void RegisterClick()
    {
        // Reset decay timer
        timeSinceLastClick = 0f;
        isComboActive = true;
        hasReachedMax = false;

        // Build combo
        float previousMultiplier = currentMultiplier;
        currentMultiplier += baseComboGainPerClick;
        currentMultiplier = Mathf.Clamp(currentMultiplier, minMultiplier, currentMaxMultiplier);

        // Track highest combo
        if (currentMultiplier > highestComboReached)
        {
            highestComboReached = currentMultiplier;
        }

        // Check if max reached
        if (currentMultiplier >= currentMaxMultiplier && !hasReachedMax)
        {
            hasReachedMax = true;
            maxComboReachedCount++;
            OnComboMaxReached?.Invoke();
            Debug.Log($"[Combo] MAX COMBO REACHED: {currentMultiplier:F2}x!");
        }

        // Notify if changed significantly (avoid spamming)
        if (Mathf.Abs(currentMultiplier - previousMultiplier) > 0.001f)
        {
            OnComboChanged?.Invoke(currentMultiplier);
            GameManager.Instance?.OnComboChanged?.Invoke(currentMultiplier);
        }
    }

    /// <summary>
    /// Reset combo to minimum multiplier.
    /// Called automatically after decay time or manually.
    /// </summary>
    public void ResetCombo()
    {
        if (currentMultiplier > minMultiplier)
        {
            Debug.Log($"[Combo] Combo reset! Was: {currentMultiplier:F2}x");
            comboResetCount++;
        }

        currentMultiplier = minMultiplier;
        isComboActive = false;
        timeSinceLastClick = 0f;
        hasReachedMax = false;

        OnComboChanged?.Invoke(currentMultiplier);
        OnComboReset?.Invoke();
        GameManager.Instance?.OnComboChanged?.Invoke(currentMultiplier);
    }

    /// <summary>
    /// Set combo value directly (used for loading save data or bonuses).
    /// </summary>
    public void SetCombo(float multiplier)
    {
        currentMultiplier = Mathf.Clamp(multiplier, minMultiplier, currentMaxMultiplier);
        isComboActive = currentMultiplier > minMultiplier;
        timeSinceLastClick = 0f;
        OnComboChanged?.Invoke(currentMultiplier);
    }

    /// <summary>
    /// Add a bonus to current combo (from power-ups, achievements, etc.).
    /// </summary>
    public void AddComboBonus(float bonusAmount)
    {
        currentMultiplier += bonusAmount;
        currentMultiplier = Mathf.Clamp(currentMultiplier, minMultiplier, currentMaxMultiplier);
        timeSinceLastClick = 0f;
        isComboActive = true;

        OnComboChanged?.Invoke(currentMultiplier);
        Debug.Log($"[Combo] Bonus applied! New combo: {currentMultiplier:F2}x");
    }

    #endregion

    #region Upgrade Methods

    /// <summary>
    /// Upgrade the maximum combo multiplier (towards absolute max of 10.0x).
    /// </summary>
    public void UpgradeMaxMultiplier(float additionalMax)
    {
        currentMaxMultiplier += additionalMax;
        currentMaxMultiplier = Mathf.Clamp(currentMaxMultiplier, defaultMaxMultiplier, absoluteMaxMultiplier);
        Debug.Log($"[Combo] Max multiplier upgraded to: {currentMaxMultiplier:F2}x");
    }

    /// <summary>
    /// Set max multiplier directly (used for loading save data).
    /// </summary>
    public void SetMaxMultiplier(float maxMultiplier)
    {
        currentMaxMultiplier = Mathf.Clamp(maxMultiplier, defaultMaxMultiplier, absoluteMaxMultiplier);
    }

    /// <summary>
    /// Upgrade combo decay time (time before reset).
    /// </summary>
    public void UpgradeDecayTime(float additionalSeconds)
    {
        comboDecayTime += additionalSeconds;
        Debug.Log($"[Combo] Decay time upgraded to: {comboDecayTime:F1}s");
    }

    /// <summary>
    /// Set decay time directly (used for loading save data).
    /// </summary>
    public void SetDecayTime(float decayTime)
    {
        comboDecayTime = Mathf.Max(1f, decayTime);
    }

    /// <summary>
    /// Upgrade combo build rate (faster combo building).
    /// </summary>
    public void UpgradeComboGainRate(float multiplier)
    {
        baseComboGainPerClick *= multiplier;
        Debug.Log($"[Combo] Combo gain rate upgraded to: {baseComboGainPerClick:F6}/click");
    }

    /// <summary>
    /// Set statistics (used for loading save data).
    /// </summary>
    public void SetStatistics(float highestCombo, int resetCount, int maxReachedCount)
    {
        highestComboReached = highestCombo;
        comboResetCount = resetCount;
        maxComboReachedCount = maxReachedCount;
    }

    #endregion
}
