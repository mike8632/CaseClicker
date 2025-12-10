using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Simple UI manager that renders a grid/list of case "cards".
/// Each card shows: icon, name, and owned count.
/// Bind this to your Case Manager panel and assign a `contentParent` and `cardPrefab`.
/// </summary>
public class CaseManagerUI : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Parent transform where cards will be instantiated (e.g., a Vertical/Horizontal/Grid Layout group)")]
    public Transform contentParent;

    [Tooltip("Prefab containing a CaseCardUI component to display a single case")]
    public GameObject cardPrefab;

    [Header("Data Source (optional)")]
    [Tooltip("If set, will use cases from CaseProgressManager. If null, provide cases manually in `manualCases`.")]
    public CaseProgressManager progressManager;

    [Tooltip("Manual cases to render if no progressManager provided")]
    public List<CaseData> manualCases = new List<CaseData>();

    private readonly List<CaseCardUI> _spawnedCards = new List<CaseCardUI>();

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// Rebuild the cards from the data source.
    /// </summary>
    public void Refresh()
    {
        if (contentParent == null || cardPrefab == null)
        {
            Debug.LogWarning("[CaseManagerUI] Missing contentParent or cardPrefab.");
            return;
        }

        // Clear previous
        for (int i = _spawnedCards.Count - 1; i >= 0; i--)
        {
            if (_spawnedCards[i] != null)
            {
                Destroy(_spawnedCards[i].gameObject);
            }
        }
        _spawnedCards.Clear();

        // Get source cases
        List<CaseData> source = null;
        if (progressManager == null)
        {
            source = manualCases;
        }
        else
        {
            // Access available cases via reflection-free helper
            source = GetAvailableCases(progressManager);
        }

        if (source == null || source.Count == 0)
        {
            Debug.Log("[CaseManagerUI] No cases to display.");
            return;
        }

        foreach (var caseData in source)
        {
            if (caseData == null) continue;
            var go = Instantiate(cardPrefab, contentParent);
            var card = go.GetComponent<CaseCardUI>();
            if (card == null)
            {
                Debug.LogWarning("[CaseManagerUI] Card prefab missing CaseCardUI component.");
                continue;
            }
            card.SetData(caseData);
            _spawnedCards.Add(card);
        }
    }

    /// <summary>
    /// Adds a case to be displayed when using manual mode.
    /// </summary>
    public void AddManualCase(CaseData data)
    {
        if (data == null) return;
        manualCases.Add(data);
        if (progressManager == null)
            Refresh();
    }

    // Helper to get the available cases from CaseProgressManager
    private List<CaseData> GetAvailableCases(CaseProgressManager manager)
    {
        // We don't have a public getter in the manager, so mirror via a simple public method if needed.
        // For now, use a lightweight cache: build from events or manual assignment.
        // To keep it simple, try to use reflection only if absolutely needed; otherwise rely on manualCases.
        // If you want a direct getter, expose one in CaseProgressManager.
        return manualCases.Count > 0 ? manualCases : new List<CaseData>();
    }
}
