using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Settings tab panel. Persists values with PlayerPrefs and applies them at startup.
/// Master volume maps to AudioListener.volume so it affects all audio without needing an AudioMixer.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    private const string MasterVolumeKey = "settings.masterVolume";
    private const string FullscreenKey = "settings.fullscreen";

    [Header("Controls (optional)")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Optional value label")]
    [Tooltip("If set, shows the current master volume as a percentage.")]
    [SerializeField] private TMPro.TMP_Text masterVolumeLabel;

    private void Awake()
    {
        // Apply saved settings as early as possible.
        ApplySavedSettings();
    }

    private void OnEnable()
    {
        SyncControlsToCurrentValues();

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void OnDisable()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
    }

    private void ApplySavedSettings()
    {
        float volume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        AudioListener.volume = Mathf.Clamp01(volume);

        if (PlayerPrefs.HasKey(FullscreenKey))
            Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) == 1;
    }

    private void SyncControlsToCurrentValues()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        UpdateVolumeLabel(AudioListener.volume);
    }

    private void OnMasterVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MasterVolumeKey, value);
        PlayerPrefs.Save();
        UpdateVolumeLabel(value);
    }

    private void OnFullscreenChanged(bool isOn)
    {
        Screen.fullScreen = isOn;
        PlayerPrefs.SetInt(FullscreenKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateVolumeLabel(float value)
    {
        if (masterVolumeLabel != null)
            masterVolumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
