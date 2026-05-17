using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TooltipHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private Text tooltipText;
    [SerializeField] private string hoverText;

    private void Awake()
    {
        if (hideOnStart && tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(true);

        if (tooltipText != null)
            tooltipText.text = hoverText;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }
}
