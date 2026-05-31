using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ToggleBoxFillUI : MonoBehaviour
{
    [SerializeField] private Image boxFillImage;
    [SerializeField] private bool startFilled = false;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (boxFillImage != null)
            boxFillImage.enabled = startFilled;
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
        if (boxFillImage != null)
            boxFillImage.enabled = !boxFillImage.enabled;
    }
}
