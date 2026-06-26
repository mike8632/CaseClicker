using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// General-purpose confirmation popup singleton.
/// Place one instance under the root Canvas (always visible, not inside any panel).
/// Call ConfirmDialogUI.Instance.Show(title, message, onConfirm) from any script.
/// </summary>
public class ConfirmDialogUI : MonoBehaviour
{
    public static ConfirmDialogUI Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("The child panel to show/hide. Should NOT be this GameObject itself.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Text (assign Text, TMP_Text, or both)")]
    [SerializeField] private Text     titleText;
    [SerializeField] private TMP_Text titleTmpText;
    [SerializeField] private Text     messageText;
    [SerializeField] private TMP_Text messageTmpText;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private System.Action _onConfirm;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(HandleCancel);
    }

    private void OnDisable()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
        if (cancelButton  != null) cancelButton.onClick.RemoveListener(HandleCancel);
    }

    /// <summary>
    /// Show the dialog. onConfirm is called only if the player clicks the confirm button.
    /// </summary>
    public void Show(string title, string message, System.Action onConfirm)
    {
        _onConfirm = onConfirm;
        SetText(titleText,   titleTmpText,   title);
        SetText(messageText, messageTmpText, message);
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        _onConfirm = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void HandleConfirm()
    {
        var callback = _onConfirm;
        Hide();          // hide before callback so panel is gone during side-effects
        callback?.Invoke();
    }

    private void HandleCancel() => Hide();

    private static void SetText(Text legacy, TMP_Text tmp, string value)
    {
        if (legacy != null) legacy.text = value;
        if (tmp    != null) tmp.text    = value;
    }
}
