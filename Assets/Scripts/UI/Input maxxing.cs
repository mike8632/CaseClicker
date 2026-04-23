using UnityEngine;
using System.Text;
using UnityEngine.UI;

[RequireComponent(typeof(InputField))]
public class BuyAmountHandler : MonoBehaviour
{
    [SerializeField] private InputField inputField;
    [SerializeField] private int maxDigits = 6;
    [SerializeField] private bool forceIntegerInput = true;

    public int minAmount = 1;
    public int maxAmount = 999999;

    private bool _isUpdating;

    private void Awake()
    {
        if (inputField == null)
            inputField = GetComponent<InputField>();

        if (inputField != null && forceIntegerInput)
            inputField.contentType = InputField.ContentType.IntegerNumber;
    }

    private void OnEnable()
    {
        if (inputField != null)
            inputField.onValueChanged.AddListener(HandleValueChanged);
    }

    private void OnDisable()
    {
        if (inputField != null)
            inputField.onValueChanged.RemoveListener(HandleValueChanged);
    }

    private void HandleValueChanged(string value)
    {
        if (_isUpdating || inputField == null)
            return;

        string sanitized = SanitizeDigits(value);
        if (sanitized == value)
            return;

        _isUpdating = true;
        inputField.SetTextWithoutNotify(sanitized);
        _isUpdating = false;
    }

    private string SanitizeDigits(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        int limit = Mathf.Max(1, maxDigits);
        var builder = new StringBuilder(limit);

        for (int i = 0; i < value.Length && builder.Length < limit; i++)
        {
            char c = value[i];
            if (char.IsDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    public int GetAmount()
    {
        if (inputField == null)
            return minAmount;

        if (!int.TryParse(inputField.text, out int amount))
            amount = minAmount;

        amount = Mathf.Clamp(amount, minAmount, maxAmount);

        // update the UI so player sees corrected value
        string clampedText = amount.ToString();
        if (clampedText.Length > maxDigits)
            clampedText = clampedText.Substring(0, maxDigits);

        inputField.SetTextWithoutNotify(clampedText);

        return amount;
    }
}