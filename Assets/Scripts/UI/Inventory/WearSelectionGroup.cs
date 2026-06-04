using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Enforces single-selection across a set of uGUI Buttons (radio-group behavior).
/// Only one button can be "selected" at a time. Attach to the buttons' common
/// parent and assign the buttons in the desired order.
/// </summary>
[DisallowMultipleComponent]
public class WearSelectionGroup : MonoBehaviour
{
    [System.Serializable]
    public class IndexEvent : UnityEvent<int> { }

    [Tooltip("The buttons in this group, in order. Only one can be selected at a time.")]
    [SerializeField] private List<Button> buttons = new List<Button>();

    [Tooltip("Optional ToggleBoxFillUI components that visually represent selection for each button. If set, they will be synced to the selected index.")]
    [SerializeField] private List<ToggleBoxFillUI> toggleFills = new List<ToggleBoxFillUI>();

    [Tooltip("Name of the child graphic used to show the selected highlight. " +
             "Falls back to the button's Target Graphic if the child is not found.")]
    [SerializeField] private string highlightChildName = "Border";

    [Tooltip("Color applied to the highlight graphic of unselected buttons.")]
    [SerializeField] private Color normalColor = new Color(0.071f, 0.133f, 0.212f, 1f);

    [Tooltip("Color applied to the highlight graphic of the selected button.")]
    [SerializeField] private Color selectedColor = new Color(0.18f, 0.45f, 0.85f, 1f);

    [Tooltip("If true, clicking the already-selected button deselects it (allowing zero selected). " +
             "If false, one button always stays selected once chosen.")]
    [SerializeField] private bool allowDeselect = false;

    [Tooltip("If true, you must deselect the current button before selecting another.")]
    [SerializeField] private bool requireDeselectBeforeSwitch = true;

    [Tooltip("Index selected on Start (-1 = none).")]
    [SerializeField] private int defaultIndex = -1;

    [Tooltip("Raised whenever the selection changes. Argument is the selected index, or -1 if none.")]
    public IndexEvent onSelectionChanged;

    private int selectedIndex = -1;
    private UnityAction[] handlers;

    /// <summary>Currently selected button index, or -1 if none.</summary>
    public int SelectedIndex => selectedIndex;

    /// <summary>Currently selected button, or null if none.</summary>
    public Button SelectedButton =>
        (selectedIndex >= 0 && selectedIndex < buttons.Count) ? buttons[selectedIndex] : null;

    private void Awake()
    {
        handlers = new UnityAction[buttons.Count];
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null) continue;
            int idx = i;
            handlers[i] = () => OnButtonClicked(idx);
            buttons[i].onClick.AddListener(handlers[i]);
        }

        selectedIndex = -1;
        if (defaultIndex >= 0 && defaultIndex < buttons.Count)
            SetSelected(defaultIndex, false);
        else
            ApplyVisuals();
    }

    private void OnDestroy()
    {
        if (handlers == null) return;
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i] != null && handlers[i] != null)
                buttons[i].onClick.RemoveListener(handlers[i]);
    }

    private void OnButtonClicked(int index)
    {
        if (requireDeselectBeforeSwitch && selectedIndex >= 0 && index != selectedIndex)
            return;

        if (allowDeselect && index == selectedIndex)
        {
            SetSelected(-1, true);
            return;
        }

        if (index == selectedIndex) return;
        SetSelected(index, true);
    }

    /// <summary>Programmatically set the selected index. Pass -1 to clear selection.</summary>
    public void SetSelected(int index, bool notify = true)
    {
        selectedIndex = (index >= 0 && index < buttons.Count) ? index : -1;
        ApplyVisuals();
        if (notify && onSelectionChanged != null)
            onSelectionChanged.Invoke(selectedIndex);
    }

    private void ApplyVisuals()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null) continue;
            var graphic = GetHighlightGraphic(buttons[i]);
            if (graphic != null)
                graphic.color = (i == selectedIndex) ? selectedColor : normalColor;
        }

        for (int i = 0; i < toggleFills.Count; i++)
        {
            if (toggleFills[i] == null) continue;
            toggleFills[i].SetFilled(i == selectedIndex);
        }
    }

    private Graphic GetHighlightGraphic(Button button)
    {
        if (!string.IsNullOrEmpty(highlightChildName))
        {
            var child = button.transform.Find(highlightChildName);
            if (child != null)
            {
                var g = child.GetComponent<Graphic>();
                if (g != null) return g;
            }
        }
        return button.targetGraphic;
    }
}
