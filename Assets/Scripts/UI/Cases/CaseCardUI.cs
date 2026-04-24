using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CaseCardUI : MonoBehaviour
{
    private static readonly Dictionary<string, CaseData> CaseDataById = new Dictionary<string, CaseData>();
    private static readonly Dictionary<string, CaseData> CaseDataByName = new Dictionary<string, CaseData>();

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

    public static bool TryGetCaseDataById(string caseId, out CaseData caseData)
    {
        caseData = null;
        if (string.IsNullOrEmpty(caseId)) return false;

        string id = caseId.Trim();
        EnsureCaseDataRegistry();
        if (CaseDataById.TryGetValue(id, out caseData))
            return true;

        RebuildCaseDataRegistry();
        return CaseDataById.TryGetValue(id, out caseData);
    }

    public static bool TryGetCaseDataByName(string caseName, out CaseData caseData)
    {
        caseData = null;
        string normalizedName = NormalizeCaseName(caseName);
        if (string.IsNullOrEmpty(normalizedName)) return false;

        EnsureCaseDataRegistry();
        if (CaseDataByName.TryGetValue(normalizedName, out caseData))
            return true;

        RebuildCaseDataRegistry();
        return CaseDataByName.TryGetValue(normalizedName, out caseData);
    }

    private void OnEnable()
    {
        RegisterThisCaseData();
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
        RegisterThisCaseData();
        Refresh();
    }

    private static void EnsureCaseDataRegistry()
    {
        if (CaseDataById.Count > 0)
            return;

        RebuildCaseDataRegistry();
    }

    private static void RebuildCaseDataRegistry()
    {
        CaseDataById.Clear();
        CaseDataByName.Clear();

        var cards = Resources.FindObjectsOfTypeAll<CaseCardUI>();
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            if (card == null || card.data == null) continue;
            if (!card.gameObject.scene.IsValid()) continue;

            RegisterCaseData(card.data);
        }
    }

    private void RegisterThisCaseData()
    {
        RegisterCaseData(data);
    }

    private static void RegisterCaseData(CaseData caseData)
    {
        if (caseData == null || string.IsNullOrEmpty(caseData.caseId))
            return;

        string id = caseData.caseId.Trim();
        CaseDataById[id] = caseData;

        string normalizedName = NormalizeCaseName(caseData.caseName);
        if (!string.IsNullOrEmpty(normalizedName))
            CaseDataByName[normalizedName] = caseData;
    }

    private static string NormalizeCaseName(string caseName)
    {
        if (string.IsNullOrWhiteSpace(caseName))
            return null;

        return caseName.Trim().ToLowerInvariant();
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

        int caseCount = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetCaseCount(data.caseId)
            : ownedCount;
        int keyCount = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.GetKeyCount(data.caseId)
            : 0;
        UpdateCount(caseCount);

        // Unlock state: default to true until inventory is ready
        bool unlocked = true;
        if (CaseInventoryManager.Instance != null)
        {
            unlocked = CaseInventoryManager.Instance.IsCaseUnlocked(data.caseId);
        }

        if (lockOverlay) lockOverlay.SetActive(!unlocked);

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
            bool showSell = hasCase;
            sellCaseButton.gameObject.SetActive(showSell);
            sellCaseButton.interactable = showSell;
        }

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

    private void Update()
    {
        if (data == null) return;
        var inv = CaseInventoryManager.Instance;
        if (inv == null) return;
        int current = inv.GetCaseCount(data.caseId);
        if (current != ownedCount)
        {
            // Use full Refresh to update button visibility and other UI states
            Refresh();
        }
    }
}
