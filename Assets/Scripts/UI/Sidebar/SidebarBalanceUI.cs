using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sidebar balance UI. Mirrors MoneyUI balance display for the sidebar.
/// Shows the player's current money and keeps it updated.
/// </summary>
public class SidebarBalanceUI : MonoBehaviour
{
    [Header("Text Reference")]
    [SerializeField] private Text balanceText;

    [Header("Format")]
    [SerializeField] private string balanceFormat = "${0:F2}";

    private BalanceManager balance => BalanceManager.Instance;

    private void OnEnable()
    {
        Refresh();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // Keep responsive even if events don't fire (e.g., frequent changes)
        UpdateBalance();
    }

    private void Subscribe()
    {
        if (balance != null)
        {
            balance.OnMoneyChanged.AddListener(OnBalanceChanged);
        }
    }

    private void Unsubscribe()
    {
        if (balance != null)
        {
            balance.OnMoneyChanged.RemoveListener(OnBalanceChanged);
        }
    }

    private void Refresh()
    {
        UpdateBalance();
    }

    private void OnBalanceChanged(double newBalance)
    {
        UpdateBalance();
    }

    private void UpdateBalance()
    {
        if (balanceText == null || balance == null) return;
        balanceText.text = string.Format(balanceFormat, balance.CurrentMoney);
    }
}
