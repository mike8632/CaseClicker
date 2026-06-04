using TMPro;
using UnityEngine;

/// <summary>
/// Restricts a TMP_InputField so the user can only type numbers
/// (digits and, optionally, a single decimal point).
/// Letters and other characters are rejected as they are typed,
/// mirroring the behaviour used by the float range fields.
/// Attach this to the same GameObject as the TMP_InputField,
/// or assign the field manually in the inspector.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class NumericInputFilter : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [Tooltip("Allow a single decimal point (e.g. for prices like 12.50). Disable for whole numbers only.")]
    [SerializeField] private bool allowDecimalPoint = true;

    private void Awake()
    {
        if (inputField == null)
            inputField = GetComponent<TMP_InputField>();

        if (inputField == null)
            return;

        inputField.contentType = allowDecimalPoint
            ? TMP_InputField.ContentType.DecimalNumber
            : TMP_InputField.ContentType.IntegerNumber;

        inputField.characterValidation = allowDecimalPoint
            ? TMP_InputField.CharacterValidation.Decimal
            : TMP_InputField.CharacterValidation.Integer;

        inputField.onValidateInput = ValidateChar;
    }

    private char ValidateChar(string text, int charIndex, char addedChar)
    {
        if (char.IsDigit(addedChar))
            return addedChar;

        if (allowDecimalPoint && addedChar == '.' && !text.Contains("."))
            return addedChar;

        return '\0';
    }
}
