using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the total market value of all skins currently in the inventory.
/// Locked skins are included (they are still owned and valued).
/// Updates automatically when skins are added, removed, or repriced.
///
/// Wiring: attach to any GameObject that has a Text or TMP_Text child.
/// Assign valueText / valueTmpText in the Inspector. Both are optional — assign either or both.
/// </summary>
public class InventoryValueUI : MonoBehaviour
{
    [Header("Text (assign Text, TMP_Text, or both)")]
    [SerializeField] private Text      valueText;
    [SerializeField] private TMP_Text  valueTmpText;

    [Header("Format")]
    [Tooltip("Format string. {0} = total value (double). Example: 'Value: ${0:F2}'")]
    [SerializeField] private string format = "Value: ${0:F2}";

    private Coroutine _waitRoutine;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (_waitRoutine != null) StopCoroutine(_waitRoutine);
        _waitRoutine = StartCoroutine(WaitThenSubscribe());
    }

    private void OnDisable()
    {
        if (_waitRoutine != null) { StopCoroutine(_waitRoutine); _waitRoutine = null; }

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinAdded.RemoveListener(OnInventoryChanged);
            SkinInventoryManager.Instance.OnSkinRemoved.RemoveListener(OnInventoryChanged);
            SkinInventoryManager.Instance.OnSkinValueChanged.RemoveListener(OnInventoryChanged);
        }

        if (SaveSystem.Instance != null)
            SaveSystem.Instance.OnLoadCompleted.RemoveListener(Refresh);
    }

    // ── Initialization ─────────────────────────────────────────────────────────

    private IEnumerator WaitThenSubscribe()
    {
        while (SkinInventoryManager.Instance == null) yield return null;
        while (SaveSystem.Instance == null)           yield return null;
        yield return null; // one extra frame so inventory is fully populated after load

        SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnInventoryChanged);
        SkinInventoryManager.Instance.OnSkinRemoved.AddListener(OnInventoryChanged);
        SkinInventoryManager.Instance.OnSkinValueChanged.AddListener(OnInventoryChanged);
        SaveSystem.Instance.OnLoadCompleted.AddListener(Refresh);

        Refresh();
        _waitRoutine = null;
    }

    // ── Event handlers ─────────────────────────────────────────────────────────

    private void OnInventoryChanged(SkinInventoryEntry _) => Refresh();

    // ── Core refresh ───────────────────────────────────────────────────────────

    private void Refresh()
    {
        double total = 0;

        if (SkinInventoryManager.Instance != null)
        {
            foreach (var entry in SkinInventoryManager.Instance.Entries)
            {
                if (entry != null)
                    total += entry.marketValue;
            }
        }

        string text = string.Format(format, total);
        if (valueText    != null) valueText.text    = text;
        if (valueTmpText != null) valueTmpText.text = text;
    }
}
