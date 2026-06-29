using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any Button that should open ContainerInfoPanelUI showing the contents of a case.
/// Does NOT modify CaseCardUI itself — just reads its public data field.
///
/// Wiring:
///   1. Add this component to a Button on (or inside) a case card.
///   2. Assign caseCard → the CaseCardUI on the same card.
///   3. Leave button empty to auto-grab the Button on this same GameObject.
///   4. ContainerInfoPanelUI must exist somewhere in the scene.
/// </summary>
public class CaseContentsButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("CaseCardUI whose data will be shown. Assign in Inspector.")]
    [SerializeField] private CaseCardUI caseCard;
    [Tooltip("The Button to listen to. Auto-fetched from this GameObject if left empty.")]
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void HandleClick()
    {
        if (caseCard == null || caseCard.data == null)
        {
            Debug.LogWarning("[CaseContentsButton] No CaseCardUI or CaseData assigned.");
            return;
        }

        if (ContainerInfoPanelUI.Instance != null)
        {
            ContainerInfoPanelUI.Instance.Show(caseCard.data);
            return;
        }

        // Fallback to generic panel if ContainerInfoPanelUI is not in the scene
        if (ContentsPanelUI.Instance != null)
        {
            ContentsPanelUI.Instance.ShowCaseContents(caseCard.data);
            return;
        }

        Debug.LogWarning("[CaseContentsButton] Neither ContainerInfoPanelUI nor ContentsPanelUI is in the scene.");
    }
}
