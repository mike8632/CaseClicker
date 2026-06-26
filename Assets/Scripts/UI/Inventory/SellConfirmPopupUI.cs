using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sell-confirmation popup. Place one instance in the scene (on a Canvas).
/// Call SellConfirmPopupUI.Instance.Show(entry, onConfirmed) from any sell button.
/// The panel is hidden by default; Show() activates it, Sell/Cancel hide it again.
/// </summary>
public class SellConfirmPopupUI : MonoBehaviour
{
    public static SellConfirmPopupUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Text Bindings  (assign either Text or TMP, or both)")]
    [SerializeField] private Text     itemNameText;
    [SerializeField] private TMP_Text itemNameTmpText;
    [SerializeField] private Text     priceText;
    [SerializeField] private TMP_Text priceTmpText;

    [Header("Buttons")]
    [SerializeField] private Button sellButton;
    [SerializeField] private Button cancelButton;

    [Header("Formats  (single item)")]
    [SerializeField] private string priceFormat    = "Sell for ${0:F2}";
    [SerializeField] private string titleFormat    = "Sell {0}?";

    [Header("Formats  (bulk)")]
    [Tooltip("{0} = item count")]
    [SerializeField] private string bulkTitleFormat = "Sell {0} items?";
    [Tooltip("{0} = total value")]
    [SerializeField] private string bulkPriceFormat = "Total: ${0:F2}";

    // Pending state
    private System.Action _onConfirm;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (sellButton   != null) sellButton.onClick.AddListener(HandleSell);
        if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancel);
    }

    private void OnDisable()
    {
        if (sellButton   != null) sellButton.onClick.RemoveListener(HandleSell);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancel);
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    /// <summary>Show the popup for a single skin. onConfirmed is called only if the player clicks Sell.</summary>
    public void Show(SkinInventoryEntry entry, System.Action onConfirmed)
    {
        if (entry == null) return;

        string name = !string.IsNullOrEmpty(entry.itemName)
            ? entry.itemName
            : $"{entry.weaponName} | {entry.skinName}";

        OpenPopup(
            string.Format(titleFormat, name),
            string.Format(priceFormat, entry.marketValue),
            onConfirmed);
    }

    /// <summary>Show the popup for a bulk sell. onConfirmed is called only if the player clicks Sell.</summary>
    public void ShowBulk(int count, float totalValue, System.Action onConfirmed)
    {
        if (count <= 0) return;

        OpenPopup(
            string.Format(bulkTitleFormat, count),
            string.Format(bulkPriceFormat, totalValue),
            onConfirmed);
    }

    private void OpenPopup(string title, string price, System.Action onConfirmed)
    {
        _onConfirm = onConfirmed;

        if (itemNameText    != null) itemNameText.text    = title;
        if (itemNameTmpText != null) itemNameTmpText.text = title;
        if (priceText       != null) priceText.text       = price;
        if (priceTmpText    != null) priceTmpText.text    = price;

        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        _onConfirm = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Button handlers ────────────────────────────────────────────────────────
    private void HandleSell()
    {
        var callback = _onConfirm;
        Hide();          // hide first so the panel is gone before any side-effects
        callback?.Invoke();
    }

    private void HandleCancel() => Hide();
}
