using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opens a specific sidebar tab when clicked.
/// </summary>
[RequireComponent(typeof(Button))]
public class OpenTabButton : MonoBehaviour
{
    [SerializeField] private string tabId = "";

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (string.IsNullOrEmpty(tabId))
        {
            Debug.LogWarning($"[OpenTabButton] '{gameObject.name}' has no tabId set.");
            return;
        }

        SidebarController.Instance?.SelectTab(tabId);
    }
}
