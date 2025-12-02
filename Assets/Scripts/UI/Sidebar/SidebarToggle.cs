using UnityEngine;

/// <summary>
/// Small helper to show/hide the sidebar GameObject (animated via scale or CanvasGroup).
/// Assign this to a toggle button to open/close the whole sidebar.
/// </summary>
public class SidebarToggle : MonoBehaviour
{
    [Tooltip("Root of sidebar UI to show/hide.")]
    public GameObject sidebarRoot;

    [Tooltip("Optional animator to control open/close states (parameter: 'Open' bool)")]
    public Animator sidebarAnimator;

    private bool isOpen = true;

    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        if (sidebarRoot != null) sidebarRoot.SetActive(isOpen);
        if (sidebarAnimator != null) sidebarAnimator.SetBool("Open", isOpen);
    }

    private void Start()
    {
        // ensure initial state
        SetOpen(isOpen);
    }
}