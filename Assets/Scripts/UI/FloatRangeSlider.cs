using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FloatRangeSlider : MonoBehaviour
{
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

    private void UpdateVisuals()
    {
        float min = minSlider.value;
        float max = maxSlider.value;

        if (minText != null)
            minText.text = min.ToString("0.00");

        if (maxText != null)
            maxText.text = max.ToString("0.00");

        UpdateFill(min, max);
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
