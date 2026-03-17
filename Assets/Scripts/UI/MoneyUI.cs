using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;

/// <summary>
/// UI controller for Money clicker. Shows balance, $ per click, $ per second, and combo multiplier.
/// Bind to texts in the Coin clicker panel.
/// </summary>
public class MoneyUI : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private Text balanceText;
    [SerializeField] private Text perClickText;
    [SerializeField] private Text perSecondText;
    [SerializeField] private Text comboText;

    [Header("Formats")]
    [SerializeField] private string balanceFormat = "${0:F2}";
    [SerializeField] private string perClickFormat = "+${0} per click";
    [SerializeField] private string perSecondFormat = "${0}/s";
    [SerializeField] private string comboFormat = "Combo: {0:F2}x";

    private ClickerController clicker => ClickerController.Instance;
    private BalanceManager balance => BalanceManager.Instance;
    private ComboSystem combo => GameManager.Instance?.Combo;

    private void OnEnable()
    {
        RefreshAll();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // Update fast-changing values smoothly
        UpdatePerSecond();
        UpdateCombo();
        // Also refresh balance and per-click so UI stays responsive even without events
        UpdateBalance();
        UpdatePerClick();
    }

    private void Subscribe()
    {
        if (balance != null)
        {
            balance.OnMoneyChanged.AddListener(OnBalanceChanged);
        }
        // Optional: update per click/MPS when upgrades change
        StatisticsManager.Instance?.OnStatisticChanged?.AddListener(OnStatisticChanged);
    }

    private void Unsubscribe()
    {
        if (balance != null)
        {
            balance.OnMoneyChanged.RemoveListener(OnBalanceChanged);
        }
        StatisticsManager.Instance?.OnStatisticChanged?.RemoveListener(OnStatisticChanged);
    }

    private void RefreshAll()
    {
        UpdateBalance();
        UpdatePerClick();
        UpdatePerSecond();
        UpdateCombo();
    }

    private void OnBalanceChanged(double newBalance)
    {
        UpdateBalance();
    }

    private void OnStatisticChanged(string key, object value)
    {
        // Convert to float when possible
        float f = 0f;
        if (value is float fv) f = fv;
        else if (value is double dv) f = (float)dv;
        else if (value is int iv) f = iv;

        if (key == "moneyPerClick") UpdatePerClick();
        if (key == "moneyPerSecond") UpdatePerSecond();
    }

    private void UpdateBalance()
    {
        if (balanceText == null || balance == null) return;
        balanceText.text = string.Format(balanceFormat, balance.CurrentMoney);
    }

    private void UpdatePerClick()
    {
        if (perClickText == null || clicker == null) return;
        perClickText.text = string.Format(perClickFormat, FormatDynamicTruncated(clicker.CurrentMoneyPerClick));
    }

    private void UpdatePerSecond()
    {
        if (perSecondText == null || balance == null) return;
        perSecondText.text = string.Format(perSecondFormat, FormatDynamicTruncated(balance.CurrentMoneyPerSecond));
    }

    private void UpdateCombo()
    {
        if (comboText == null || combo == null) return;

        string processedFormat = comboFormat.Replace("\\n", "\n");

        comboText.text = string.Format(processedFormat, combo.CurrentMultiplier);
    }

    private string FormatDynamicTruncated(float value)
    {
        float abs = Mathf.Abs(value);
        int decimals = abs >= 1f ? 2 : (abs >= 0.1f ? 3 : 4);

        double factor = Math.Pow(10, decimals);
        double truncated = Math.Truncate(value * factor) / factor;

        string pattern = "0." + new string('0', decimals);
        return truncated.ToString(pattern, CultureInfo.InvariantCulture);
    }
}
