using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ToggleBoxFillUI : MonoBehaviour
{
    [SerializeField] private Image boxFillImage;
    [SerializeField] private bool startFilled = false;
    [Tooltip("When enabled, the whole box fill GameObject (including its children such as text and icon) is toggled on/off, instead of only the Image component.")]
    [SerializeField] private bool toggleChildren = false;
    [Tooltip("If false, this component will not toggle itself on click. Use an external controller to set the filled state.")]
    [SerializeField] private bool allowSelfToggle = true;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (boxFillImage != null)
            SetFilled(startFilled);
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ToggleFill);
            button.onClick.AddListener(ToggleFill);
        }
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(ToggleFill);
    }

    private void ToggleFill()
    {
        if (!allowSelfToggle)
            return;

        if (boxFillImage == null)
            return;

        bool currentlyFilled = toggleChildren
            ? boxFillImage.gameObject.activeSelf
            : boxFillImage.enabled;

        SetFilled(!currentlyFilled);
    }

    public void SetFilled(bool filled)
    {
        if (boxFillImage == null)
            return;

        if (toggleChildren)
        {
            // Toggle the entire GameObject so children (text, icon, etc.) follow.
            boxFillImage.gameObject.SetActive(filled);
        }
        else
        {
            // Original behaviour: only the Image component is toggled.
            boxFillImage.enabled = filled;
        }
    }

    public bool IsFilled
    {
        get
        {
            if (boxFillImage == null)
                return false;

            return toggleChildren
                ? boxFillImage.gameObject.activeSelf
                : boxFillImage.enabled;
        }
    }
}
