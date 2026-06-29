using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Container Info panel — shows all items inside a selected case with ownership state.
///
/// Open from code:  ContainerInfoPanelUI.Instance.Show(caseData);
/// Close from code: ContainerInfoPanelUI.Instance.Hide();
///
/// Inspector layout suggestion:
///
///   ContainerInfoRoot  ← this script lives here (always active)
///   └── PanelRoot  (starts inactive)          ← panelRoot
///       ├── Backdrop  (Button, transparent)    ← backdropButton
///       └── Window
///           ├── Header
///           │   ├── TitleIcon   (Image)         ← titleIcon
///           │   └── TitleText   (TMP_Text)      ← titleText
///           │
///           ├── StatsRow
///           │   └── CombinedStatsText (TMP_Text) ← combinedStatsText  [NEW]
///           │       (old separate fields still work as fallback)
///           │
///           ├── CloseButton (Button)            ← closeButton
///           │
///           ├── ScrollView → Viewport → Grid    ← itemGridContent
///           │
///           └── EmptyStatePanel                 ← emptyStatePanel
/// </summary>
public class ContainerInfoPanelUI : MonoBehaviour
{
    public static ContainerInfoPanelUI Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Panel Root")]
    [Tooltip("The child window to show/hide. Must NOT be this same GameObject.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button     closeButton;
    [SerializeField] private Button     backdropButton;

    [Header("Title")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Optional case icon shown next to the title.")]
    [SerializeField] private Image    titleIcon;

    [Header("Stats — Combined (preferred)")]
    [Tooltip("If assigned, shows a single line: '12x opened | Profit: $45.20 | Not Completed'.\n" +
             "Completion part turns gold via TMP rich text when the case is complete.")]
    [SerializeField] private TMP_Text combinedStatsText;

    [Header("Stats — Separate (fallback / legacy)")]
    [SerializeField] private TMP_Text openedCountText;
    [SerializeField] private TMP_Text earningsText;
    [SerializeField] private TMP_Text completionText;

    [Header("Stats Formats (legacy fields)")]
    [SerializeField] private string openedFormat      = "Opened: {0}";
    [SerializeField] private string earningsFormat    = "Earned: ${0:F2}";
    [SerializeField] private string completedText     = "Complete ✓";
    [SerializeField] private string notCompletedText  = "Not Complete";

    [Header("Completion Colors")]
    [SerializeField] private Color completedColor     = new Color(1.00f, 0.85f, 0.00f); // gold
    [SerializeField] private Color notCompletedColor  = new Color(0.80f, 0.80f, 0.80f); // grey

    [Header("Grid")]
    [Tooltip("Parent with GridLayoutGroup (or VerticalLayoutGroup). Cards are spawned here.")]
    [SerializeField] private Transform itemGridContent;

    [Tooltip("Scene card used as a template. The original is hidden; each item gets a clone.\n" +
             "Takes priority over itemCardPrefab when assigned.")]
    [SerializeField] private ContainerInfoItemCardUI itemCardTemplate;

    [Tooltip("Project prefab fallback — used only when itemCardTemplate is not assigned.")]
    [SerializeField] private ContainerInfoItemCardUI itemCardPrefab;

    [Header("Special / Rare Item Slot")]
    [Tooltip("If true, inserts a gold 'Rare Special Item' card as the first slot.\n" +
             "Shows knives/gloves are obtainable — informational only, not counted for completion.")]
    [SerializeField] private bool         showSpecialItemCard = true;
    [Tooltip("Named knife/glove sprites for specific case rules (Kilowatt→Kukri, Breakout→Butterfly, etc.).\n" +
             "Each sprite is matched by name fragment — see KnifeRules in code.")]
    [SerializeField] private List<Sprite> specialItemSprites;
    [Tooltip("Fallback icon used for cases that have no specific knife rule.\n" +
             "Use a generic 'star', 'gem', or universal rare icon here so non-matched cases\n" +
             "look clean instead of showing a random knife from the pool.")]
    [SerializeField] private Sprite       genericSpecialItemSprite;
    [SerializeField] private string       specialItemWeaponText = "Rare Special Item";
    [SerializeField] private string       specialItemSkinText   = "Knives / Gloves";
    [Tooltip("Editor-only: folder scanned by 'Auto Fill Special Item Sprites From Folder'.\n" +
             "Not used at runtime.")]
    [SerializeField] private string       specialItemSpritesFolder = "Assets/images/knifes";

    [Header("Empty State")]
    [SerializeField] private GameObject emptyStatePanel;

    // ── Internal state ─────────────────────────────────────────────────────────
    private CaseData                               _currentCase;
    private List<CaseItemData>                     _uniqueItems      = new List<CaseItemData>();
    private readonly List<ContainerInfoItemCardUI> _cards            = new List<ContainerInfoItemCardUI>();
    private ContainerInfoItemCardUI                _specialCard;   // tracked separately — never in _cards
    private Coroutine                              _subscribeRoutine;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot != null) panelRoot.SetActive(false);

        // Hide the scene template so only generated clones are ever visible
        if (itemCardTemplate != null)
            itemCardTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (closeButton    != null) closeButton.onClick.AddListener(Hide);
        if (backdropButton != null) backdropButton.onClick.AddListener(Hide);

        if (SkinInventoryManager.Instance != null)
            SubscribeInventory();
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

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinAdded.RemoveListener(OnInventoryChanged);
            SkinInventoryManager.Instance.OnSkinRemoved.RemoveListener(OnInventoryChanged);
        }
        if (_subscribeRoutine != null) { StopCoroutine(_subscribeRoutine); _subscribeRoutine = null; }
    }

    private void SubscribeInventory()
    {
        SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnInventoryChanged);
        SkinInventoryManager.Instance.OnSkinRemoved.AddListener(OnInventoryChanged);
    }

    private IEnumerator WaitThenSubscribeInventory()
    {
        while (SkinInventoryManager.Instance == null) yield return null;
        SubscribeInventory();
        _subscribeRoutine = null;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Open the panel and populate it with the contents of <paramref name="caseData"/>.</summary>
    public void Show(CaseData caseData)
    {
        if (caseData == null)
        {
            Debug.LogWarning("[ContainerInfoPanelUI] Show() called with null CaseData.");
            return;
        }

        _currentCase = caseData;

        if (caseData.possibleItems == null || caseData.possibleItems.Count == 0)
        {
            Debug.LogWarning($"[ContainerInfoPanelUI] '{caseData.caseName}' has no possibleItems.");
            _uniqueItems.Clear();
        }
        else
        {
            BuildSortedUniqueItems(caseData.possibleItems);
        }

        if (panelRoot != null) panelRoot.SetActive(true);

        RefreshHeader();
        RefreshStats();
        RebuildGrid();
    }

    /// <summary>Close the panel.</summary>
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Inventory change (fast-path — panel already open) ─────────────────────
    private void OnInventoryChanged(SkinInventoryEntry _)
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;
        RefreshStats();
        RefreshCardOwnedState();
    }

    // ── Header ─────────────────────────────────────────────────────────────────
    private void RefreshHeader()
    {
        if (titleText != null)
            titleText.text = _currentCase != null ? _currentCase.caseName.ToUpper() : string.Empty;

        if (titleIcon != null)
        {
            titleIcon.sprite = _currentCase?.caseIcon;
            titleIcon.gameObject.SetActive(_currentCase?.caseIcon != null);
        }
    }

    // ── Stats bar ──────────────────────────────────────────────────────────────
    private void RefreshStats()
    {
        if (_currentCase == null) return;

        int    openedCount = 0;
        double profit      = 0.0;

        // ── Prefer persistent stats (survives sells / repricing) ──────────────
        if (ContainerStatsManager.Instance != null && !string.IsNullOrEmpty(_currentCase.caseId))
        {
            openedCount = ContainerStatsManager.Instance.GetOpenedCount(_currentCase.caseId);
            profit      = ContainerStatsManager.Instance.GetProfit(_currentCase.caseId);
        }
        else if (SkinInventoryManager.Instance != null)
        {
            // Fallback for old saves with no container stats: count current inventory
            double fallbackValue = 0.0;
            foreach (var entry in SkinInventoryManager.Instance.Entries)
            {
                if (entry == null) continue;
                if (entry.sourceCaseId == _currentCase.caseId)
                {
                    openedCount++;
                    fallbackValue += entry.marketValue;
                }
            }
            profit = fallbackValue; // opening cost unknown for old sessions
        }

        bool isComplete = IsCollectionComplete();

        // Format profit with correct sign: "$3.25" or "-$3.25"
        string profitStr = profit >= 0
            ? $"${profit:F2}"
            : $"-${(-profit):F2}";

        // ── Combined stats text (preferred) ───────────────────────────────────
        if (combinedStatsText != null)
        {
            string completionPart = isComplete
                ? "<color=#FFD700>Completed</color>"
                : "Not Completed";

            combinedStatsText.text  = $"{openedCount}x opened | Profit: {profitStr} | {completionPart}";
            combinedStatsText.color = Color.white;
        }

        // ── Separate fields (legacy / fallback) ───────────────────────────────
        if (openedCountText != null)
            openedCountText.text = string.Format(openedFormat, openedCount);

        if (earningsText != null)
            earningsText.text = $"Profit: {profitStr}";

        if (completionText != null)
        {
            completionText.text  = isComplete ? completedText : notCompletedText;
            completionText.color = isComplete ? completedColor : notCompletedColor;
        }
    }

    // ── Grid ───────────────────────────────────────────────────────────────────
    private void RebuildGrid()
    {
        // Destroy previous clones — never the template
        if (_specialCard != null && _specialCard != itemCardTemplate)
        {
            Destroy(_specialCard.gameObject);
            _specialCard = null;
        }

        foreach (var c in _cards)
        {
            if (c != null && c != itemCardTemplate)
                Destroy(c.gameObject);
        }
        _cards.Clear();

        bool hasItems = _uniqueItems.Count > 0;
        if (emptyStatePanel != null) emptyStatePanel.SetActive(!hasItems && !showSpecialItemCard);

        // Resolve source: scene template takes priority over project prefab
        ContainerInfoItemCardUI source = itemCardTemplate != null ? itemCardTemplate : itemCardPrefab;

        if (source == null || itemGridContent == null)
        {
            if ((hasItems || showSpecialItemCard) && source == null)
                Debug.LogError("[ContainerInfoPanelUI] No itemCardTemplate or itemCardPrefab assigned — cannot spawn item cards.");
            return;
        }

        // ── Special/rare gold card (always first) ──────────────────────────────
        if (showSpecialItemCard)
        {
            var special = Instantiate(source, itemGridContent);
            special.transform.SetAsFirstSibling();
            special.gameObject.SetActive(true);
            special.SetupSpecial(PickSpecialSpriteForCase(_currentCase), specialItemWeaponText, specialItemSkinText);
            _specialCard = special;
        }

        if (!hasItems) return;

        // ── Regular item cards ─────────────────────────────────────────────────
        var ownedKeys = BuildOwnedKeys();

        foreach (var item in _uniqueItems)
        {
            bool owned = ownedKeys.Contains(SkinKey(item));
            var  card  = Instantiate(source, itemGridContent);
            card.gameObject.SetActive(true);
            card.Setup(item, owned);
            _cards.Add(card);
        }
    }

    private void RefreshCardOwnedState()
    {
        if (_uniqueItems.Count == 0 || _cards.Count == 0) return;
        var ownedKeys = BuildOwnedKeys();

        // _cards is parallel with _uniqueItems (special card is NOT in _cards)
        for (int i = 0; i < _cards.Count && i < _uniqueItems.Count; i++)
            _cards[i].SetOwned(ownedKeys.Contains(SkinKey(_uniqueItems[i])));
    }

    // ── Unique item list (sorted) ──────────────────────────────────────────────
    private void BuildSortedUniqueItems(List<CaseItemData> source)
    {
        _uniqueItems.Clear();

        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var temp = new List<CaseItemData>();

        foreach (var item in source)
        {
            if (item == null) continue;
            if (seen.Add(SkinKey(item))) temp.Add(item);
        }

        // Descending rarity: Knife=7 first → ConsumerGrade=0 last
        temp.Sort((a, b) =>
            ContainerInfoItemCardUI.RaritySortKey(b.GetEffectiveRarity())
                .CompareTo(ContainerInfoItemCardUI.RaritySortKey(a.GetEffectiveRarity())));

        _uniqueItems = temp;
    }

    // ── Completion check ───────────────────────────────────────────────────────
    private bool IsCollectionComplete()
    {
        if (_uniqueItems.Count == 0) return false;
        var ownedKeys = BuildOwnedKeys();
        foreach (var item in _uniqueItems)
            if (!ownedKeys.Contains(SkinKey(item))) return false;
        return true;
    }

    // ── Special sprite picker ──────────────────────────────────────────────────

    // Case keyword → sprite name fragments to try (in priority order, case-insensitive).
    // Add more rules here as new case types are imported.
    private static readonly (string caseKeyword, string[] spriteKeywords)[] KnifeRules =
    {
        ("kilowatt",  new[] { "kukri", "kurra", "kura"   }),
        ("breakout",  new[] { "butterfly"                }),
        ("huntsman",  new[] { "huntsman"                 }),
        ("falchion",  new[] { "falchion"                 }),
        ("shadow",    new[] { "shadow"                   }),
        ("bowie",     new[] { "bowie"                    }),
    };

    /// <summary>
    /// Three-tier special icon selection:
    ///   1. Exact case rule  — match case id/name against KnifeRules, pick by sprite name fragment.
    ///   2. Generic fallback — use genericSpecialItemSprite if assigned (clean icon for unmatched cases).
    ///   3. Hash fallback    — deterministic pick from specialItemSprites as a last resort.
    /// Returns null only if all tiers have nothing assigned.
    /// </summary>
    private Sprite PickSpecialSpriteForCase(CaseData caseData)
    {
        string caseName = caseData?.caseName ?? "Unknown";

        // ── Tier 1: exact case rule ────────────────────────────────────────────
        if (caseData != null && specialItemSprites != null && specialItemSprites.Count > 0)
        {
            string lookupText = ((caseData.caseId ?? "") + " " + (caseData.caseName ?? "")).ToLowerInvariant();

            foreach (var (caseKeyword, spriteKeywords) in KnifeRules)
            {
                if (!lookupText.Contains(caseKeyword)) continue;

                foreach (string sk in spriteKeywords)
                {
                    Sprite found = FindSpriteByNameFragment(sk);
                    if (found != null)
                    {
                        Debug.Log($"[ContainerInfoPanelUI] '{caseName}' → exact rule '{caseKeyword}' → sprite '{found.name}'");
                        return found;
                    }
                }

                // Rule matched but sprite not found — warn and fall through to generic
                Debug.LogWarning($"[ContainerInfoPanelUI] '{caseName}' matched rule '{caseKeyword}' " +
                                 $"but no sprite matched [{string.Join(", ", spriteKeywords)}]. " +
                                 "Add a matching sprite to specialItemSprites, or assign genericSpecialItemSprite.");
                break;
            }
        }

        // ── Tier 2: generic fallback ───────────────────────────────────────────
        if (genericSpecialItemSprite != null)
        {
            Debug.Log($"[ContainerInfoPanelUI] '{caseName}' → no exact rule → generic sprite '{genericSpecialItemSprite.name}'");
            return genericSpecialItemSprite;
        }

        // ── Tier 3: deterministic hash from specialItemSprites ─────────────────
        if (specialItemSprites != null && specialItemSprites.Count > 0)
        {
            string key   = caseData != null
                ? (!string.IsNullOrEmpty(caseData.caseId) ? caseData.caseId : caseData.caseName ?? "")
                : "";
            int    hash  = Mathf.Abs(key.GetHashCode());
            Sprite picked = specialItemSprites[hash % specialItemSprites.Count];
            Debug.Log($"[ContainerInfoPanelUI] '{caseName}' → hash fallback → sprite '{picked?.name}'");
            return picked;
        }

        // Nothing assigned at all
        return null;
    }

    /// <summary>Search specialItemSprites for a sprite whose name contains <paramref name="fragment"/>.</summary>
    private Sprite FindSpriteByNameFragment(string fragment)
    {
        foreach (Sprite sprite in specialItemSprites)
            if (sprite != null && sprite.name.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return sprite;
        return null;
    }

    // ── Ownership helpers ──────────────────────────────────────────────────────

    private static string SkinKey(CaseItemData item)
    {
        if (!string.IsNullOrEmpty(item.itemId)) return item.itemId;
        return (item.weaponName ?? "") + "|" + (item.skinName ?? item.itemName ?? "");
    }

    private static HashSet<string> BuildOwnedKeys()
    {
        var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (SkinInventoryManager.Instance == null) return set;

        foreach (var entry in SkinInventoryManager.Instance.Entries)
        {
            if (entry == null) continue;
            string key = !string.IsNullOrEmpty(entry.itemId) ? entry.itemId
                       : (entry.weaponName ?? "") + "|" + (entry.skinName ?? entry.itemName ?? "");
            set.Add(key);
        }
        return set;
    }

#if UNITY_EDITOR
    // ── Editor helper ──────────────────────────────────────────────────────────

    [ContextMenu("Auto Fill Special Item Sprites From Folder")]
    private void AutoFillSpecialItemSpritesFromFolder()
    {
        if (string.IsNullOrEmpty(specialItemSpritesFolder))
        {
            Debug.LogWarning("[ContainerInfoPanelUI] specialItemSpritesFolder is empty — set the folder path first.");
            return;
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { specialItemSpritesFolder });

        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning($"[ContainerInfoPanelUI] No Sprite assets found in '{specialItemSpritesFolder}'. " +
                             "Check the folder path and make sure textures are imported as Sprite.");
            return;
        }

        // Load each sprite and sort by name for a stable, predictable order
        var loaded = new System.Collections.Generic.List<Sprite>(guids.Length);
        foreach (string guid in guids)
        {
            string path   = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var    sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) loaded.Add(sprite);
        }

        loaded.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name));

        UnityEditor.Undo.RecordObject(this, "Auto Fill Special Item Sprites");

        if (specialItemSprites == null)
            specialItemSprites = new List<Sprite>();
        specialItemSprites.Clear();
        specialItemSprites.AddRange(loaded);

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"[ContainerInfoPanelUI] Auto-filled {loaded.Count} special item sprite(s) from '{specialItemSpritesFolder}'.");
    }
#endif
}
