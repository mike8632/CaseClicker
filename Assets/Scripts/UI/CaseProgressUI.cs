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
    [SerializeField] private Text caseComboText;      // Shows case combo multiplier

    [Header("Text Formats")]
    [SerializeField] private string percentFormat = "{0:F1}%";
    [SerializeField] private string cpsFormat = "{0:F2}%/s";
    [SerializeField] private string casesDroppedFormat = "Cases: {0}";
    [SerializeField] private string clickProgressFormat = "+{0:F2}% per click";
    [SerializeField] private string caseComboFormat = "Combo: {0:F2}x";

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

    // Events
    public UnityEvent OnCaseDropped;
    public UnityEvent<float> OnProgressUpdated;

    // Internal state
    private float displayedProgress = 0f;
    private float targetProgress = 0f;
    private bool isSubscribed = false;

    private void Awake()
    {
        // Initialize events
        OnCaseDropped ??= new UnityEvent();
        OnProgressUpdated ??= new UnityEvent<float>();
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshCaseComboText();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        InitializeUI();
        RefreshCaseComboText();
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

        // Update CPS display (updates every frame for accuracy)
        UpdateCPSDisplay();
        // Update Case combo text every frame for responsiveness
        RefreshCaseComboText();
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

        // Subscribe to case combo changes if available
        if (GameManager.Instance != null && GameManager.Instance.Combo != null)
        {
            GameManager.Instance.Combo.OnCaseComboChanged.AddListener(OnCaseComboChanged);
        }

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

        if (GameManager.Instance != null && GameManager.Instance.Combo != null)
        {
            GameManager.Instance.Combo.OnCaseComboChanged.RemoveListener(OnCaseComboChanged);
        }

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

    private void OnCaseComboChanged(float newMultiplier)
    {
        RefreshCaseComboText();
        UpdateClickProgressDisplay();
    }

    #endregion

    #region UI Updates

    private void InitializeUI()
    {
        RefreshAllDisplays();
    }

    private void RefreshAllDisplays()
    {
        UpdatePercentText(targetProgress);
        UpdateCPSDisplay();
        UpdateClickProgressDisplay();
        UpdateCasesDroppedDisplay();
        RefreshCaseComboText();
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
            casePerSecondText.text = string.Format(cpsFormat, CaseProgressManager.Instance.CurrentCasePercentPerSecond);
        }
    }

    private void UpdateCasesDroppedDisplay()
    {
        if (casesDroppedText != null && CaseProgressManager.Instance != null)
        {
            casesDroppedText.text = string.Format(casesDroppedFormat, CaseProgressManager.Instance.TotalCasesDropped);
        }
    }

    private void UpdateClickProgressDisplay()
    {
        if (clickProgressText != null && ClickerController.Instance != null)
        {
            clickProgressText.text = string.Format(clickProgressFormat, ClickerController.Instance.CurrentCasePercentPerClick);
        }
    }

    private void RefreshCaseComboText()
    {
        if (caseComboText == null) return;
        float mult = GameManager.Instance?.Combo?.CurrentCaseMultiplier ?? 1f;
        caseComboText.text = string.Format(caseComboFormat, mult);
    }

    private void UpdateProgressBarVisual(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = progress;
        }

        if (progressFillImage != null && progressGradient != null)
        {
            float t = Mathf.Clamp01(progress / 100f);
            progressFillImage.color = progressGradient.Evaluate(t);
        }
    }

    private void ApplyPulseEffect()
    {
        if (progressFillImage == null) return;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        progressFillImage.transform.localScale = new Vector3(pulse, 1f, 1f);
    }

    #endregion
}