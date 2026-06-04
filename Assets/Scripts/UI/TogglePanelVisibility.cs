using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TogglePanelVisibility : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private bool startVisible = false;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (panel != null)
            panel.SetActive(startVisible);
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(TogglePanel);
            button.onClick.AddListener(TogglePanel);
        }
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(TogglePanel);
    }

    private void TogglePanel()
    {
        if (panel == null)
            return;

        panel.SetActive(!panel.activeSelf);
    }
}
