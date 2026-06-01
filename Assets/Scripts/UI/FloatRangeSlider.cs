using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FloatRangeSlider : MonoBehaviour
{
    private const string FloatFormat = "0.00##############";
    private const int MaxInputLength = 16;
    [Header("Sliders")]
    public Slider minSlider;
    public Slider maxSlider;
    [SerializeField] private bool forceLeftToRight = true;

    [Header("Visual Fill")]
    public RectTransform backgroundBar;
    public RectTransform selectedRangeFill;

    [Header("Texts")]
    public TMP_Text minText;
    public TMP_Text maxText;

    [Header("Input Fields")]
    public TMP_InputField minInputField;
    public TMP_InputField maxInputField;

    private bool suppressInputEvents;

    private void Awake()
    {
        if (minSlider == null || maxSlider == null)
            return;

        if (forceLeftToRight)
        {
            minSlider.direction = Slider.Direction.LeftToRight;
            maxSlider.direction = Slider.Direction.LeftToRight;
        }

        minSlider.minValue = 0f;
        minSlider.maxValue = 1f;

        maxSlider.minValue = 0f;
        maxSlider.maxValue = 1f;

        minSlider.onValueChanged.AddListener(OnMinChanged);
        maxSlider.onValueChanged.AddListener(OnMaxChanged);

        if (minInputField != null)
            minInputField.onValueChanged.AddListener(OnMinInputChanged);
        if (maxInputField != null)
            maxInputField.onValueChanged.AddListener(OnMaxInputChanged);

        ConfigureInputField(minInputField);
        ConfigureInputField(maxInputField);

        minSlider.SetValueWithoutNotify(0f);
        maxSlider.SetValueWithoutNotify(1f);

        UpdateVisuals();
    }

    private void OnEnable()
    {
        if (minSlider == null || maxSlider == null)
            return;

        minSlider.SetValueWithoutNotify(Mathf.Clamp(minSlider.value, minSlider.minValue, minSlider.maxValue));
        maxSlider.SetValueWithoutNotify(Mathf.Clamp(maxSlider.value, maxSlider.minValue, maxSlider.maxValue));
        if (maxSlider.value < minSlider.value)
            maxSlider.SetValueWithoutNotify(minSlider.value);

        UpdateVisuals();
    }

    private void OnMinChanged(float value)
    {
        if (value > maxSlider.value)
            minSlider.value = maxSlider.value;

        UpdateVisuals();
    }

    private void OnMaxChanged(float value)
    {
        if (value < minSlider.value)
            maxSlider.value = minSlider.value;

        UpdateVisuals();
    }

    private void OnMinInputChanged(string value)
    {
        if (suppressInputEvents || minSlider == null)
            return;

        if (!TryParseInput(value, out float parsed))
            return;

        parsed = Mathf.Clamp(parsed, minSlider.minValue, minSlider.maxValue);
        minSlider.value = parsed;
    }

    private void OnMaxInputChanged(string value)
    {
        if (suppressInputEvents || maxSlider == null)
            return;

        if (!TryParseInput(value, out float parsed))
            return;

        parsed = Mathf.Clamp(parsed, maxSlider.minValue, maxSlider.maxValue);
        maxSlider.value = parsed;
    }

    private void OnMinInputEndEdit(string value)
    {
        ClampInputField(minInputField, minSlider);
    }

    private void OnMaxInputEndEdit(string value)
    {
        ClampInputField(maxInputField, maxSlider);
    }

    private void UpdateVisuals()
    {
        float min = minSlider.value;
        float max = maxSlider.value;

        if (minText != null)
            minText.text = min.ToString(FloatFormat, System.Globalization.CultureInfo.InvariantCulture);

        if (maxText != null)
            maxText.text = max.ToString(FloatFormat, System.Globalization.CultureInfo.InvariantCulture);

        suppressInputEvents = true;
        if (minInputField != null)
            minInputField.text = min.ToString(FloatFormat, System.Globalization.CultureInfo.InvariantCulture);
        if (maxInputField != null)
            maxInputField.text = max.ToString(FloatFormat, System.Globalization.CultureInfo.InvariantCulture);
        suppressInputEvents = false;

        UpdateFill(min, max);
    }

    private static bool TryParseInput(string value, out float parsed)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
            return true;

        return float.TryParse(value, out parsed);
    }

    private void ConfigureInputField(TMP_InputField inputField)
    {
        if (inputField == null)
            return;

        inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        inputField.characterValidation = TMP_InputField.CharacterValidation.Decimal;
        inputField.characterLimit = MaxInputLength;
        inputField.onValidateInput = ValidateFloatChar;
        inputField.onEndEdit.RemoveListener(OnMinInputEndEdit);
        inputField.onEndEdit.RemoveListener(OnMaxInputEndEdit);

        if (inputField.placeholder is TMP_Text placeholderText)
            placeholderText.text = inputField == minInputField ? "0.00" : "1.00";

        if (inputField == minInputField)
            inputField.onEndEdit.AddListener(OnMinInputEndEdit);
        else if (inputField == maxInputField)
            inputField.onEndEdit.AddListener(OnMaxInputEndEdit);
    }

    private void ClampInputField(TMP_InputField inputField, Slider slider)
    {
        if (inputField == null || slider == null)
            return;

        if (!TryParseInput(inputField.text, out float parsed))
            parsed = slider.minValue;

        parsed = Mathf.Clamp(parsed, slider.minValue, slider.maxValue);
        inputField.text = parsed.ToString(FloatFormat, System.Globalization.CultureInfo.InvariantCulture);
        slider.value = parsed;
    }

    private char ValidateFloatChar(string text, int charIndex, char addedChar)
    {
        if (char.IsDigit(addedChar))
            return IsInputWithinRange(text, charIndex, addedChar) ? addedChar : '\0';

        if (addedChar == '.' && !text.Contains("."))
            return IsInputWithinRange(text, charIndex, addedChar) ? addedChar : '\0';

        return '\0';
    }

    private static bool IsInputWithinRange(string text, int charIndex, char addedChar)
    {
        string candidate = text.Insert(charIndex, addedChar.ToString());
        if (candidate == "." || candidate == "0." || candidate == "1.")
            return true;

        if (!float.TryParse(candidate, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            return false;

        return parsed >= 0f && parsed <= 1f;
    }

    private void UpdateFill(float min, float max)
    {
        if (backgroundBar == null || selectedRangeFill == null)
            return;

        float barWidth = backgroundBar.rect.width;

        if (barWidth <= 0f)
            return;

        float leftX = min * barWidth;
        float rightX = max * barWidth;

        selectedRangeFill.anchorMin = new Vector2(0f, 0.5f);
        selectedRangeFill.anchorMax = new Vector2(0f, 0.5f);
        selectedRangeFill.pivot = new Vector2(0f, 0.5f);

        selectedRangeFill.anchoredPosition = new Vector2(leftX, 0f);
        selectedRangeFill.sizeDelta = new Vector2(rightX - leftX, selectedRangeFill.sizeDelta.y);
    }
}
