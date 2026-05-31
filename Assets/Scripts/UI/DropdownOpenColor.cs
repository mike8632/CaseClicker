using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DropdownOpenColor : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private Image targetImage;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color openColor = Color.magenta;

    private bool wasOpen;

    private void Reset()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null)
            targetImage.color = normalColor;
    }

    private void Update()
    {
        if (dropdown == null || targetImage == null)
            return;

        bool isOpen = dropdown.IsExpanded;

        if (isOpen != wasOpen)
        {
            targetImage.color = isOpen ? openColor : normalColor;
            wasOpen = isOpen;
        }
    }
}