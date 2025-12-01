using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// UI Controller for Case Progression display. 
/// Shows progress bar, percentage text, and case drop animations.
/// </summary>
public class CaseProgressionUI : MonoBehaviour
{
    [Header("Progress Bar")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image progressFillImage;
    [SerializeField] private Gradient progressGradient;  // Optional: color changes as progress increases

    [Header("Text Displays")]
    [SerializeField] private Text progressPercentText;
    [SerializeField] private Text casePerSecondText;
    [SerializeField] private Text casesDroppedText;
    [SerializeField] private Text clickProgressText;  // Shows progress per click

    [Header("Text Formats")]
    [SerializeField] private string percentFormat = "{0:F1}%";
    [SerializeField] private string cpsFormat = "{0:F2}%/s";
    [SerializeField] private string casesDroppedFormat = "Cases: {0}";
    [SerializeField] private string clickProgressFormat = "+{0:F2}% per click";

    [Header("Animation Settings")]
    [SerializeField] private bool animateProgressBar = true;
    [SerializeField] private float animationSpeed = 10f;
    [SerializeField] private bool pulseOnNearComplete = true;
    [SerializeField] private float pulseThreshold = 90f;  // Start pulsing at 90%
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.1f;

    [Header("Case Drop Effects")]
    [SerializeField] private GameObject caseDropEffect;  // Optional particle/animation prefab
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip caseDropSound;
    [SerializeField] private AudioClip progressTickSound;  // Optional sound on progress gain

    [Header("Milestone Notifications")]
    [SerializeField] private bool showMilestones = true;
    [SerializeField] private Text milestoneText;
    [SerializeField] private float milestoneFadeTime = 2f;
    private float[] milestones = { 25f, 50f, 75f, 90f };
    private int lastMilestoneIndex = -1;

    // Events
    public UnityEvent OnCaseDropped;
    public UnityEvent<float> OnProgressUpdated;
    public UnityEvent<float> OnMilestoneReached;

    // Internal state
    private float displayedProgress = 0f;
    private float targetProgress = 0f;
    private bool isSubscribed = false;
    private float milestoneTimer = 0f;

    private void Awake()
    {
        // Initialize events
        OnCaseDropped ??= new UnityEvent();
        OnProgressUpdated ??= new UnityEvent<float>();
        OnMilestoneReached ??= new UnityEvent<float>();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        InitializeUI();
    }

    private void Update()
    {
        // Smooth progress bar animation
        if (animateProgressBar && displayedProgress != targetProgress)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, animationSpeed * Time.deltaTime);
            UpdateProgressBarVisual(displayedProgress);
        }

        // Pulse effect when near completion
        if (pulseOnNearComplete && displayedProgress >= pulseThreshold)
        {
            ApplyPulseEffect();
        }

        // Fade milestone text
        if (milestoneTimer > 0f)
        {
            milestoneTimer -= Time.deltaTime;
            if (milestoneText != null)
            {
                float alpha = Mathf.Clamp01(milestoneTimer / milestoneFadeTime);
                Color color = milestoneText.color;
                color.a = alpha;
                milestoneText.color = color;
            }
        }

        // Update CPS display (updates every frame for accuracy)
        UpdateCPSDisplay();
    }

    #region Subscription Management

    private void TrySubscribe()
    {
        if (isSubscribed) return;
        if (CaseProgressManager.Instance == null) return;

        CaseProgressManager.Instance.OnProgressChanged.AddListener(OnProgressChanged);
        CaseProgressManager.Instance.OnProgressGained.AddListener(OnProgressGained);
        CaseProgressManager.Instance.OnCaseDropped.AddListener(OnCaseDroppedHandler);
        CaseProgressManager.Instance.OnProgressReset.AddListener(OnProgressReset);

        isSubscribed = true;
        RefreshAllDisplays();
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;
        if (CaseProgressManager.Instance == null) return;

        CaseProgressManager.Instance.OnProgressChanged.RemoveListener(OnProgressChanged);
        CaseProgressManager.Instance.OnProgressGained.RemoveListener(OnProgressGained);
        CaseProgressManager.Instance.OnCaseDropped.RemoveListener(OnCaseDroppedHandler);
        CaseProgressManager.Instance.OnProgressReset.RemoveListener(OnProgressReset);

        isSubscribed = false;
    }

    #endregion

    #region Event Handlers

    private void OnProgressChanged(float currentProgress)
    {
        targetProgress = currentProgress;

        if (!animateProgressBar)
        {
            displayedProgress = currentProgress;
            UpdateProgressBarVisual(displayedProgress);
        }

        UpdatePercentText(currentProgress);
        CheckMilestones(currentProgress);
        OnProgressUpdated?.Invoke(currentProgress);
    }

    private void OnProgressGained(float amountGained, float newTotal)
    {
        // Play tick sound for feedback
        if (progressTickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(progressTickSound, 0.3f);
        }

        // Update click progress display
        UpdateClickProgressDisplay();
    }

    private void OnCaseDroppedHandler(CaseData caseData)
    {
        // Reset milestone tracking
        lastMilestoneIndex = -1;

        // Play case drop sound
        if (caseDropSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(caseDropSound);
        }

        // Spawn drop effect
        if (caseDropEffect != null)
        {
            Transform spawnPoint = effectSpawnPoint != null ? effectSpawnPoint : transform;
            GameObject effect = Instantiate(caseDropEffect, spawnPoint.position, Quaternion.identity);
            Destroy(effect, 3f);  // Clean up after 3 seconds
        }

        // Update cases dropped counter
        UpdateCasesDroppedDisplay();

        // Trigger event
        OnCaseDropped?.Invoke();

        Debug.Log($"[CaseProgressUI] Case dropped!  {(caseData != null ? caseData.caseName : "Unknown Case")}");
    }

    private void OnProgressReset()
    {
        // Optional: Add reset animation here
        if (!animateProgressBar)
        {
            displayedProgress = 0f;
            UpdateProgressBarVisual(0f);
        }
    }

    #endregion

    #region UI Updates

    private void InitializeUI()
    {
        // Set up progress bar
        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 100f;
            progressBar.value = 0f;
        }

        // Initialize displays
        RefreshAllDisplays();
    }

    public void RefreshAllDisplays()
    {
        if (CaseProgressManager.Instance == null) return;

        float currentProgress = CaseProgressManager.Instance.CurrentProgress;
        targetProgress = currentProgress;
        displayedProgress = currentProgress;

        UpdateProgressBarVisual(currentProgress);
        UpdatePercentText(currentProgress);
        UpdateCPSDisplay();
        UpdateCasesDroppedDisplay();
        UpdateClickProgressDisplay();
    }

    private void UpdateProgressBarVisual(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = progress;
        }

        // Update fill color based on gradient
        if (progressFillImage != null && progressGradient != null)
        {
            float normalizedProgress = progress / 100f;
            progressFillImage.color = progressGradient.Evaluate(normalizedProgress);
        }
    }

    private void UpdatePercentText(float progress)
    {
        if (progressPercentText != null)
        {
            progressPercentText.text = string.Format(percentFormat, progress);
        }
    }

    private void UpdateCPSDisplay()
    {
        if (casePerSecondText != null && CaseProgressManager.Instance != null)
        {
            float cps = CaseProgressManager.Instance.CurrentCasePercentPerSecond;
            casePerSecondText.text = string.Format(cpsFormat, cps);
        }
    }

    private void UpdateCasesDroppedDisplay()
    {
        if (casesDroppedText != null && CaseProgressManager.Instance != null)
        {
            int casesDropped = CaseProgressManager.Instance.TotalCasesDropped;
            casesDroppedText.text = string.Format(casesDroppedFormat, casesDropped);
        }
    }

    private void UpdateClickProgressDisplay()
    {
        if (clickProgressText != null && ClickerController.Instance != null)
        {
            float progressPerClick = ClickerController.Instance.CurrentCasePercentPerClick;
            clickProgressText.text = string.Format(clickProgressFormat, progressPerClick);
        }
    }

    #endregion

    #region Visual Effects

    private void ApplyPulseEffect()
    {
        if (progressFillImage == null) return;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        progressFillImage.transform.localScale = new Vector3(pulse, 1f, 1f);
    }

    private void CheckMilestones(float progress)
    {
        if (!showMilestones) return;

        for (int i = 0; i < milestones.Length; i++)
        {
            if (progress >= milestones[i] && i > lastMilestoneIndex)
            {
                lastMilestoneIndex = i;
                ShowMilestoneNotification(milestones[i]);
                OnMilestoneReached?.Invoke(milestones[i]);
                break;
            }
        }
    }

    private void ShowMilestoneNotification(float milestone)
    {
        if (milestoneText != null)
        {
            milestoneText.text = $"{milestone:F0}% Complete! ";
            milestoneText.color = new Color(milestoneText.color.r, milestoneText.color.g, milestoneText.color.b, 1f);
            milestoneTimer = milestoneFadeTime;
        }

        Debug.Log($"[CaseProgressUI] Milestone reached: {milestone}%");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Force refresh all UI elements. 
    /// </summary>
    public void ForceRefresh()
    {
        RefreshAllDisplays();
    }

    /// <summary>
    /// Set custom milestone percentages. 
    /// </summary>
    public void SetMilestones(float[] newMilestones)
    {
        milestones = newMilestones;
        lastMilestoneIndex = -1;
    }

    /// <summary>
    /// Get the current displayed progress (may differ from actual during animation).
    /// </summary>
    public float GetDisplayedProgress()
    {
        return displayedProgress;
    }

    /// <summary>
    /// Get the actual current progress. 
    /// </summary>
    public float GetActualProgress()
    {
        return CaseProgressManager.Instance?.CurrentProgress ?? 0f;
    }

    #endregion
}