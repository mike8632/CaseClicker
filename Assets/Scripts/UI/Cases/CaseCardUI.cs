using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component for a single case card UI.
/// Expected prefab structure:
/// - root has `CaseCardUI`
/// - child `Image` for icon
/// - child `Text` (or TMP) for name
/// - child `Text` (or TMP) for count
/// Assign these in the Inspector.
/// </summary>
public class CaseCardUI : MonoBehaviour
{
    [Header("Bindings")]
    public Image icon;
    public Text nameText;
    public Text countText;

    [Header("Data")]
    public CaseData data;

    // Optional: owned count, if tracked elsewhere you can update via method
    private int ownedCount;

    public void SetData(CaseData caseData)
    {
        data = caseData;
        if (data == null)
        {
            if (nameText) nameText.text = "Unknown";
            if (icon) icon.sprite = null;
            if (countText) countText.text = "0";
            return;
        }

        if (nameText) nameText.text = data.caseName;
        if (icon) icon.sprite = data.caseIcon;
        UpdateCount(ownedCount);
    }

    public void UpdateCount(int count)
    {
        ownedCount = count;
        if (countText) countText.text = ownedCount.ToString();
    }
}
