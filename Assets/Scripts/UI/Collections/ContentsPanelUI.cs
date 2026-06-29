using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shared contents / detail viewer. Can show:
///   - Case mode   : the skins inside a case (no completion tracking, no reward yet)
///   - Collection mode : skins in a collection with owned count, completion check, and a one-time reward
///
/// Open from code:
///   ContentsPanelUI.Instance.ShowCaseContents(caseData);
///   ContentsPanelUI.Instance.ShowCollectionContents(id, name, icon, items);
///   ContentsPanelUI.Instance.Hide();
///
/// Scene setup:
///   Place this script on a persistent root GameObject (e.g. under Canvas, always active).
///   The panelRoot child should start inactive — it is shown/hidden by this script.
///
/// Inspector layout suggestion for panelRoot:
///   PanelRoot  (starts inactive)
///   ├── Backdrop  (Button, full-screen transparent) ← backdropButton
///   ├── Window
///   │   ├── Header
///   │   │   ├── HeaderIcon   (Image)    ← headerIcon
///   │   │   ├── HeaderTitle  (TMP_Text) ← headerTitle
///   │   │   └── CloseButton  (Button)   ← closeButton
///   │   │
///   │   ├── CollectionStatusSection (shown only in collection mode) ← collectionStatusSection
///   │   │   ├── ProgressText  (TMP_Text) ← collectionProgressText
///   │   │   └── RewardSection           ← rewardSection  (shown only when complete)
///   │   │       ├── RewardLabel (TMP_Text) ← rewardText
///   │   │       ├── ClaimButton (Button)   ← claimButton   (hidden once claimed)
///   │   │       └── ClaimedText (any GO)   ← claimedLabel  (shown once claimed)
///   │   │
///   │   ├── SkinScrollView
///   │   │   └── Viewport → Content      ← skinListContent (VerticalLayoutGroup + ContentSizeFitter)
///   │   │
///   │   └── EmptyStatePanel             ← emptySkinListPanel (shown when no items)
/// </summary>
public class ContentsPanelUI : MonoBehaviour
{
    public static ContentsPanelUI Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Panel Root")]
    [Tooltip("The child that is shown/hidden. Must NOT be this same GameObject.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button     backdropButton;

    [Header("Header")]
    [SerializeField] private Image    headerIcon;
    [SerializeField] private TMP_Text headerTitle;
    [SerializeField] private Button   closeButton;

    [Header("Collection Status (collection mode only)")]
    [Tooltip("Parent GO that contains progress text and reward section. Hidden in case mode.")]
    [SerializeField] private GameObject collectionStatusSection;
    [SerializeField] private TMP_Text   collectionProgressText;

    [Header("Reward Section (inside Collection Status)")]
    [Tooltip("Shown only when the collection is 100% complete.")]
    [SerializeField] private GameObject rewardSection;
    [SerializeField] private TMP_Text   rewardText;
    [SerializeField] private Button     claimButton;
    [Tooltip("Anything shown to replace the claim button after claiming (label, badge, etc.).")]
    [SerializeField] private GameObject claimedLabel;

    [Header("Skin List")]
    [SerializeField] private Transform          skinListContent;
    [SerializeField] private ContentsSkinRowUI  skinRowPrefab;
    [SerializeField] private GameObject         emptySkinListPanel;

    [Header("Reward Settings")]
    [Tooltip("Money awarded when a collection is first completed and reward is claimed.")]
    [SerializeField] private double rewardMoney = 1000.0;
    [Tooltip("Format for the reward label. {0} = money amount.")]
    [SerializeField] private string rewardFormat = "Collection complete! Claim ${0:F0}";

    // ── Internal state ─────────────────────────────────────────────────────────
    private enum PanelMode { Case, Collection }

    private PanelMode          _mode;
    private string             _collectionId;   // used as the reward claim key
    private string             _displayName;
    private Sprite             _displayIcon;
    private List<CaseItemData> _sourceItems  = new List<CaseItemData>();
    private List<CaseItemData> _uniqueItems  = new List<CaseItemData>(); // deduplicated

    private readonly List<ContentsSkinRowUI> _rows = new List<ContentsSkinRowUI>();
    private Coroutine _subscribeRoutine;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (closeButton    != null) closeButton.onClick.AddListener(Hide);
        if (backdropButton != null) backdropButton.onClick.AddListener(Hide);
        if (claimButton    != null) claimButton.onClick.AddListener(ClaimReward);

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnInventoryChanged);
            SkinInventoryManager.Instance.OnSkinRemoved.AddListener(OnInventoryChanged);
        }
        else
        {
            if (_subscribeRoutine != null) StopCoroutine(_subscribeRoutine);
            _subscribeRoutine = StartCoroutine(WaitThenSubscribeInventory());
        }
    }

    private void OnDisable()
    {
        if (closeButton    != null) closeButton.onClick.RemoveListener(Hide);
        if (backdropButton != null) backdropButton.onClick.RemoveListener(Hide);
        if (claimButton    != null) claimButton.onClick.RemoveListener(ClaimReward);

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinAdded.RemoveListener(OnInventoryChanged);
            SkinInventoryManager.Instance.OnSkinRemoved.RemoveListener(OnInventoryChanged);
        }

        if (_subscribeRoutine != null) { StopCoroutine(_subscribeRoutine); _subscribeRoutine = null; }
    }

    private IEnumerator WaitThenSubscribeInventory()
    {
        while (SkinInventoryManager.Instance == null) yield return null;
        SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnInventoryChanged);
        SkinInventoryManager.Instance.OnSkinRemoved.AddListener(OnInventoryChanged);
        _subscribeRoutine = null;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Show all items in a case.
    /// Owned items are highlighted yellow. No completion tracking. No reward.
    /// </summary>
    public void ShowCaseContents(CaseData caseData)
    {
        if (caseData == null) return;

        _mode         = PanelMode.Case;
        _collectionId = null;
        _displayName  = caseData.caseName;
        _displayIcon  = caseData.caseIcon;
        _sourceItems  = caseData.possibleItems ?? new List<CaseItemData>();

        Open();
    }

    /// <summary>
    /// Show items in a collection.
    /// Owned items are highlighted. Completion is checked. One-time reward is claimable when complete.
    /// </summary>
    public void ShowCollectionContents(string collectionId, string collectionName, Sprite icon, List<CaseItemData> items)
    {
        _mode         = PanelMode.Collection;
        // Fallback: if no id, use lowercased name as the reward key
        _collectionId = !string.IsNullOrEmpty(collectionId) ? collectionId
                       : !string.IsNullOrEmpty(collectionName) ? collectionName.ToLowerInvariant()
                       : null;
        _displayName  = collectionName;
        _displayIcon  = icon;
        _sourceItems  = items ?? new List<CaseItemData>();

        Open();
    }

    /// <summary>Close the panel.</summary>
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Internal open ──────────────────────────────────────────────────────────
    private void Open()
    {
        BuildUniqueItems();

        if (panelRoot != null) panelRoot.SetActive(true);

        // Header
        if (headerIcon  != null)
        {
            headerIcon.sprite = _displayIcon;
            headerIcon.gameObject.SetActive(_displayIcon != null);
        }
        if (headerTitle != null) headerTitle.text = _displayName ?? string.Empty;

        // Collection status section — only in collection mode
        bool isCollection = _mode == PanelMode.Collection;
        if (collectionStatusSection != null)
            collectionStatusSection.SetActive(isCollection);

        RebuildSkinRows();

        if (isCollection) UpdateCollectionStatus();
    }

    // ── Unique item deduplication ──────────────────────────────────────────────
    private void BuildUniqueItems()
    {
        _uniqueItems.Clear();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var item in _sourceItems)
        {
            if (item == null) continue;
            if (seen.Add(SkinKey(item))) _uniqueItems.Add(item);
        }
    }

    // ── Skin row list ──────────────────────────────────────────────────────────
    private void RebuildSkinRows()
    {
        foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
        _rows.Clear();

        bool hasItems = _uniqueItems.Count > 0;
        if (emptySkinListPanel != null) emptySkinListPanel.SetActive(!hasItems);

        if (!hasItems || skinRowPrefab == null) return;

        var ownedKeys = BuildOwnedKeys();
        foreach (var item in _uniqueItems)
        {
            bool owned = ownedKeys.Contains(SkinKey(item));
            var row    = Instantiate(skinRowPrefab, skinListContent);
            row.Setup(item, owned);
            _rows.Add(row);
        }
    }

    // ── Inventory-change fast refresh (no rebuild) ─────────────────────────────
    private void OnInventoryChanged(SkinInventoryEntry _)
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        var ownedKeys = BuildOwnedKeys();

        for (int i = 0; i < _rows.Count && i < _uniqueItems.Count; i++)
            _rows[i].SetOwned(ownedKeys.Contains(SkinKey(_uniqueItems[i])));

        if (_mode == PanelMode.Collection) UpdateCollectionStatus();
    }

    // ── Collection status + reward ─────────────────────────────────────────────
    private void UpdateCollectionStatus()
    {
        var ownedKeys = BuildOwnedKeys();
        int total = _uniqueItems.Count;
        int owned = 0;
        foreach (var item in _uniqueItems)
            if (ownedKeys.Contains(SkinKey(item))) owned++;

        float pct        = total > 0 ? (owned / (float)total * 100f) : 0f;
        bool  isComplete = total > 0 && owned >= total;

        if (collectionProgressText != null)
            collectionProgressText.text = $"{owned}/{total} owned ({pct:F0}%)";

        // Show reward section only when the collection is 100% complete
        if (rewardSection != null) rewardSection.SetActive(isComplete);

        if (!isComplete) return;

        bool alreadyClaimed = CollectionRewardManager.Instance != null
                              && CollectionRewardManager.Instance.IsCollectionRewardClaimed(_collectionId);

        if (rewardText   != null) rewardText.text = string.Format(rewardFormat, rewardMoney);
        if (claimButton  != null) claimButton.gameObject.SetActive(!alreadyClaimed);
        if (claimedLabel != null) claimedLabel.SetActive(alreadyClaimed);
    }

    private void ClaimReward()
    {
        if (string.IsNullOrEmpty(_collectionId))
        {
            Debug.LogWarning("[ContentsPanelUI] Cannot claim reward — collection has no id.");
            return;
        }
        if (CollectionRewardManager.Instance == null)
        {
            Debug.LogError("[ContentsPanelUI] CollectionRewardManager not found in scene.");
            return;
        }
        if (CollectionRewardManager.Instance.IsCollectionRewardClaimed(_collectionId))
            return; // already claimed — button should be hidden, but guard anyway

        BalanceManager.Instance?.AddMoney(rewardMoney);
        CollectionRewardManager.Instance.MarkCollectionRewardClaimed(_collectionId);

        Debug.Log($"[ContentsPanelUI] Claimed collection reward for '{_displayName}': ${rewardMoney:F0}");

        // Refresh just the reward UI — no need to rebuild rows
        UpdateCollectionStatus();
    }

    // ── Key helpers ────────────────────────────────────────────────────────────

    /// <summary>Stable key for CaseItemData — itemId preferred, else weapon|skin.</summary>
    private static string SkinKey(CaseItemData item)
    {
        if (!string.IsNullOrEmpty(item.itemId)) return item.itemId;
        return (item.weaponName ?? "") + "|" + (item.skinName ?? item.itemName ?? "");
    }

    /// <summary>Stable key for SkinInventoryEntry — itemId preferred, else weapon|skin.</summary>
    private static string EntryKey(SkinInventoryEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.itemId)) return entry.itemId;
        return (entry.weaponName ?? "") + "|" + (entry.skinName ?? entry.itemName ?? "");
    }

    private static HashSet<string> BuildOwnedKeys()
    {
        var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (SkinInventoryManager.Instance == null) return set;
        foreach (var entry in SkinInventoryManager.Instance.Entries)
        {
            if (entry != null) set.Add(EntryKey(entry));
        }
        return set;
    }
}
