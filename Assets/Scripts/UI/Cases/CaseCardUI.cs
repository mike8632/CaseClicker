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

    [Header("Open UI")]
    public Button openCaseButton;   // OPEN
    public Text openCaseText;       // optional label

    [Header("Formats")]
    public string countFormat     = "COUNT: {0}";
    public string priceFormat     = "BUY FOR ${0:F2}!";
    public string buyKeyFormat    = "BUY KEY - ${0:F2}!";
    public string sellCaseFormat  = "SELL FOR ${0:F2}!";
    public string openCaseFormat  = "OPEN";
    
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

        if (openCaseButton != null)
        {
            openCaseButton.onClick.RemoveAllListeners();
            openCaseButton.onClick.AddListener(OpenCase);
        }

        // Refresh after load/init to ensure counts from save are shown
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameInitialized.AddListener(OnGameInitialized);
            GameManager.Instance.OnCaseDropped.AddListener(OnCaseDropped);
        }
        if (CaseInventoryManager.Instance != null)
        {
            CaseInventoryManager.Instance.OnCaseCountChanged.AddListener(OnCaseCountChanged);
        }
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnLoadCompleted.AddListener(OnSaveLoadCompleted);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameInitialized.RemoveListener(OnGameInitialized);
            GameManager.Instance.OnCaseDropped.RemoveListener(OnCaseDropped);
        }
        if (CaseInventoryManager.Instance != null)
        {
            CaseInventoryManager.Instance.OnCaseCountChanged.RemoveListener(OnCaseCountChanged);
        }
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnLoadCompleted.RemoveListener(OnSaveLoadCompleted);
        }
    }

    private void OnGameInitialized()
    {
        Refresh();
    }

    private void OnSaveLoadCompleted()
    {
        Refresh();
    }

    private void OnCaseDropped(CaseData dropped)
    {
        if (dropped == null || data == null) return;
        if (dropped.caseId == data.caseId)
        {
            Refresh();
        }
    }

    private void OnCaseCountChanged(string caseId)
    {
        if (data == null) return;
        if (caseId == data.caseId)
        {
            Refresh();
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
            if (openCaseText)  openCaseText.text = openCaseFormat;

            if (buyButton)      buyButton.interactable      = false;
            if (buyKeyButton)   buyKeyButton.interactable   = false;
            if (sellCaseButton) sellCaseButton.interactable = false;
            if (openCaseButton) openCaseButton.interactable = false;
            if (lockOverlay)    lockOverlay.SetActive(true);
            return;
        }

        if (nameText) nameText.text = data.caseName;
        if (icon) icon.sprite = data.caseIcon;

        // Read counts from inventory
        int caseCount = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetCaseCount(data.caseId)
            : ownedCount;
        int keyCount = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetKeyCount(data.caseId)
            : 0;
        UpdateCount(caseCount);

        // Unlock state
        bool unlocked = CaseInventoryManager.Instance == null
            ? true
            : CaseInventoryManager.Instance.IsCaseUnlocked(data.caseId);

        if (lockOverlay) lockOverlay.SetActive(!unlocked);

        // Button visibility logic:
        // - If no cases owned: show BUY CASE
        // - If cases owned but no keys: show BUY KEY and SELL
        // - If cases owned and keys owned: show OPEN and SELL (hide BUY KEY)
        bool hasCase = unlocked && caseCount > 0;
        bool hasKey = unlocked && keyCount > 0;
        bool canBuyCase = unlocked && caseCount == 0;

        if (buyButton)
        {
            buyButton.gameObject.SetActive(canBuyCase);
            buyButton.interactable = canBuyCase;
        }

        if (buyKeyButton)
        {
            bool showBuyKey = hasCase && !hasKey;
            buyKeyButton.gameObject.SetActive(showBuyKey);
            buyKeyButton.interactable = showBuyKey;
        }

        if (openCaseButton)
        {
            bool showOpen = hasCase && hasKey;
            openCaseButton.gameObject.SetActive(showOpen);
            openCaseButton.interactable = showOpen;
            if (openCaseText) openCaseText.text = openCaseFormat;
        }

        if (sellCaseButton)
        {
            bool showSell = hasCase; // can sell if at least 1 case
            sellCaseButton.gameObject.SetActive(showSell);
            sellCaseButton.interactable = showSell;
        }

        // Prices
        float casePrice = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetCasePrice(data)
            : data.casePrice;
        if (priceText) priceText.text = string.Format(priceFormat, casePrice);

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
        CaseInventoryManager.Instance?.BuyCase(data);
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
        CaseInventoryManager.Instance?.SellCase(data);
        Refresh();
    }

    private void OpenCase()
    {
        if (data == null) return;
        CaseInventoryManager.Instance?.OpenCase(data);
        Refresh();
    }
}
