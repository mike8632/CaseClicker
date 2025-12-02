using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages sidebar tabs and panels. Call SelectTab(id) or SelectTab(index).
/// Also supports keyboard shortcuts 1..9 to switch tabs and events when tab changes.
/// </summary>
public class SidebarController : MonoBehaviour
{
    public static SidebarController Instance { get; private set; }

    [Header("Registration")]
    [Tooltip("Optionally populate panels manually. If empty, all TabPanel children of this object (or assigned parent) will be auto-registered at Start.")]
    [SerializeField] private List<TabPanel> panels = new List<TabPanel>();

    [Tooltip("If set, controller will search this transform for TabPanel components on Start.")]
    [SerializeField] private Transform panelsParent;

    [Header("Behavior")]
    [Tooltip("Index of the tab to open on start (0-based).")]
    [SerializeField] private int startTabIndex = 0;

    [Tooltip("If true, selecting the current tab will close it (toggle behavior).")]
    [SerializeField] private bool allowToggleCurrent = false;

    [Header("Keyboard")]
    [Tooltip("Enable pressing numeric keys 1..9 to open tabs 0..8")]
    [SerializeField] private bool enableNumberShortcuts = true;

    // Events
    public UnityEvent<string, int> OnTabChanged; // (tabId, index)
    public UnityEvent<string, int> OnTabClosed;

    private Dictionary<string, TabPanel> panelMap = new Dictionary<string, TabPanel>();
    private List<TabPanel> orderedPanels = new List<TabPanel>();
    private TabPanel activePanel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        OnTabChanged ??= new UnityEvent<string, int>();
        OnTabClosed ??= new UnityEvent<string, int>();
    }

    private void Start()
    {
        AutoRegisterPanels();
        // clamp start index
        if (orderedPanels.Count == 0) return;
        int idx = Mathf.Clamp(startTabIndex, 0, orderedPanels.Count - 1);
        SelectTab(idx);
    }

    private void Update()
    {
        if (!enableNumberShortcuts) return;

        // check numeric keys 1..9 (Alpha1..Alpha9)
        for (int i = 0; i < Math.Min(9, orderedPanels.Count); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectTab(i);
            }
        }
    }

    private void AutoRegisterPanels()
    {
        panelMap.Clear();
        orderedPanels.Clear();

        if (panelsParent != null)
        {
            var found = panelsParent.GetComponentsInChildren<TabPanel>(true);
            foreach (var p in found) RegisterPanel(p);
        }

        // Add manually assigned list (allows overriding or custom order)
        if (panels != null && panels.Count > 0)
        {
            foreach (var p in panels)
            {
                if (p != null) RegisterPanel(p);
            }
        }

        // If still empty, search children of this GameObject
        if (orderedPanels.Count == 0)
        {
            var found = GetComponentsInChildren<TabPanel>(true);
            foreach (var p in found) RegisterPanel(p);
        }
    }

    /// <summary>
    /// Register a panel with the controller. Panels must have unique Ids.
    /// </summary>
    public void RegisterPanel(TabPanel panel)
    {
        if (panel == null) return;
        if (string.IsNullOrEmpty(panel.TabId))
        {
            Debug.LogWarning($"[SidebarController] Panel on '{panel.gameObject.name}' has empty TabId. Skipping.");
            return;
        }

        if (panelMap.ContainsKey(panel.TabId))
        {
            // ignore duplicates but keep first registered
            Debug.LogWarning($"[SidebarController] Duplicate TabId '{panel.TabId}' on '{panel.gameObject.name}'. Skipping.");
            return;
        }

        panelMap[panel.TabId] = panel;
        orderedPanels.Add(panel);
        // ensure panel starts hidden
        panel.HideImmediate();
    }

    /// <summary>
    /// Select tab by unique id.
    /// </summary>
    public void SelectTab(string tabId)
    {
        if (string.IsNullOrEmpty(tabId)) return;
        if (!panelMap.TryGetValue(tabId, out var panel)) { Debug.LogWarning($"[SidebarController] No panel with id '{tabId}'"); return; }
        DoSelect(panel);
    }

    /// <summary>
    /// Select tab by index (0-based).
    /// </summary>
    public void SelectTab(int index)
    {
        if (index < 0 || index >= orderedPanels.Count) { Debug.LogWarning($"[SidebarController] Index {index} out of range"); return; }
        DoSelect(orderedPanels[index]);
    }

    private void DoSelect(TabPanel panel)
    {
        if (panel == activePanel)
        {
            if (allowToggleCurrent)
            {
                // close it
                panel.Hide();
                OnTabClosed?.Invoke(panel.TabId, orderedPanels.IndexOf(panel));
                activePanel = null;
            }
            return;
        }

        // hide currently active
        if (activePanel != null)
        {
            activePanel.Hide();
        }

        // show new
        activePanel = panel;
        activePanel.Show();

        OnTabChanged?.Invoke(panel.TabId, orderedPanels.IndexOf(panel));
    }

    /// <summary>
    /// Returns index of a registered tab or -1 if not found.
    /// </summary>
    public int GetTabIndex(string tabId) => panelMap.TryGetValue(tabId, out var p) ? orderedPanels.IndexOf(p) : -1;

    public IReadOnlyList<TabPanel> GetAllPanels() => orderedPanels.AsReadOnly();

    /// <summary>
    /// Close the currently open tab (if any).
    /// </summary>
    public void CloseActiveTab()
    {
        if (activePanel == null) return;
        var id = activePanel.TabId;
        var idx = orderedPanels.IndexOf(activePanel);
        activePanel.Hide();
        activePanel = null;
        OnTabClosed?.Invoke(id, idx);
    }
}