using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to your sidebar Button. Set TabId to match the TabPanel's TabId.
/// This script will call SidebarController.SelectTab when clicked and optionally update visual state.
/// </summary>
[RequireComponent(typeof(Button))]
public class TabButton : MonoBehaviour
{
    [Tooltip("Unique id of the TabPanel this button opens.")]
    public string tabId;

    [Header("Visual (optional)")]
    public GameObject selectedHighlight; // assign an indicator (e.g. an image) to show selected state

    private Button btn;

    private void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClicked);
    }

    private void OnEnable()
    {
        RefreshSelectedState();
        // subscribe to changes so highlight updates when tabs change
        if (SidebarController.Instance != null)
        {
            SidebarController.Instance.OnTabChanged.AddListener(OnTabChanged);
            SidebarController.Instance.OnTabClosed.AddListener(OnTabClosed);
        }
    }

    private void OnDisable()
    {
        if (SidebarController.Instance != null)
        {
            SidebarController.Instance.OnTabChanged.RemoveListener(OnTabChanged);
            SidebarController.Instance.OnTabClosed.RemoveListener(OnTabClosed);
        }
    }

    private void OnClicked()
    {
        if (string.IsNullOrEmpty(tabId))
        {
            Debug.LogWarning($"[TabButton] Button '{gameObject.name}' has no tabId set.");
            return;
        }

        SidebarController.Instance?.SelectTab(tabId);
    }

    private void OnTabChanged(string newTabId, int idx)
    {
        RefreshSelectedState();
    }

    private void OnTabClosed(string closedTabId, int idx)
    {
        RefreshSelectedState();
    }

    private void RefreshSelectedState()
    {
        if (selectedHighlight == null) return;

        bool selected = false;
        if (SidebarController.Instance != null)
        {
            int activeIndex = -1;
            var panels = SidebarController.Instance.GetAllPanels();
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].IsVisible)
                {
                    activeIndex = i;
                    break;
                }
            }
            int myIndex = SidebarController.Instance.GetTabIndex(tabId);
            selected = myIndex >= 0 && myIndex == activeIndex;
        }

        selectedHighlight.SetActive(selected);
    }

    // helper to get active index quickly (used above)
    private int GetActiveIndex()
    {
        if (SidebarController.Instance == null) return -1;
        var panels = SidebarController.Instance.GetAllPanels();
        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i].IsVisible) return i;
        }
        return -1;
    }
}