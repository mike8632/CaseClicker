using UnityEngine;
using UnityEngine.UI;

public class UpgradeItemUI : MonoBehaviour
{
    public Image icon;
    public Text titleText;
    public Text costText;
    public Text requirementText;
    public Text descriptionText;
    public Button buyButton;

    [Header("Binding")]
    public string upgradeId;

    private UpgradeDefinition def;

    private void OnEnable()
    {
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
        Refresh();
        UpgradeManager.Instance?.OnUpgradesChanged.AddListener(Refresh);
    }

    private void OnDisable()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradesChanged.RemoveListener(Refresh);
    }

    public void Refresh()
    {
        def = UpgradeManager.Instance?.GetUpgrade(upgradeId);
        if (def == null) return;
        if (icon) icon.sprite = def.icon;
        if (titleText) titleText.text = def.displayName + " " + (def.currentLevel + 1);
        if (costText) costText.text = def.IsMaxed ? "MAXED" : $"COST: ${def.CurrentCost:F2}";
        if (requirementText) requirementText.text = def.IsMaxed ? "" : UpgradeManager.Instance.GetRequirementText(def);
        if (descriptionText) descriptionText.text = def.IsMaxed ? "" : def.CurrentDescription;

        bool canBuy = !def.IsMaxed;
        // requirement
        int required = def.CurrentRequiredCases;
        int opened = StatisticsManager.Instance != null ? StatisticsManager.Instance.TotalCasesOpened : 0;
        if (opened < required) canBuy = false;
        // money
        if (BalanceManager.Instance != null && !def.IsMaxed)
        {
            if (BalanceManager.Instance.CurrentMoney < def.CurrentCost) canBuy = false;
        }

        buyButton.interactable = canBuy;
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
