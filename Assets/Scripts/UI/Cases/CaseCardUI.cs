using UnityEngine;
using UnityEngine.UI;

public class CaseCardUI : MonoBehaviour
{
    [Header("Bindings")]
    public Image icon;
    public Text nameText;
    public Text countText;

    [Header("Main Buy UI")]
    public Text priceText;          // BUY FOR $0.60!
    public Button buyButton;        // buy case
    public GameObject lockOverlay;  // NOT UNLOCKED overlay

    [Header("Key & Sell UI")]
    public Button buyKeyButton;     // BUY KEY - $2.50!
    public Text buyKeyText;
    public Button sellCaseButton;   // SELL FOR $0.15
    public Text sellCaseText;

    [Header("Formats")]
    public string countFormat     = "COUNT: {0}";
    public string priceFormat     = "BUY FOR ${0:F2}!";
    public string buyKeyFormat    = "BUY KEY - ${0:F2}!";
    public string sellCaseFormat  = "SELL FOR ${0:F2}!";
    
    [Header("Data")]
    public CaseData data;

    private int ownedCount;

    private void OnEnable()
    {
        Refresh();

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuyCase);
        }

        if (buyKeyButton != null)
        {
            buyKeyButton.onClick.RemoveAllListeners();
            buyKeyButton.onClick.AddListener(BuyKey);
        }

        if (sellCaseButton != null)
        {
            sellCaseButton.onClick.RemoveAllListeners();
            sellCaseButton.onClick.AddListener(SellCase);
        }
    }

    public void SetData(CaseData caseData)
    {
        data = caseData;
        Refresh();
    }

    public void Refresh()
    {
        if (data == null)
        {
            if (nameText) nameText.text = "Unknown";
            if (icon) icon.sprite = null;
            if (countText) countText.text = string.Format(countFormat, 0);

            if (priceText)     priceText.text    = string.Format(priceFormat, 0f);
            if (buyKeyText)    buyKeyText.text   = string.Format(buyKeyFormat, 0f);
            if (sellCaseText)  sellCaseText.text = string.Format(sellCaseFormat, 0f);

            if (buyButton)      buyButton.interactable      = false;
            if (buyKeyButton)   buyKeyButton.interactable   = false;
            if (sellCaseButton) sellCaseButton.interactable = false;
            if (lockOverlay)    lockOverlay.SetActive(true);
            return;
        }

        if (nameText) nameText.text = data.caseName;
        if (icon) icon.sprite = data.caseIcon;

        // Read count from inventory
        int count = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetCaseCount(data.caseId)
            : ownedCount;
        UpdateCount(count);

        // Unlock state
        bool unlocked = CaseInventoryManager.Instance == null
            ? true
            : CaseInventoryManager.Instance.IsCaseUnlocked(data.caseId);

        if (lockOverlay) lockOverlay.SetActive(!unlocked);

        // Show/hide buttons based on count:
        //  - COUNT == 0  -> only BUY CASE
        //  - COUNT >= 1  -> only BUY KEY + SELL
        bool canBuyCase = unlocked && count == 0;
        bool canUseKeyOrSell = unlocked && count > 0;

        if (buyButton)
        {
            buyButton.gameObject.SetActive(canBuyCase);
            buyButton.interactable = canBuyCase;
        }

        if (buyKeyButton)
        {
            buyKeyButton.gameObject.SetActive(canUseKeyOrSell);
            buyKeyButton.interactable = canUseKeyOrSell;
        }

        if (sellCaseButton)
        {
            sellCaseButton.gameObject.SetActive(canUseKeyOrSell);
            // can only sell if at least 1 owned
            sellCaseButton.interactable = canUseKeyOrSell;
        }

        // Prices
        float casePrice = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetCasePrice(data)
            : data.casePrice;

        if (priceText)
            priceText.text = string.Format(priceFormat, casePrice);


        float keyPrice = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetKeyPrice(data.caseId, data.keyPrice)
            : data.keyPrice;
        if (buyKeyText) buyKeyText.text = string.Format(buyKeyFormat, keyPrice);

        float sellPrice = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetCaseSellPrice(data)
            : data.casePrice / 4f;
        if (sellCaseText) sellCaseText.text = string.Format(sellCaseFormat, sellPrice);
    }

    public void UpdateCount(int count)
    {
        ownedCount = count;
        if (countText) countText.text = string.Format(countFormat, ownedCount);
    }

    private void BuyCase()
    {
        if (data == null) return;
        CaseInventoryManager.Instance?.BuyCase(data);  // new method below
        Refresh();
    }

    private void BuyKey()
    {
        if (data == null) return;
        CaseInventoryManager.Instance?.BuyCaseKey(data);
        Refresh();
    }

    private void SellCase()
    {
        if (data == null) return;
        CaseInventoryManager.Instance?.SellCase(data); // new method below
        Refresh();
    }
}
