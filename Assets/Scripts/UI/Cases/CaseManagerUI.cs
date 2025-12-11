using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Handles search/filtering for existing CaseCardUI children under contentParent.
/// It does NOT spawn or destroy cards; it just shows/hides them based on the search text.
/// </summary>
public class CaseManagerUI : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Parent that contains all the case rows (each with a CaseCardUI).")]
    public Transform contentParent;

    [Header("Search")]
    [Tooltip("InputField used to type the search query.")]
    public InputField searchInput;

    private readonly List<CaseCardUI> _cards = new List<CaseCardUI>();
    private string _currentSearch = string.Empty;

    private void Awake()
    {
        RebuildCardList();
    }

    private void OnEnable()
    {
        if (searchInput != null)
            searchInput.onValueChanged.AddListener(OnSearchChanged);

        ApplySearchFilter();
    }

    private void OnDisable()
    {
        if (searchInput != null)
            searchInput.onValueChanged.RemoveListener(OnSearchChanged);
    }

    /// <summary>
    /// Re-scan children under contentParent for CaseCardUI components.
    /// Call this if you add/remove case rows at runtime.
    /// </summary>
    public void RebuildCardList()
    {
        _cards.Clear();

        // Use contentParent if set, otherwise fall back to this panel's transform.
        Transform root = contentParent != null ? contentParent : transform;

        // true = include inactive children as well
        _cards.AddRange(root.GetComponentsInChildren<CaseCardUI>(true));
    }


    /// <summary>
    /// Public refresh (e.g. if you change names or add rows).
    /// </summary>
    public void Refresh()
    {
        RebuildCardList();
        ApplySearchFilter();
    }

    private void OnSearchChanged(string text)
    {
        _currentSearch = text ?? string.Empty;
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        if (_cards.Count == 0) return;

        string query = string.IsNullOrWhiteSpace(_currentSearch)
            ? null
            : _currentSearch.Trim().ToLowerInvariant();

        foreach (var card in _cards)
        {
            if (card == null) continue;

            bool show = true;

            if (!string.IsNullOrEmpty(query))
            {
                string name = null;

                if (card.data != null)
                    name = card.data.caseName;
                else if (card.nameText != null)
                    name = card.nameText.text;

                show = !string.IsNullOrEmpty(name) &&
                       name.ToLowerInvariant().Contains(query);
            }

            card.gameObject.SetActive(show);
        }
    }
}
