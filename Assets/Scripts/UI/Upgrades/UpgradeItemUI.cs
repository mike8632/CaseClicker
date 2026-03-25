using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UpgradeItemUI : MonoBehaviour
{
    // Match the names you used in the scene hierarchy
    public Image upgradeIcon;
    public Text upgradeNameText;
    public Text costPlaceHolder;
    public Text openedCasesPlaceHolder;
    public Text upgradeDescription;
    public Button buyButton;

    [Header("Binding")]
    public string upgradeId;

    [Header("Progression Visibility")]
    [Tooltip("Optional: this row is shown only after this upgrade id is maxed (e.g. Random Case 2 waits for Random Case 1).")]
    public string showAfterUpgradeId;
    [Tooltip("Hide this row after this upgrade is purchased/maxed.")]
    public bool hideWhenMaxed = true;

    private UpgradeDefinition def;
    private Coroutine _waitRoutine;
    private CanvasGroup _canvasGroup;
    private LayoutElement _layoutElement;

    private Color _enabledColor = new Color(0.388f, 0.573f, 0.176f); // green
    private Color _disabledColor = new Color(0.6f, 0.0f, 0.0f); // red

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _layoutElement = GetComponent<LayoutElement>();
        if (_layoutElement == null)
        {
            _layoutElement = gameObject.AddComponent<LayoutElement>();
        }
    }

    private void OnEnable()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        // Start waiting/subscribing to core managers
        if (_waitRoutine != null) StopCoroutine(_waitRoutine);
        _waitRoutine = StartCoroutine(WaitForSystemsThenInit());
    }

    private void OnDisable()
    {
        // Unsubscribe safely
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradesChanged.RemoveListener(Refresh);
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnStatisticChanged.RemoveListener(OnStatisticChanged);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCaseOpened.RemoveListener(OnCaseOpened);
            GameManager.Instance.OnMoneyChanged.RemoveListener(OnMoneyChanged);
        }

        if (_waitRoutine != null)
        {
            StopCoroutine(_waitRoutine);
            _waitRoutine = null;
        }
    }

    private IEnumerator WaitForSystemsThenInit()
    {
        // Wait for UpgradeManager
        while (UpgradeManager.Instance == null)
            yield return null;

        // Wait for StatisticsManager
        while (StatisticsManager.Instance == null)
            yield return null;

        // Wait for GameManager
        while (GameManager.Instance == null)
            yield return null;

        // Small extra frame
        yield return null;

        // Subscribe
        UpgradeManager.Instance.OnUpgradesChanged.AddListener(Refresh);
        StatisticsManager.Instance.OnStatisticChanged.AddListener(OnStatisticChanged);
        GameManager.Instance.OnCaseOpened.AddListener(OnCaseOpened);
        GameManager.Instance.OnMoneyChanged.AddListener(OnMoneyChanged);

        // Initial refresh
        Refresh();

        _waitRoutine = null;
    }

    private void OnStatisticChanged(string key, object value)
    {
        if (key == "casesOpened")
        {
            Refresh();
        }
    }

    private void OnCaseOpened(CaseData data)
    {
        Refresh();
    }

    private void OnMoneyChanged(double newAmount)
    {
        Refresh();
    }

    private bool IsUnlockedByPreviousUpgrade()
    {
        if (string.IsNullOrEmpty(showAfterUpgradeId)) return true;
        if (UpgradeManager.Instance == null) return false;
        var previous = UpgradeManager.Instance.GetUpgrade(showAfterUpgradeId);
        return previous != null && previous.IsMaxed;
    }

    private void SetRowVisible(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        if (_layoutElement != null)
        {
            _layoutElement.ignoreLayout = !visible;
        }

        if (_canvasGroup == null && _layoutElement == null)
        {
            // Fallback if no helper components are present
            gameObject.SetActive(visible);
        }
    }

    public void Refresh()
    {
        if (UpgradeManager.Instance == null) return;

        // Chain visibility check first
        if (!IsUnlockedByPreviousUpgrade())
        {
            SetRowVisible(false);
            return;
        }

        SetRowVisible(true);

        def = UpgradeManager.Instance.GetUpgrade(upgradeId);
        if (def == null) return;

        // Hide this row after purchase if configured
        if (hideWhenMaxed && def.IsMaxed)
        {
            SetRowVisible(false);
            return;
        }

        if (upgradeIcon != null) upgradeIcon.sprite = def.icon;
        if (upgradeNameText != null) upgradeNameText.text = def.displayName;
        if (costPlaceHolder != null) costPlaceHolder.text = def.IsMaxed ? "MAXED" : $"COST: ${def.CurrentCost:F2}";
        if (openedCasesPlaceHolder != null) openedCasesPlaceHolder.text = def.IsMaxed ? "" : UpgradeManager.Instance.GetRequirementText(def);
        if (upgradeDescription != null) upgradeDescription.text = def.IsMaxed ? "" : def.CurrentDescription;

        bool canBuy = !def.IsMaxed;

        // requirement
        int required = def.CurrentRequiredCases;
        int opened = StatisticsManager.Instance != null ? StatisticsManager.Instance.TotalCasesOpened : 0;
        if (opened < required) canBuy = false;

        // money
        double currentMoney = 0.0;
        if (BalanceManager.Instance != null && !def.IsMaxed)
        {
            currentMoney = BalanceManager.Instance.CurrentMoney;
            if (currentMoney < def.CurrentCost) canBuy = false;
        }

        if (buyButton != null)
        {
            buyButton.interactable = canBuy;
            var img = buyButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = canBuy ? _enabledColor : _disabledColor;
            }
        }

        Debug.Log($"[UpgradeItemUI] Refresh id={upgradeId} level={def.currentLevel+1} opened={opened}/{required} money={currentMoney:F2} cost={def.CurrentCost:F2} canBuy={canBuy}");
    }

    private void OnBuyClicked()
    {
        if (UpgradeManager.Instance == null) return;
        bool ok = UpgradeManager.Instance.PurchaseUpgrade(upgradeId);
        if (!ok)
        {
            Debug.Log("Could not buy upgrade: requirements not met or insufficient funds");
        }
    }
}
