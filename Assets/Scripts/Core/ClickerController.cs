using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Handles all click interactions for Money Coin and Case Coin. 
/// Integrates with ComboSystem for momentum multipliers.
/// </summary>
public class ClickerController : MonoBehaviour
{
    public static ClickerController Instance { get; private set; }

    [Header("Click Settings")]
    [SerializeField] private float baseMoneyPerClick = 1f;
    [SerializeField] private float baseCasePercentPerClick = 0.5f;

    [Header("Critical Click")]
    [Tooltip("Probability (0–1) that a click is a critical hit.")]
    [SerializeField, Range(0f, 1f)] private float criticalClickChance = 0.05f;
    [Tooltip("Multiplier applied to earnings on a critical click.")]
    [SerializeField, Min(1f)] private float criticalClickMultiplier = 3f;

    [Header("UI References (Optional - assign in Inspector)")]
    [SerializeField] private Button moneyCoinButton;
    [SerializeField] private Button caseCoinButton;

    // Events for UI feedback
    public UnityEvent<float> OnMoneyClicked;      // Passes amount earned (post-crit)
    public UnityEvent<float> OnCaseClicked;       // Passes percent gained (post-crit)
    public UnityEvent<Vector3> OnClickFeedback;   // For visual effects at click position
    /// <summary>Fired only when a critical click lands. Passes the total amount earned.</summary>
    public UnityEvent<float> OnCriticalClick;

    // Upgrade multipliers (modified by upgrade system)
    private float moneyClickMultiplier = 1f;
    private float caseClickMultiplier = 1f;

    // Statistics tracking
    private int totalMoneyClicks = 0;
    private int totalCaseClicks = 0;

    #region Properties

    public float BaseMoneyPerClick => baseMoneyPerClick;
    public float BaseCasePercentPerClick => baseCasePercentPerClick;
    public float MoneyClickMultiplier => moneyClickMultiplier;
    public float CaseClickMultiplier => caseClickMultiplier;
    public int TotalMoneyClicks => totalMoneyClicks;
    public int TotalCaseClicks => totalCaseClicks;
    public float CriticalClickChance => criticalClickChance;
    public float CriticalClickMultiplier => criticalClickMultiplier;

    /// <summary>
    /// Current Money Per Click including all multipliers (upgrades + coin combo)
    /// </summary>
    public float CurrentMoneyPerClick
    {
        get
        {
            float comboMultiplier = GameManager.Instance?.Combo?.CurrentCoinMultiplier ?? 1f;
            return baseMoneyPerClick * moneyClickMultiplier * comboMultiplier;
        }
    }

    /// <summary>
    /// Current Case % Per Click including all multipliers (upgrades + case combo)
    /// </summary>
    public float CurrentCasePercentPerClick
    {
        get
        {
            float comboMultiplier = GameManager.Instance?.Combo?.CurrentCaseMultiplier ?? 1f;
            return baseCasePercentPerClick * caseClickMultiplier * comboMultiplier;
        }
    }

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize events if null
        OnMoneyClicked  ??= new UnityEvent<float>();
        OnCaseClicked   ??= new UnityEvent<float>();
        OnClickFeedback ??= new UnityEvent<Vector3>();
        OnCriticalClick ??= new UnityEvent<float>();

        // Ensure minimum starting values (fixes 0 value bug from serialization)
        if (baseMoneyPerClick <= 0f) baseMoneyPerClick = 1f;
        if (baseCasePercentPerClick <= 0f) baseCasePercentPerClick = 0.5f;
        if (moneyClickMultiplier <= 0f) moneyClickMultiplier = 1f;
        if (caseClickMultiplier <= 0f) caseClickMultiplier = 1f;
    }

    private void Start()
    {
        // Setup button listeners if assigned
        if (moneyCoinButton != null)
        {
            moneyCoinButton.onClick.AddListener(OnMoneyCoinClicked);
        }

        if (caseCoinButton != null)
        {
            caseCoinButton.onClick.AddListener(OnCaseCoinClicked);
        }
    }

    #region Click Handlers

    /// <summary>
    /// Called when player clicks the Money Coin. 
    /// Can be called from UI Button or directly from code.
    /// </summary>
    public void OnMoneyCoinClicked()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGamePaused)
            return;

        GameManager.Instance.Combo?.RegisterCoinClick();

        // Calculate earnings with combo multiplier, then apply crit
        float earnings = CurrentMoneyPerClick;
        bool isCrit = Random.value < criticalClickChance;
        if (isCrit) earnings *= criticalClickMultiplier;

        // Add money to balance
        GameManager.Instance.Balance?.AddMoney(earnings);

        // Track statistics
        totalMoneyClicks++;
        GameManager.Instance.Statistics?.RecordMoneyClick(earnings);

        // Trigger events for UI feedback
        OnMoneyClicked?.Invoke(earnings);
        if (isCrit) OnCriticalClick?.Invoke(earnings);
    }

    /// <summary>
    /// Called when player clicks the Case Coin.
    /// Can be called from UI Button or directly from code.
    /// </summary>
    public void OnCaseCoinClicked()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGamePaused)
            return;

        GameManager.Instance.Combo?.RegisterCaseClick();

        // Calculate case progress with combo multiplier, then apply crit
        float progressGain = CurrentCasePercentPerClick;
        bool isCaseCrit = Random.value < criticalClickChance;
        if (isCaseCrit) progressGain *= criticalClickMultiplier;

        // Add progress to case system
        GameManager.Instance.CaseProgress?.AddProgress(progressGain);

        // Track statistics
        totalCaseClicks++;
        GameManager.Instance.Statistics?.RecordCaseClick(progressGain);

        // Trigger events for UI feedback
        OnCaseClicked?.Invoke(progressGain);
        if (isCaseCrit) OnCriticalClick?.Invoke(progressGain);
    }

    /// <summary>
    /// Alternative click handler that accepts world position for visual feedback.
    /// </summary>
    public void OnMoneyCoinClicked(Vector3 clickPosition)
    {
        OnMoneyCoinClicked();
        OnClickFeedback?.Invoke(clickPosition);
    }

    /// <summary>
    /// Alternative click handler that accepts world position for visual feedback.
    /// </summary>
    public void OnCaseCoinClicked(Vector3 clickPosition)
    {
        OnCaseCoinClicked();
        OnClickFeedback?.Invoke(clickPosition);
    }

    #endregion

    #region Upgrade Methods (Called by Upgrade System)

    /// <summary>
    /// Upgrade the base money per click value.
    /// </summary>
    public void UpgradeBaseMoneyPerClick(float additionalAmount)
    {
        baseMoneyPerClick += additionalAmount;
        Debug.Log($"[Clicker] Base Money Per Click upgraded to: ${baseMoneyPerClick:F2}");
    }

    /// <summary>
    /// Upgrade the money click multiplier.
    /// </summary>
    public void UpgradeMoneyClickMultiplier(float additionalMultiplier)
    {
        moneyClickMultiplier += additionalMultiplier;
        Debug.Log($"[Clicker] Money Click Multiplier upgraded to: {moneyClickMultiplier:F2}x");
    }

    /// <summary>
    /// Upgrade the base case % per click value. 
    /// </summary>
    public void UpgradeBaseCasePercentPerClick(float additionalPercent)
    {
        baseCasePercentPerClick += additionalPercent;
        Debug.Log($"[Clicker] Base Case % Per Click upgraded to: {baseCasePercentPerClick:F2}%");
    }

    /// <summary>
    /// Upgrade the case click multiplier.
    /// </summary>
    public void UpgradeCaseClickMultiplier(float additionalMultiplier)
    {
        caseClickMultiplier += additionalMultiplier;
        Debug.Log($"[Clicker] Case Click Multiplier upgraded to: {caseClickMultiplier:F2}x");
    }

    /// <summary>
    /// Set all click values directly (used for loading save data).
    /// </summary>
    public void SetClickValues(float baseMoney, float moneyMultiplier, float baseCase, float caseMultiplier)
    {
        baseMoneyPerClick = baseMoney > 0f ? baseMoney : 1f;
        moneyClickMultiplier = moneyMultiplier > 0f ? moneyMultiplier : 1f;
        baseCasePercentPerClick = baseCase > 0f ? baseCase : 0.5f;
        caseClickMultiplier = caseMultiplier > 0f ? caseMultiplier : 1f;
    }

    /// <summary>
    /// Set click statistics (used for loading save data).
    /// </summary>
    public void SetClickStats(int moneyClicks, int caseClicks)
    {
        totalMoneyClicks = moneyClicks;
        totalCaseClicks = caseClicks;
    }

    #endregion

}