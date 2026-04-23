using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic shop row for buying cases/keys in amounts.
/// Attach this to one shop row and bind UI references in Inspector.
/// </summary>
public class ShopCaseRowUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Image icon;
    [SerializeField] private Text nameText;
    [SerializeField] private Text countText;

    [SerializeField] private InputField amountInput;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    [SerializeField] private Text unitPriceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Text buyButtonText;

    [Header("Formats")]
    [SerializeField] private string countFormat = "COUNT:{0}";
    [SerializeField] private string priceFormat = "${0:F2}";
    [SerializeField] private string buyFormat = "Buy {0} FOR ${1:F2}";

    [Header("Data")]
    [SerializeField] private string caseId;
    [SerializeField] private CaseData data;
    [SerializeField] private bool buyKeysInsteadOfCases = false;
    [SerializeField] private int minAmount = 1;
    [SerializeField] private int maxAmount = 999999;
    [SerializeField] private int maxDigits = 6;

    [Header("Optional Lock UI")]
    [SerializeField] private GameObject lockOverlay;

    private int _amount = 1;
    private bool _syncingInput;

    private void Awake()
    {
        if (string.IsNullOrEmpty(caseId) && data != null)
            caseId = data.caseId;

        _amount = Mathf.Clamp(_amount, Mathf.Max(1, minAmount), Mathf.Max(minAmount, maxAmount));
        if (amountInput != null)
            amountInput.contentType = InputField.ContentType.IntegerNumber;
    }

    private void OnEnable()
    {
        if (minusButton != null)
        {
            minusButton.onClick.RemoveListener(OnMinusClicked);
            minusButton.onClick.AddListener(OnMinusClicked);
        }

        if (plusButton != null)
        {
            plusButton.onClick.RemoveListener(OnPlusClicked);
            plusButton.onClick.AddListener(OnPlusClicked);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnBuyClicked);
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        if (amountInput != null)
        {
            amountInput.onValueChanged.RemoveListener(OnAmountChanged);
            amountInput.onValueChanged.AddListener(OnAmountChanged);
        }

        if (CaseInventoryManager.Instance != null)
            CaseInventoryManager.Instance.OnCaseCountChanged.AddListener(OnCaseCountChanged);

        if (BalanceManager.Instance != null)
            BalanceManager.Instance.OnMoneyChanged.AddListener(OnMoneyChanged);

        RefreshAll();
    }

    private void OnDisable()
    {
        if (minusButton != null) minusButton.onClick.RemoveListener(OnMinusClicked);
        if (plusButton != null) plusButton.onClick.RemoveListener(OnPlusClicked);
        if (buyButton != null) buyButton.onClick.RemoveListener(OnBuyClicked);
        if (amountInput != null) amountInput.onValueChanged.RemoveListener(OnAmountChanged);

        if (CaseInventoryManager.Instance != null)
            CaseInventoryManager.Instance.OnCaseCountChanged.RemoveListener(OnCaseCountChanged);

        if (BalanceManager.Instance != null)
            BalanceManager.Instance.OnMoneyChanged.RemoveListener(OnMoneyChanged);
    }

    public void SetData(CaseData caseData)
    {
        data = caseData;
        if (caseData != null)
            caseId = caseData.caseId;

        RefreshAll();
    }

    private void OnCaseCountChanged(string caseId)
    {
        string currentCaseId = GetCurrentCaseId();
        if (string.IsNullOrEmpty(currentCaseId) || string.IsNullOrEmpty(caseId)) return;
        if (!string.Equals(currentCaseId, caseId.Trim(), System.StringComparison.Ordinal)) return;
        RefreshAll();
    }

    private void OnMoneyChanged(double _)
    {
        RefreshBuyState();
    }

    private void OnMinusClicked()
    {
        SetAmount(_amount - 1);
    }

    private void OnPlusClicked()
    {
        SetAmount(_amount + 1);
    }

    private void OnAmountChanged(string value)
    {
        if (_syncingInput) return;

        string sanitized = SanitizeDigits(value, Mathf.Max(1, maxDigits));
        if (string.IsNullOrEmpty(sanitized))
        {
            SetAmount(minAmount);
            return;
        }

        if (!int.TryParse(sanitized, out int parsed))
            parsed = minAmount;

        SetAmount(parsed);
    }

    private void SetAmount(int value)
    {
        int min = Mathf.Max(1, minAmount);
        int max = Mathf.Max(min, maxAmount);
        _amount = Mathf.Clamp(value, min, max);

        if (amountInput != null)
        {
            _syncingInput = true;
            amountInput.SetTextWithoutNotify(_amount.ToString());
            _syncingInput = false;
        }

        RefreshBuyState();
    }

    private void RefreshAll()
    {
        data = ResolveCaseData();

        if (data == null)
        {
            if (nameText != null) nameText.text = string.IsNullOrEmpty(caseId) ? "Unknown" : caseId;
            if (icon != null) icon.sprite = null;
            if (countText != null) countText.text = string.Format(countFormat, 0);
            if (unitPriceText != null) unitPriceText.text = string.Format(priceFormat, 0f);
            if (buyButtonText != null) buyButtonText.text = string.Format(buyFormat, 0, 0f);
            if (buyButton != null) buyButton.interactable = false;
            if (lockOverlay != null) lockOverlay.SetActive(true);
            return;
        }

        if (nameText != null) nameText.text = data.caseName;
        if (icon != null) icon.sprite = data.caseIcon;

        int count = GetOwnedCount();
        if (countText != null) countText.text = string.Format(countFormat, count);

        float unitPrice = GetUnitPrice();
        if (unitPriceText != null) unitPriceText.text = string.Format(priceFormat, unitPrice);

        SetAmount(_amount);
    }

    private void RefreshBuyState()
    {
        if (data == null)
        {
            if (buyButton != null) buyButton.interactable = false;
            return;
        }

        float unitPrice = GetUnitPrice();
        float totalPrice = Mathf.Max(0f, unitPrice) * _amount;

        string currentCaseId = GetCurrentCaseId();
        bool unlocked = CaseInventoryManager.Instance != null &&
                        !string.IsNullOrEmpty(currentCaseId) &&
                        CaseInventoryManager.Instance.IsCaseUnlocked(currentCaseId);
        bool canAfford = BalanceManager.Instance != null && BalanceManager.Instance.CanAfford(totalPrice);
        bool canBuy = unlocked && unitPrice > 0f && _amount > 0 && canAfford;

        if (lockOverlay != null)
            lockOverlay.SetActive(!unlocked);

        if (buyButton != null)
            buyButton.interactable = canBuy;

        if (buyButtonText != null)
            buyButtonText.text = string.Format(buyFormat, _amount, totalPrice);

        if (minusButton != null)
            minusButton.interactable = _amount > Mathf.Max(1, minAmount);

        if (plusButton != null)
            plusButton.interactable = _amount < Mathf.Max(Mathf.Max(1, minAmount), maxAmount);
    }

    private void OnBuyClicked()
    {
        data = ResolveCaseData();
        if (data == null || CaseInventoryManager.Instance == null) return;

        int bought = 0;
        for (int i = 0; i < _amount; i++)
        {
            bool ok = buyKeysInsteadOfCases
                ? CaseInventoryManager.Instance.TryBuyCaseKey(data)
                : CaseInventoryManager.Instance.TryBuyCase(data);

            if (!ok) break;
            bought++;
        }

        if (bought <= 0)
            return;

        RefreshAll();
    }

    private int GetOwnedCount()
    {
        string currentCaseId = GetCurrentCaseId();
        if (CaseInventoryManager.Instance == null || string.IsNullOrEmpty(currentCaseId)) return 0;

        return buyKeysInsteadOfCases
            ? CaseInventoryManager.Instance.GetKeyCount(currentCaseId)
            : CaseInventoryManager.Instance.GetCaseCount(currentCaseId);
    }

    private float GetUnitPrice()
    {
        if (data == null) return 0f;
        if (CaseInventoryManager.Instance == null)
            return buyKeysInsteadOfCases ? data.keyPrice : data.casePrice;

        return buyKeysInsteadOfCases
            ? CaseInventoryManager.Instance.GetKeyPrice(data.caseId, data.keyPrice)
            : CaseInventoryManager.Instance.GetCasePrice(data);
    }

    private string GetCurrentCaseId()
    {
        if (!string.IsNullOrEmpty(caseId))
            return caseId.Trim();

        return data != null ? data.caseId?.Trim() : null;
    }

    private CaseData ResolveCaseData()
    {
        string id = GetCurrentCaseId();
        if (string.IsNullOrEmpty(id))
            return null;

        if (CaseCardUI.TryGetCaseDataById(id, out var resolved))
        {
            data = resolved;
            return data;
        }

        // If explicit caseId is configured, do not fall back to unrelated inline data.
        if (!string.IsNullOrEmpty(caseId))
            return null;

        return data;
    }

    private static string SanitizeDigits(string value, int maxDigitCount)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(maxDigitCount);
        for (int i = 0; i < value.Length && sb.Length < maxDigitCount; i++)
        {
            char c = value[i];
            if (char.IsDigit(c))
                sb.Append(c);
        }

        return sb.ToString();
    }
}
