using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages two independent Momentum/Combo systems: one for Money (coin) clicks and one for Case clicks.
/// Each multiplier starts at 1.0x and decays independently.
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
    public UnityEvent<float> OnCoinComboChanged;       // Current coin multiplier
    public UnityEvent<float> OnCaseComboChanged;       // Current case multiplier
    public UnityEvent<float> OnComboDecayWarning;      // Time remaining before reset (generic)
    public UnityEvent OnCoinComboReset;
    public UnityEvent OnCaseComboReset;
    public UnityEvent OnComboMaxReached;               // Generic notification

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

        // Set initial max multiplier
        currentMaxMultiplier = defaultMaxMultiplier;
        coinMultiplier = minMultiplier;
        caseMultiplier = minMultiplier;

        CalculateComboGainRate();
    }

    private void CalculateComboGainRate()
    {
        float assumedClicksPerSecond = 2.5f;
        float totalGainNeeded = defaultMaxMultiplier - minMultiplier;
        float totalClicksToMax = timeToMaxCombo * assumedClicksPerSecond;
        baseComboGainPerClick = totalGainNeeded / totalClicksToMax;
        Debug.Log($"[Combo] Gain per click: {baseComboGainPerClick:F6}, Clicks to max: {totalClicksToMax:F0}");
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

        float previous = coinMultiplier;
        coinMultiplier += baseComboGainPerClick;
        coinMultiplier = Mathf.Clamp(coinMultiplier, minMultiplier, currentMaxMultiplier);

        if (coinMultiplier > highestCoinComboReached)
            highestCoinComboReached = coinMultiplier;

        if (coinMultiplier >= currentMaxMultiplier && !coinReachedMax)
        {
            coinReachedMax = true;
            maxComboReachedCount++;
            OnComboMaxReached?.Invoke();
            Debug.Log($"[Combo] COIN MAX COMBO REACHED: {coinMultiplier:F2}x!");
        }

        if (Mathf.Abs(coinMultiplier - previous) > 0.001f)
        {
            OnCoinComboChanged?.Invoke(coinMultiplier);
            GameManager.Instance?.OnComboChanged?.Invoke(coinMultiplier);
        }
    }

    public void RegisterCaseClick()
    {
        caseTimeSinceLastClick = 0f;
        caseComboActive = true;
        caseReachedMax = false;

        float previous = caseMultiplier;
        caseMultiplier += baseComboGainPerClick;
        caseMultiplier = Mathf.Clamp(caseMultiplier, minMultiplier, currentMaxMultiplier);

        if (caseMultiplier > highestCaseComboReached)
            highestCaseComboReached = caseMultiplier;

        if (caseMultiplier >= currentMaxMultiplier && !caseReachedMax)
        {
            caseReachedMax = true;
            maxComboReachedCount++;
            OnComboMaxReached?.Invoke();
            Debug.Log($"[Combo] CASE MAX COMBO REACHED: {caseMultiplier:F2}x!");
        }

        if (Mathf.Abs(caseMultiplier - previous) > 0.001f)
        {
            OnCaseComboChanged?.Invoke(caseMultiplier);
        }
    }

    public void ResetCoinCombo()
    {
        if (coinMultiplier > minMultiplier)
        {
            Debug.Log($"[Combo] Coin combo reset! Was: {coinMultiplier:F2}x");
            coinComboResetCount++;
        }

        coinMultiplier = minMultiplier;
        coinComboActive = false;
        coinTimeSinceLastClick = 0f;
        coinReachedMax = false;

        OnCoinComboChanged?.Invoke(coinMultiplier);
        OnCoinComboReset?.Invoke();
        GameManager.Instance?.OnComboChanged?.Invoke(coinMultiplier);
    }

    public void ResetCaseCombo()
    {
        if (caseMultiplier > minMultiplier)
        {
            Debug.Log($"[Combo] Case combo reset! Was: {caseMultiplier:F2}x");
            caseComboResetCount++;
        }

        caseMultiplier = minMultiplier;
        caseComboActive = false;
        caseTimeSinceLastClick = 0f;
        caseReachedMax = false;

        OnCaseComboChanged?.Invoke(caseMultiplier);
        OnCaseComboReset?.Invoke();
    }

    #endregion

    #region Upgrade Methods

    public void UpgradeMaxMultiplier(float additionalMax)
    {
        currentMaxMultiplier += additionalMax;
        currentMaxMultiplier = Mathf.Clamp(currentMaxMultiplier, defaultMaxMultiplier, absoluteMaxMultiplier);
        Debug.Log($"[Combo] Max multiplier upgraded to: {currentMaxMultiplier:F2}x");
    }

    public void SetMaxMultiplier(float maxMultiplier)
    {
        currentMaxMultiplier = Mathf.Clamp(maxMultiplier, defaultMaxMultiplier, absoluteMaxMultiplier);
    }

    public void UpgradeDecayTime(float additionalSeconds)
    {
        comboDecayTime += additionalSeconds;
        Debug.Log($"[Combo] Decay time upgraded to: {comboDecayTime:F1}s");
    }

    public void SetDecayTime(float decayTime)
    {
        comboDecayTime = Mathf.Max(1f, decayTime);
    }

    public void UpgradeComboGainRate(float multiplier)
    {
        baseComboGainPerClick *= multiplier;
        Debug.Log($"[Combo] Combo gain rate upgraded to: {baseComboGainPerClick:F6}/click");
    }

    public void SetStatistics(float highestCoinCombo, int coinResetCount, int maxReachedCount)
    {
        highestCoinComboReached = highestCoinCombo;
        coinComboResetCount = coinResetCount;
        maxComboReachedCount = maxReachedCount;
    }

    #endregion
}
