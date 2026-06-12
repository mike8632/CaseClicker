using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates a new UI card for each dropped skin item.
/// Assign contentParent and cardPrefab (with SkinInventoryCardUI) in inspector.
/// </summary>
public class SkinInventoryUI : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private bool newestOnTop = true;
    [SerializeField] private SkinInventoryDetailUI detailPanel;
    [SerializeField] private bool autoFindDetailPanel = true;
    [SerializeField] private bool enableDetailPanel = true;
    [Header("Search")]
    [SerializeField] private InputField searchInput;
    [SerializeField] private TMP_InputField searchInputTmp;
    [Header("Lifecycle")]
    [SerializeField] private bool clearCardsOnDisable = false;
    [SerializeField] private bool rebuildOnTabShow = false;
    [Header("Card Behavior")]
    [SerializeField] private SkinInventoryCardUI.CardClickMode defaultCardClickMode = SkinInventoryCardUI.CardClickMode.Normal;

    private Coroutine initRoutine;
    private SkinInventoryCardUI sceneTemplateCard;
    private string searchFilter;
    private bool _liveActive = false;
    private TabPanel _parentTabPanel;

    public event System.Action<SkinInventoryCardUI> CardSpawned;
    /// <summary>Fired after cards are cleared (before new ones spawn). Subscribe to clean up stale card references.</summary>
    public event System.Action OnCardsCleared;

    public SkinInventoryCardUI[] GetCards(bool includeInactive = false)
    {
        if (contentParent == null)
            return System.Array.Empty<SkinInventoryCardUI>();

        return contentParent.GetComponentsInChildren<SkinInventoryCardUI>(includeInactive);
    }

    private void OnEnable()
    {
        ResolveDetailPanel();
        if (initRoutine != null) StopCoroutine(initRoutine);
        initRoutine = StartCoroutine(WaitThenSubscribe());

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            searchInput.onValueChanged.AddListener(HandleSearchChanged);
        }

        if (searchInputTmp != null)
        {
            searchInputTmp.onValueChanged.RemoveListener(HandleSearchChanged);
            searchInputTmp.onValueChanged.AddListener(HandleSearchChanged);
        }
    }

    private void OnDisable()
    {
        if (initRoutine != null)
        {
            StopCoroutine(initRoutine);
            initRoutine = null;
        }

        if (clearCardsOnDisable)
            ClearSpawnedCards();

        if (rebuildOnTabShow)
        {
            if (SidebarController.Instance != null)
            {
                SidebarController.Instance.OnTabChanged.RemoveListener(HandleTabChanged);
                SidebarController.Instance.OnTabClosed.RemoveListener(HandleTabClosed);
            }
            DeactivateLive();
        }

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinAdded.RemoveListener(OnSkinAdded);
            SkinInventoryManager.Instance.OnSkinRemoved.RemoveListener(OnSkinRemoved);
        }

        if (searchInput != null)
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);

        if (searchInputTmp != null)
            searchInputTmp.onValueChanged.RemoveListener(HandleSearchChanged);
    }

    private IEnumerator WaitThenSubscribe()
    {
        while (SkinInventoryManager.Instance == null)
            yield return null;

        // OnSkinRemoved is always subscribed so sold-skin cleanup works even when the tab is hidden
        SkinInventoryManager.Instance.OnSkinRemoved.AddListener(OnSkinRemoved);

        if (rebuildOnTabShow)
        {
            while (SidebarController.Instance == null)
                yield return null;

            _parentTabPanel = GetComponentInParent<TabPanel>(true);
            if (_parentTabPanel == null)
            {
                Debug.LogWarning("[SkinInventoryUI] rebuildOnTabShow=true but no parent TabPanel was found. Falling back to live mode.");
                SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnSkinAdded);
                RebuildFromSnapshot();
            }
            else
            {
                SidebarController.Instance.OnTabChanged.AddListener(HandleTabChanged);
                SidebarController.Instance.OnTabClosed.AddListener(HandleTabClosed);

                // If the tab is already visible at startup, activate immediately
                if (_parentTabPanel.IsVisible)
                    ActivateLive();
            }
        }
        else
        {
            SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnSkinAdded);
            RebuildFromSnapshot();
        }

        initRoutine = null;
    }

    public void RebuildFromSnapshot()
    {
        if (contentParent == null || SkinInventoryManager.Instance == null)
            return;

        ResolveTemplateIfNeeded();
        ClearSpawnedCards();
        OnCardsCleared?.Invoke();

        var list = SkinInventoryManager.Instance.Entries;
        for (int i = 0; i < list.Count; i++)
        {
            SpawnCard(list[i]);
        }

        ApplySearchFilter();
    }

    private void OnSkinAdded(SkinInventoryEntry entry)
    {
        SpawnCard(entry);
    }

    private void OnSkinRemoved(SkinInventoryEntry entry)
    {
        detailPanel?.Hide();

        if (contentParent == null || entry == null) return;

        var cards = contentParent.GetComponentsInChildren<SkinInventoryCardUI>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            if (sceneTemplateCard != null && card == sceneTemplateCard) continue;
            if (card.CurrentEntry != null && card.CurrentEntry.instanceId == entry.instanceId)
            {
                Destroy(card.gameObject);
                break;
            }
        }
    }

    private void ActivateLive()
    {
        if (_liveActive) return;
        _liveActive = true;
        if (SkinInventoryManager.Instance != null)
            SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnSkinAdded);
        RebuildFromSnapshot();
    }

    private void DeactivateLive()
    {
        if (!_liveActive) return;
        _liveActive = false;
        if (SkinInventoryManager.Instance != null)
            SkinInventoryManager.Instance.OnSkinAdded.RemoveListener(OnSkinAdded);
        ClearSpawnedCards();
        OnCardsCleared?.Invoke();
    }

    private void HandleTabChanged(string tabId, int index)
    {
        if (_parentTabPanel == null) return;
        if (tabId == _parentTabPanel.TabId)
            ActivateLive();
        else if (_liveActive)
            DeactivateLive();
    }

    private void HandleTabClosed(string tabId, int index)
    {
        if (_parentTabPanel == null) return;
        if (tabId == _parentTabPanel.TabId && _liveActive)
            DeactivateLive();
    }

    private void SpawnCard(SkinInventoryEntry entry)
    {
        if (contentParent == null || entry == null)
            return;

        GameObject source = GetCardSource();
        if (source == null)
            return;

        var go = Instantiate(source, contentParent);
        go.SetActive(true);
        if (newestOnTop)
        {
            go.transform.SetAsFirstSibling();
        }
        var card = go.GetComponent<SkinInventoryCardUI>();
        if (card != null)
        {
            card.Bind(entry);
            card.SetClickMode(defaultCardClickMode);
            if (enableDetailPanel && detailPanel != null)
            {
                card.OnSelected.RemoveListener(detailPanel.Show);
                card.OnSelected.AddListener(detailPanel.Show);
            }
            CardSpawned?.Invoke(card);
        }

        ApplySearchFilter(go);
    }

    private void HandleSearchChanged(string value)
    {
        searchFilter = value;
        ApplySearchFilter();
    }

    private void ApplySearchFilter(GameObject specificCard = null)
    {
        string query = string.IsNullOrWhiteSpace(searchFilter)
            ? null
            : searchFilter.Trim().ToLowerInvariant();

        if (specificCard != null)
        {
            ApplySearchFilterToCard(specificCard, query);
            return;
        }

        if (contentParent == null)
            return;

        var cards = contentParent.GetComponentsInChildren<SkinInventoryCardUI>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            ApplySearchFilterToCard(card.gameObject, query);
        }
    }

    private void ApplySearchFilterToCard(GameObject cardObject, string query)
    {
        if (cardObject == null)
            return;

        var card = cardObject.GetComponent<SkinInventoryCardUI>();
        if (card == null)
            return;

        var entry = card.CurrentEntry;
        if (entry == null)
            return;

        bool show = true;
        if (!string.IsNullOrEmpty(query))
        {
            string name = !string.IsNullOrEmpty(entry.itemName)
                ? entry.itemName
                : string.Concat(entry.weaponName, " ", entry.skinName);
            show = !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(query);
        }

        cardObject.SetActive(show);
    }

    private void ResolveDetailPanel()
    {
        if (detailPanel != null || !autoFindDetailPanel)
            return;

        detailPanel = FindFirstObjectByType<SkinInventoryDetailUI>(FindObjectsInactive.Include);
    }

    private GameObject GetCardSource()
    {
        if (cardPrefab != null)
            return cardPrefab;

        ResolveTemplateIfNeeded();
        if (sceneTemplateCard != null)
            return sceneTemplateCard.gameObject;

        return null;
    }

    private void ResolveTemplateIfNeeded()
    {
        if (sceneTemplateCard != null || contentParent == null)
            return;

        if (cardPrefab != null)
            return;

        // Never use contentParent itself as the template card.
        // The template must be a child item (e.g. "skincard").
        var cards = contentParent.GetComponentsInChildren<SkinInventoryCardUI>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            if (card.transform == contentParent) continue;

            sceneTemplateCard = card;
            break;
        }

        if (sceneTemplateCard == null)
        {
            var parentCard = contentParent.GetComponent<SkinInventoryCardUI>();
            if (parentCard != null)
            {
                Debug.LogWarning("[SkinInventoryUI] contentParent has SkinInventoryCardUI on it. Move SkinInventoryCardUI to a child card object and keep contentParent as a pure container.");
            }
            return;
        }

        if (sceneTemplateCard != null)
        {
            // Keep one in-scene template hidden, and clone it at runtime.
            sceneTemplateCard.gameObject.SetActive(false);
        }
    }

    private void ClearSpawnedCards()
    {
        if (contentParent == null)
            return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Transform child = contentParent.GetChild(i);

            if (sceneTemplateCard != null && child == sceneTemplateCard.transform)
                continue;

            if (child.GetComponent<SkinInventoryCardUI>() != null)
                Destroy(child.gameObject);
        }
    }
}
