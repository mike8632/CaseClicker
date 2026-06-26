using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays "Total: X / Y items" in the inventory header.
/// X = cards currently visible after search + filters.
/// Y = total skins owned.
/// 
/// Setup: Add this component to any GameObject in the inventory panel.
/// Assign counterText (or counterTextLegacy), inventoryUI, and optionally filterController.
/// </summary>
public class InventoryCounterUI : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Text     counterTextLegacy;

    [Header("References")]
    [SerializeField] private SkinInventoryUI          inventoryUI;
    [SerializeField] private InventoryFilterController filterController;

    [Header("Format")]
    [Tooltip("Use {0} for visible count and {1} for total count.")]
    [SerializeField] private string format = "Total: {0} / {1} items";

    private Coroutine _initRoutine;

    private void OnEnable()
    {
        if (_initRoutine != null) StopCoroutine(_initRoutine);
        _initRoutine = StartCoroutine(WaitAndSubscribe());
    }

    private void OnDisable()
    {
        if (_initRoutine != null) { StopCoroutine(_initRoutine); _initRoutine = null; }

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinAdded.RemoveListener(OnInventoryChanged);
            SkinInventoryManager.Instance.OnSkinRemoved.RemoveListener(OnInventoryChanged);
        }

        if (inventoryUI != null)
            inventoryUI.OnRebuildComplete -= Refresh;

        if (filterController != null)
            filterController.OnFiltersApplied -= Refresh;
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (SkinInventoryManager.Instance == null)
            yield return null;

        SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnInventoryChanged);
        SkinInventoryManager.Instance.OnSkinRemoved.AddListener(OnInventoryChanged);

        if (inventoryUI != null)
            inventoryUI.OnRebuildComplete += Refresh;

        if (filterController != null)
            filterController.OnFiltersApplied += Refresh;

        _initRoutine = null;
        Refresh();
    }

    private void OnInventoryChanged(SkinInventoryEntry _)
    {
        // Defer by one frame so Unity's deferred Destroy() calls have completed
        // and the card hierarchy matches the updated Entries list.
        StartCoroutine(RefreshNextFrame());
    }

    private System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null;
        Refresh();
    }

    public void Refresh()
    {
        int total   = SkinInventoryManager.Instance != null
                      ? SkinInventoryManager.Instance.Entries.Count
                      : 0;

        int visible = 0;
        if (inventoryUI != null)
        {
            var cards = inventoryUI.GetCards(includeInactive: true);
            foreach (var card in cards)
            {
                if (card != null && card.gameObject.activeSelf)
                    visible++;
            }
        }
        else
        {
            // No inventoryUI assigned — just show total
            visible = total;
        }

        string text = string.Format(format, visible, total);
        if (counterText != null)       counterText.text = text;
        if (counterTextLegacy != null)  counterTextLegacy.text = text;
    }
}
