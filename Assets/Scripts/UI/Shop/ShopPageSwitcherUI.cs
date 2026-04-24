using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Switches between multiple shop pages using left/right arrows.
/// </summary>
public class ShopPageSwitcherUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;

    [Header("Pages")]
    [SerializeField] private List<GameObject> shopPages = new List<GameObject>();
    [SerializeField] private int startPageIndex = 0;
    [SerializeField] private bool wrapAround = true;

    [Header("Title (optional)")]
    [SerializeField] private TMP_Text pageTitleText;
    [SerializeField] private List<string> pageTitles = new List<string>();

    [Header("Legacy fallback (used if Pages list is empty)")]
    [SerializeField] private GameObject caseShopUi;
    [SerializeField] private GameObject keysShopUi;
    [SerializeField] private string casesTitle = "Cases";
    [SerializeField] private string keysTitle = "Keys";

    private int _currentPageIndex;

    private void Awake()
    {
        EnsurePagesConfigured();
        _currentPageIndex = Mathf.Clamp(startPageIndex, 0, Mathf.Max(0, shopPages.Count - 1));
        ApplyCurrentPage();
    }

    private void OnEnable()
    {
        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.RemoveListener(ShowPreviousPage);
            leftArrowButton.onClick.AddListener(ShowPreviousPage);
        }

        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.RemoveListener(ShowNextPage);
            rightArrowButton.onClick.AddListener(ShowNextPage);
        }

        ApplyCurrentPage();
    }

    private void OnDisable()
    {
        if (leftArrowButton != null)
            leftArrowButton.onClick.RemoveListener(ShowPreviousPage);

        if (rightArrowButton != null)
            rightArrowButton.onClick.RemoveListener(ShowNextPage);
    }

    public void ShowNextPage()
    {
        if (shopPages.Count <= 1) return;

        int next = _currentPageIndex + 1;
        if (next >= shopPages.Count)
        {
            if (!wrapAround) return;
            next = 0;
        }

        _currentPageIndex = next;
        ApplyCurrentPage();
    }

    public void ShowPreviousPage()
    {
        if (shopPages.Count <= 1) return;

        int prev = _currentPageIndex - 1;
        if (prev < 0)
        {
            if (!wrapAround) return;
            prev = shopPages.Count - 1;
        }

        _currentPageIndex = prev;
        ApplyCurrentPage();
    }

    private void EnsurePagesConfigured()
    {
        if (shopPages.Count > 0)
            return;

        if (caseShopUi != null)
            shopPages.Add(caseShopUi);

        if (keysShopUi != null)
            shopPages.Add(keysShopUi);

        if (pageTitles.Count == 0)
        {
            if (!string.IsNullOrEmpty(casesTitle)) pageTitles.Add(casesTitle);
            if (!string.IsNullOrEmpty(keysTitle)) pageTitles.Add(keysTitle);
        }
    }

    private void ApplyCurrentPage()
    {
        for (int i = 0; i < shopPages.Count; i++)
        {
            if (shopPages[i] != null)
                shopPages[i].SetActive(i == _currentPageIndex);
        }

        if (pageTitleText != null)
        {
            if (_currentPageIndex >= 0 && _currentPageIndex < pageTitles.Count)
                pageTitleText.text = pageTitles[_currentPageIndex];
        }

        bool hasMultiple = shopPages.Count > 1;
        if (leftArrowButton != null)
            leftArrowButton.interactable = hasMultiple && (wrapAround || _currentPageIndex > 0);
        if (rightArrowButton != null)
            rightArrowButton.interactable = hasMultiple && (wrapAround || _currentPageIndex < shopPages.Count - 1);
    }
}
