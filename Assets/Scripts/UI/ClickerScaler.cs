using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles the "squish" animation for a clickable UI element
/// (coin, case) when pressed / held.
/// </summary>
public class ClickSquish : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [Tooltip("The UI element (coin, case, etc.) that should squish when clicked.")]
    public RectTransform target;   // The coin/case image

    [Header("Scales")]
    [Range(0.5f, 1f)]
    [Tooltip("Scale while the button is clicked/tapped briefly (e.g. 0.85 = 85% size).")]
    public float clickScale = 0.85f;  // size while clicked

    [Range(0.5f, 1f)]
    [Tooltip("Scale while the button is held down past the hold threshold (e.g. 0.7 = 70% size).")]
    public float holdScale = 0.70f;  // size while holding

    [Tooltip("How fast it animates between sizes.")]
    public float scaleLerpSpeed = 10f;

    [Header("Hold Settings")]
    [Tooltip("How long (in seconds) you have to hold before it counts as a 'hold'.")]
    public float holdThreshold = 0.15f;

    private Vector3 _originalScale;
    private bool _isPointerDown;
    private float _pointerDownTime;
    private float _targetScale = 1f;

    private void Awake()
    {
        if (target == null)
            target = transform as RectTransform;

        _originalScale = target.localScale;
        _targetScale = 1f;
    }

    private void Update()
    {
        // Decide what scale we are aiming for:
        if (_isPointerDown)
        {
            _pointerDownTime += Time.unscaledDeltaTime;

            // If held long enough go to holdScale
            if (_pointerDownTime >= holdThreshold)
                _targetScale = holdScale;
            else
                _targetScale = clickScale;
        }
        else
        {
            // Not pressed go back to normal size
            _targetScale = 1f;
        }

        // Smoothly animate towards target scale
        Vector3 desiredScale = _originalScale * _targetScale;
        target.localScale = Vector3.Lerp(
            target.localScale,
            desiredScale,
            scaleLerpSpeed * Time.unscaledDeltaTime
        );
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPointerDown = true;
        _pointerDownTime = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPointerDown = false;
    }
}
