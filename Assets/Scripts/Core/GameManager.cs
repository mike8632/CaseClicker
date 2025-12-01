using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Central game manager - singleton that coordinates all game systems.
/// Handles game state, events, and system initialization.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private bool isGamePaused = false;

    // Core system references
    public BalanceManager Balance { get; private set; }
    public CaseProgressManager CaseProgress { get; private set; }
    public ComboSystem Combo { get; private set; }
    public IdleIncomeSystem IdleIncome { get; private set; }
    public StatisticsManager Statistics { get; private set; }
    public SaveSystem SaveSystem { get; private set; }

    // Game Events (UnityEvents for inspector hookup and script access)
    public UnityEvent OnGameInitialized;
    public UnityEvent OnGamePaused;
    public UnityEvent OnGameResumed;
    public UnityEvent<double> OnMoneyChanged;
    public UnityEvent<float> OnCaseProgressChanged;
    public UnityEvent<CaseData> OnCaseDropped;
    public UnityEvent<float> OnComboChanged;

    // Properties
    public bool IsGamePaused => isGamePaused;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSystems();
    }

    private void InitializeSystems()
    {
        // Initialize events
        OnGameInitialized ??= new UnityEvent();
        OnGamePaused ??= new UnityEvent();
        OnGameResumed ??= new UnityEvent();
        OnMoneyChanged ??= new UnityEvent<double>();
        OnCaseProgressChanged ??= new UnityEvent<float>();
        OnCaseDropped ??= new UnityEvent<CaseData>();
        OnComboChanged ??= new UnityEvent<float>();

        // Get or create core systems
        Balance = GetOrAddComponent<BalanceManager>();
        CaseProgress = GetOrAddComponent<CaseProgressManager>();
        Combo = GetOrAddComponent<ComboSystem>();
        IdleIncome = GetOrAddComponent<IdleIncomeSystem>();
        Statistics = GetOrAddComponent<StatisticsManager>();
        SaveSystem = GetOrAddComponent<SaveSystem>();

        Debug.Log("[GameManager] All systems initialized");
    }

    private void Start()
    {
        // Load saved data
        SaveSystem.LoadGame();

        // Calculate offline income (handled in IdleIncomeSystem.Start)

        OnGameInitialized?.Invoke();
        Debug.Log("[GameManager] Game started");
    }

    private T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }
        return component;
    }

    #region Event Triggers (called by other systems)

    public void TriggerMoneyChanged(double newAmount)
    {
        OnMoneyChanged?.Invoke(newAmount);
    }

    public void TriggerCaseProgressChanged(float progress)
    {
        OnCaseProgressChanged?.Invoke(progress);
    }

    public void TriggerCaseDropped(CaseData caseData)
    {
        OnCaseDropped?.Invoke(caseData);
    }

    public void TriggerComboChanged(float multiplier)
    {
        OnComboChanged?.Invoke(multiplier);
    }

    #endregion

    #region Game State Management

    public void PauseGame()
    {
        if (!isGamePaused)
        {
            isGamePaused = true;
            Time.timeScale = 0f;
            OnGamePaused?.Invoke();
        }
    }

    public void ResumeGame()
    {
        if (isGamePaused)
        {
            isGamePaused = false;
            Time.timeScale = 1f;
            OnGameResumed?.Invoke();
        }
    }

    #endregion

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Game is being paused/minimized - save
            SaveSystem?.SaveGame();
            IdleIncome?.SaveLastActiveTime();
        }
        else
        {
            // Game is being resumed
            IdleIncome?.CalculateAndAwardOfflineEarnings();
        }
    }

    private void OnApplicationQuit()
    {
        SaveSystem?.SaveGame();
        IdleIncome?.SaveLastActiveTime();
    }
}
