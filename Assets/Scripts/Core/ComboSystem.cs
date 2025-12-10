using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Manages two independent Momentum/Combo systems: one for Money (coin) clicks and one for Case clicks.
/// Each multiplier starts at 1.0x and decays independently.
/// Progress bars: coin requires a number of clicks to gain 0.01x, case requires a different number.
/// </summary>
public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    [Header("Combo Settings")]
    [SerializeField] private float minMultiplier = 1.0f;
    [SerializeField] private float defaultMaxMultiplier = 5.0f;
    [SerializeField] private float absoluteMaxMultiplier = 10.0f;  // Upgraded max
    [SerializeField] private float comboDecayTime = 3.0f;          // Seconds before reset

    [Header("Increment Settings")]
    [Tooltip("Boost added when progress bars complete")]
    [SerializeField] private float boostPerIncrement = 0.01f;
    [Tooltip("Coin clicks required to fill the progress bar once")]
    [SerializeField] private int coinClicksPerIncrement = 17;
    [Tooltip("Case clicks required to fill the progress bar once")]
    [SerializeField] private int caseClicksPerIncrement = 14;

    [Header("Progress Bars (optional)")]
    [Tooltip("Assign the Coin progress Slider here to update it automatically.")]
    public Slider coinProgressBar;
    [Tooltip("Assign the Case progress Slider here to update it automatically.")]
    public Slider caseProgressBar;

    // Events
    public UnityEvent<float> OnCoinComboChanged;       // Current coin multiplier
    public UnityEvent<float> OnCaseComboChanged;       // Current case multiplier
    public UnityEvent<float> OnComboDecayWarning;      // Time remaining before reset (generic)
    public UnityEvent OnCoinComboReset;
    public UnityEvent OnCaseComboReset;
    public UnityEvent OnComboMaxReached;               // Generic notification
    // New: progress bar updates (current clicks, required clicks)
    public UnityEvent<int, int> OnCoinProgressChanged;
    public UnityEvent<int, int> OnCaseProgressChanged;

    // Current state (independent)
    private float coinMultiplier = 1.0f;
    private float caseMultiplier = 1.0f;
    private float currentMaxMultiplier;
    private float coinTimeSinceLastClick = 0f;
    private float caseTimeSinceLastClick = 0f;
    private bool coinComboActive = false;
    private bool caseComboActive = false;
    private bool coinReachedMax = false;
    private bool caseReachedMax = false;

    // Progress counters for bars
    private int coinClicksProgress = 0;
    private int caseClicksProgress = 0;

    // Statistics
    private float highestCoinComboReached = 1.0f;
    private float highestCaseComboReached = 1.0f;
    private int coinComboResetCount = 0;
    private int caseComboResetCount = 0;
    private int maxComboReachedCount = 0;

    #region Properties

    public float CurrentCoinMultiplier => coinMultiplier;
    public float CurrentCaseMultiplier => caseMultiplier;
    // Backward-compat: treat CurrentMultiplier as coin combo
    public float CurrentMultiplier => coinMultiplier;
    public float MinMultiplier => minMultiplier;
    public float CurrentMaxMultiplier => currentMaxMultiplier;
    public float AbsoluteMaxMultiplier => absoluteMaxMultiplier;
    public float ComboDecayTime => comboDecayTime;
    public float CoinTimeUntilReset => Mathf.Max(0, comboDecayTime - coinTimeSinceLastClick);
    public float CaseTimeUntilReset => Mathf.Max(0, comboDecayTime - caseTimeSinceLastClick);
    public bool IsCoinComboActive => coinComboActive;
    public bool IsCaseComboActive => caseComboActive;
    public float HighestCoinComboReached => highestCoinComboReached;
    public float HighestCaseComboReached => highestCaseComboReached;
    public int CoinComboResetCount => coinComboResetCount;
    public int CaseComboResetCount => caseComboResetCount;
    public int CoinClicksProgress => coinClicksProgress;
    public int CaseClicksProgress => caseClicksProgress;

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
        OnCoinComboChanged ??= new UnityEvent<float>();
        OnCaseComboChanged ??= new UnityEvent<float>();
        OnComboDecayWarning ??= new UnityEvent<float>();
        OnCoinComboReset ??= new UnityEvent();
        OnCaseComboReset ??= new UnityEvent();
        OnComboMaxReached ??= new UnityEvent();
        OnCoinProgressChanged ??= new UnityEvent<int, int>();
        OnCaseProgressChanged ??= new UnityEvent<int, int>();

        // Set initial max multiplier
        currentMaxMultiplier = defaultMaxMultiplier;
        coinMultiplier = minMultiplier;
        caseMultiplier = minMultiplier;

        // Init progress bars
        InitBars();

        // Notify initial progress bars
        OnCoinProgressChanged?.Invoke(coinClicksProgress, coinClicksPerIncrement);
        OnCaseProgressChanged?.Invoke(caseClicksProgress, caseClicksPerIncrement);
    }

    private void InitBars()
    {
        if (coinProgressBar != null)
        {
            coinProgressBar.minValue = 0f;
            coinProgressBar.maxValue = coinClicksPerIncrement;
            coinProgressBar.value = coinClicksProgress;
        }
        if (caseProgressBar != null)
        {
            caseProgressBar.minValue = 0f;
            caseProgressBar.maxValue = caseClicksPerIncrement;
            caseProgressBar.value = caseClicksProgress;
        }
    }

    private void Update()
    {
        if (coinComboActive)
        {
            coinTimeSinceLastClick += Time.deltaTime;
            if (coinTimeSinceLastClick >= comboDecayTime - 1f && coinTimeSinceLastClick < comboDecayTime)
            {
                OnComboDecayWarning?.Invoke(CoinTimeUntilReset);
            }
            if (coinTimeSinceLastClick >= comboDecayTime)
            {
                ResetCoinCombo();
            }
        }

        if (caseComboActive)
        {
            caseTimeSinceLastClick += Time.deltaTime;
            if (caseTimeSinceLastClick >= comboDecayTime - 1f && caseTimeSinceLastClick < comboDecayTime)
            {
                OnComboDecayWarning?.Invoke(CaseTimeUntilReset);
            }
            if (caseTimeSinceLastClick >= comboDecayTime)
            {
                ResetCaseCombo();
            }
        }
    }

    #region Combo Operations

    public void RegisterCoinClick()
    {
        coinTimeSinceLastClick = 0f;
        coinComboActive = true;
        coinReachedMax = false;

        // Advance progress bar
        coinClicksProgress = Mathf.Min(coinClicksProgress + 1, coinClicksPerIncrement);
        OnCoinProgressChanged?.Invoke(coinClicksProgress, coinClicksPerIncrement);
        if (coinProgressBar != null)
        {
            coinProgressBar.value = coinClicksProgress;
        }

        if (coinClicksProgress >= coinClicksPerIncrement)
        {
            // Grant boost and reset progress bar
            coinClicksProgress = 0;
            float previous = coinMultiplier;
            coinMultiplier += boostPerIncrement;
            coinMultiplier = Mathf.Clamp(coinMultiplier, minMultiplier, currentMaxMultiplier);

            if (coinMultiplier > highestCoinComboReached)
                highestCoinComboReached = coinMultiplier;

            if (coinMultiplier >= currentMaxMultiplier && !coinReachedMax)
            {
                coinReachedMax = true;
                maxComboReachedCount++;
                OnComboMaxReached?.Invoke();
            }

            if (Mathf.Abs(coinMultiplier - previous) > 0.00001f)
            {
                OnCoinComboChanged?.Invoke(coinMultiplier);
                GameManager.Instance?.OnComboChanged?.Invoke(coinMultiplier);
            }

            // Update progress bar after reset
            OnCoinProgressChanged?.Invoke(coinClicksProgress, coinClicksPerIncrement);
            if (coinProgressBar != null)
            {
                coinProgressBar.value = coinClicksProgress;
            }
        }
    }

    public void RegisterCaseClick()
    {
        caseTimeSinceLastClick = 0f;
        caseComboActive = true;
        caseReachedMax = false;

        // Advance progress bar
        caseClicksProgress = Mathf.Min(caseClicksProgress + 1, caseClicksPerIncrement);
        OnCaseProgressChanged?.Invoke(caseClicksProgress, caseClicksPerIncrement);
        if (caseProgressBar != null)
        {
            caseProgressBar.value = caseClicksProgress;
        }

        if (caseClicksProgress >= caseClicksPerIncrement)
        {
            // Grant boost and reset progress bar
            caseClicksProgress = 0;
            float previous = caseMultiplier;
            caseMultiplier += boostPerIncrement;
            caseMultiplier = Mathf.Clamp(caseMultiplier, minMultiplier, currentMaxMultiplier);

            if (caseMultiplier > highestCaseComboReached)
                highestCaseComboReached = caseMultiplier;

            if (caseMultiplier >= currentMaxMultiplier && !caseReachedMax)
            {
                caseReachedMax = true;
                maxComboReachedCount++;
                OnComboMaxReached?.Invoke();
            }

            if (Mathf.Abs(caseMultiplier - previous) > 0.00001f)
            {
                OnCaseComboChanged?.Invoke(caseMultiplier);
            }

            // Update progress bar after reset
            OnCaseProgressChanged?.Invoke(caseClicksProgress, caseClicksPerIncrement);
            if (caseProgressBar != null)
            {
                caseProgressBar.value = caseClicksProgress;
            }
        }
    }

    public void ResetCoinCombo()
    {
        if (coinMultiplier > minMultiplier)
        {
            coinComboResetCount++;
        }

        coinMultiplier = minMultiplier;
        coinComboActive = false;
        coinTimeSinceLastClick = 0f;
        coinReachedMax = false;
        coinClicksProgress = 0;
        OnCoinProgressChanged?.Invoke(coinClicksProgress, coinClicksPerIncrement);
        if (coinProgressBar != null)
        {
            coinProgressBar.maxValue = coinClicksPerIncrement;
            coinProgressBar.value = coinClicksProgress;
        }

        OnCoinComboChanged?.Invoke(coinMultiplier);
        OnCoinComboReset?.Invoke();
        GameManager.Instance?.OnComboChanged?.Invoke(coinMultiplier);
    }

    public void ResetCaseCombo()
    {
        if (caseMultiplier > minMultiplier)
        {
            caseComboResetCount++;
        }

        caseMultiplier = minMultiplier;
        caseComboActive = false;
        caseTimeSinceLastClick = 0f;
        caseReachedMax = false;
        caseClicksProgress = 0;
        OnCaseProgressChanged?.Invoke(caseClicksProgress, caseClicksPerIncrement);
        if (caseProgressBar != null)
        {
            caseProgressBar.maxValue = caseClicksPerIncrement;
            caseProgressBar.value = caseClicksProgress;
        }

        OnCaseComboChanged?.Invoke(caseMultiplier);
        OnCaseComboReset?.Invoke();
    }

    #endregion

    #region Upgrade Methods

    public void UpgradeMaxMultiplier(float additionalMax)
    {
        currentMaxMultiplier += additionalMax;
        currentMaxMultiplier = Mathf.Clamp(currentMaxMultiplier, defaultMaxMultiplier, absoluteMaxMultiplier);
    }

    public void SetMaxMultiplier(float maxMultiplier)
    {
        currentMaxMultiplier = Mathf.Clamp(maxMultiplier, defaultMaxMultiplier, absoluteMaxMultiplier);
    }

    public void UpgradeDecayTime(float additionalSeconds)
    {
        comboDecayTime += additionalSeconds;
    }

    public void SetDecayTime(float decayTime)
    {
        comboDecayTime = Mathf.Max(1f, decayTime);
    }

    public void UpgradeComboGainRate(float multiplier)
    {
        boostPerIncrement *= multiplier;
    }

    public void SetStatistics(float highestCoinCombo, int coinResetCount, int maxReachedCount)
    {
        highestCoinComboReached = highestCoinCombo;
        coinComboResetCount = coinResetCount;
        maxComboReachedCount = maxReachedCount;
    }

    #endregion
}
