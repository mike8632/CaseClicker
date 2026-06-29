using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Settings tab panel. Persists values with PlayerPrefs and applies them at startup.
/// Includes support for Audio, Visuals, Gameplay Display, and Debug settings.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    public const string MasterVolumeKey = "settings.masterVolume";
    public const string MusicVolumeKey = "settings.musicVolume";
    public const string SfxVolumeKey = "settings.sfxVolume";
    public const string MuteAudioKey = "settings.muteAudio";
    public const string ShowFloatingTextKey = "settings.showFloatingText";
    public const string FallingClickIconsKey = "settings.fallingClickIcons";
    public const string ReduceAnimationsKey = "settings.reduceAnimations";
    public const string CompactMoneyTextKey = "settings.compactMoneyText";
    public const string ShowExactFloatValuesKey = "settings.showExactFloatValues";
    public const string ShowCollectionIconsKey = "settings.showCollectionIcons";

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMPro.TMP_Text masterVolumeLabel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMPro.TMP_Text musicVolumeLabel;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMPro.TMP_Text sfxVolumeLabel;
    [SerializeField] private Toggle muteAudioToggle;

    [Header("Visuals")]
    [SerializeField] private Toggle showFloatingTextToggle;
    [SerializeField] private Toggle fallingClickIconsToggle;
    [SerializeField] private Toggle reduceAnimationsToggle;

    [Header("Gameplay Display")]
    [SerializeField] private Toggle compactMoneyTextToggle;
    [SerializeField] private Toggle showExactFloatValuesToggle;
    [SerializeField] private Toggle showCollectionIconsToggle;

    [Header("Reset")]
    [SerializeField] private Button resetProgressButton;

    [Header("Developer Tools")]
    [SerializeField] private Button repriceInventoryButton;
    [SerializeField] private Button giveTestMoneyButton;

    private void Awake()
    {
        ApplySavedSettings();
    }

    private void OnEnable()
    {
        SyncControlsToCurrentValues();

        // Audio listeners
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        if (muteAudioToggle != null) muteAudioToggle.onValueChanged.AddListener(OnMuteAudioChanged);

        // Visual listeners
        if (showFloatingTextToggle != null) showFloatingTextToggle.onValueChanged.AddListener(OnShowFloatingTextChanged);
        if (fallingClickIconsToggle != null) fallingClickIconsToggle.onValueChanged.AddListener(OnFallingClickIconsChanged);
        if (reduceAnimationsToggle != null) reduceAnimationsToggle.onValueChanged.AddListener(OnReduceAnimationsChanged);

        // Gameplay listeners
        if (compactMoneyTextToggle != null) compactMoneyTextToggle.onValueChanged.AddListener(OnCompactMoneyTextChanged);
        if (showExactFloatValuesToggle != null) showExactFloatValuesToggle.onValueChanged.AddListener(OnShowExactFloatValuesChanged);
        if (showCollectionIconsToggle != null) showCollectionIconsToggle.onValueChanged.AddListener(OnShowCollectionIconsChanged);

        // Reset
        if (resetProgressButton != null) resetProgressButton.onClick.AddListener(OnResetProgressClicked);

        // Developer buttons
        if (repriceInventoryButton != null) repriceInventoryButton.onClick.AddListener(OnRepriceInventoryClicked);
        if (giveTestMoneyButton != null) giveTestMoneyButton.onClick.AddListener(OnGiveTestMoneyClicked);
    }

    private void OnDisable()
    {
        // Audio listeners
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        if (muteAudioToggle != null) muteAudioToggle.onValueChanged.RemoveListener(OnMuteAudioChanged);

        // Visual listeners
        if (showFloatingTextToggle != null) showFloatingTextToggle.onValueChanged.RemoveListener(OnShowFloatingTextChanged);
        if (fallingClickIconsToggle != null) fallingClickIconsToggle.onValueChanged.RemoveListener(OnFallingClickIconsChanged);
        if (reduceAnimationsToggle != null) reduceAnimationsToggle.onValueChanged.RemoveListener(OnReduceAnimationsChanged);

        // Gameplay listeners
        if (compactMoneyTextToggle != null) compactMoneyTextToggle.onValueChanged.RemoveListener(OnCompactMoneyTextChanged);
        if (showExactFloatValuesToggle != null) showExactFloatValuesToggle.onValueChanged.RemoveListener(OnShowExactFloatValuesChanged);
        if (showCollectionIconsToggle != null) showCollectionIconsToggle.onValueChanged.RemoveListener(OnShowCollectionIconsChanged);

        // Reset
        if (resetProgressButton != null) resetProgressButton.onClick.RemoveListener(OnResetProgressClicked);

        // Developer buttons
        if (repriceInventoryButton != null) repriceInventoryButton.onClick.RemoveListener(OnRepriceInventoryClicked);
        if (giveTestMoneyButton != null) giveTestMoneyButton.onClick.RemoveListener(OnGiveTestMoneyClicked);
    }

    private void ApplySavedSettings()
    {
        bool isMuted = PlayerPrefs.GetInt(MuteAudioKey, 0) == 1;
        float volume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        AudioListener.volume = isMuted ? 0f : Mathf.Clamp01(volume);
    }

    private void SyncControlsToCurrentValues()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f));
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        if (muteAudioToggle != null) muteAudioToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(MuteAudioKey, 0) == 1);

        if (showFloatingTextToggle != null) showFloatingTextToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(ShowFloatingTextKey, 1) == 1);
        if (fallingClickIconsToggle != null) fallingClickIconsToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FallingClickIconsKey, 1) == 1);
        if (reduceAnimationsToggle != null) reduceAnimationsToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(ReduceAnimationsKey, 0) == 1);

        if (compactMoneyTextToggle != null) compactMoneyTextToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(CompactMoneyTextKey, 1) == 1);
        if (showExactFloatValuesToggle != null) showExactFloatValuesToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(ShowExactFloatValuesKey, 0) == 1);
        if (showCollectionIconsToggle != null) showCollectionIconsToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(ShowCollectionIconsKey, 1) == 1);

        UpdateVolumeLabels();
    }

    private void UpdateVolumeLabels()
    {
        if (masterVolumeLabel != null)
            masterVolumeLabel.text = Mathf.RoundToInt(PlayerPrefs.GetFloat(MasterVolumeKey, 1f) * 100f) + "%";
        if (musicVolumeLabel != null)
            musicVolumeLabel.text = Mathf.RoundToInt(PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f) * 100f) + "%";
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = Mathf.RoundToInt(PlayerPrefs.GetFloat(SfxVolumeKey, 1f) * 100f) + "%";
    }

    private void OnMasterVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, value);
        PlayerPrefs.Save();
        UpdateVolumeLabels();
        ApplySavedSettings();
    }

    private void OnMusicVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, value);
        PlayerPrefs.Save();
        UpdateVolumeLabels();
    }

    private void OnSfxVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, value);
        PlayerPrefs.Save();
        UpdateVolumeLabels();
    }

    private void OnMuteAudioChanged(bool isMuted)
    {
        PlayerPrefs.SetInt(MuteAudioKey, isMuted ? 1 : 0);
        PlayerPrefs.Save();
        ApplySavedSettings();
    }

    private void OnShowFloatingTextChanged(bool show)
    {
        PlayerPrefs.SetInt(ShowFloatingTextKey, show ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnFallingClickIconsChanged(bool show)
    {
        PlayerPrefs.SetInt(FallingClickIconsKey, show ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnReduceAnimationsChanged(bool reduce)
    {
        PlayerPrefs.SetInt(ReduceAnimationsKey, reduce ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnCompactMoneyTextChanged(bool compact)
    {
        PlayerPrefs.SetInt(CompactMoneyTextKey, compact ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnShowExactFloatValuesChanged(bool show)
    {
        PlayerPrefs.SetInt(ShowExactFloatValuesKey, show ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnShowCollectionIconsChanged(bool show)
    {
        PlayerPrefs.SetInt(ShowCollectionIconsKey, show ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnResetProgressClicked()
    {
        if (ConfirmDialogUI.Instance == null)
        {
            Debug.LogError("[SettingsPanelUI] Cannot reset progress — ConfirmDialogUI is missing from the scene. Add it before using this button.");
            return;
        }

        ConfirmDialogUI.Instance.Show(
            "Reset Progress?",
            "This will delete ALL saved progress and restart the game from the beginning.\n\nThis cannot be undone.",
            () => GameManager.Instance?.ResetAndReload());
    }

    private void OnRepriceInventoryClicked()
    {
        SkinInventoryManager.Instance?.RepriceInventoryFromCachedPrices();
    }

    private void OnGiveTestMoneyClicked()
    {
        BalanceManager.Instance?.AddMoney(1000000.0);
    }
}
