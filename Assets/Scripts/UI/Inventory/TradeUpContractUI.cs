using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TradeUpContractUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SkinInventoryUI sourceInventoryUI;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button submitButton;
    [SerializeField] private Text selectedCountText;
    [SerializeField] private TMP_Text selectedCountTmpText;
    [SerializeField] private SkinInventoryDetailUI detailPanel;

    [Header("Navigation")]
    [SerializeField] private string inventoryTabId = "4";
    [SerializeField] private string tradeTabId = "8";

    [Header("Detail Preview")]
    [SerializeField] private float detailPreviewSeconds = 3f;

    [Header("Validation")]
    [SerializeField] private int requiredCount = 10;

    [Header("Refresh")]
    [SerializeField] private List<SkinInventoryUI> refreshInventories = new List<SkinInventoryUI>();

    private readonly List<SkinInventoryEntry> selectedEntries = new List<SkinInventoryEntry>();
    private readonly HashSet<SkinInventoryCardUI> subscribedCards = new HashSet<SkinInventoryCardUI>();
    private ItemRarity? lockedRarity;
    private bool? lockedStatTrak;
    private Coroutine detailPreviewRoutine;

    private void Awake()
    {
        if (sourceInventoryUI != null && !refreshInventories.Contains(sourceInventoryUI))
            refreshInventories.Add(sourceInventoryUI);

        UpdateSelectedCountText();
    }

    private void OnEnable()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(HandleCancel);
            cancelButton.onClick.AddListener(HandleCancel);
        }

        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(HandleSubmit);
            submitButton.onClick.AddListener(HandleSubmit);
        }

        SubscribeToInventory();
        UpdateSelectedCountText();

        if (SkinInventoryManager.Instance != null)
            SkinInventoryManager.Instance.OnSkinRemoved.AddListener(HandleSkinRemoved);
    }

    private void OnDisable()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(HandleCancel);

        if (submitButton != null)
            submitButton.onClick.RemoveListener(HandleSubmit);

        UnsubscribeFromInventory();

        if (SkinInventoryManager.Instance != null)
            SkinInventoryManager.Instance.OnSkinRemoved.RemoveListener(HandleSkinRemoved);
    }

    private void SubscribeToInventory()
    {
        if (sourceInventoryUI == null)
            return;

        sourceInventoryUI.CardSpawned += HandleCardSpawned;
        sourceInventoryUI.OnCardsCleared += HandleOnCardsCleared;

        var existingCards = sourceInventoryUI.GetComponentsInChildren<SkinInventoryCardUI>(true);
        for (int i = 0; i < existingCards.Length; i++)
        {
            RegisterCard(existingCards[i]);
        }
    }

    private void UnsubscribeFromInventory()
    {
        if (sourceInventoryUI != null)
        {
            sourceInventoryUI.CardSpawned -= HandleCardSpawned;
            sourceInventoryUI.OnCardsCleared -= HandleOnCardsCleared;
        }

        foreach (var card in subscribedCards)
        {
            if (card == null) continue;
            card.SelectionChanged -= HandleSelectionChanged;
        }
        subscribedCards.Clear();
        selectedEntries.Clear();
    }

    private void HandleCardSpawned(SkinInventoryCardUI card)
    {
        RegisterCard(card);
    }

    private void HandleOnCardsCleared()
    {
        // Cards were just cleared/rebuilt — deregister SelectionChanged from old cards
        // and reset selection state. CardSpawned subscription stays active so new
        // cards will register themselves as they spawn.
        foreach (var card in subscribedCards)
        {
            if (card == null) continue;
            card.SelectionChanged -= HandleSelectionChanged;
        }
        subscribedCards.Clear();
        selectedEntries.Clear();
        lockedRarity = null;
        lockedStatTrak = null;
        UpdateSelectedCountText();
    }

    private void RegisterCard(SkinInventoryCardUI card)
    {
        if (card == null || subscribedCards.Contains(card))
            return;

        if (!card.gameObject.activeSelf)
            return;

        subscribedCards.Add(card);
        card.SelectionChanged += HandleSelectionChanged;
        ApplyFilters();
    }

    private void HandleSelectionChanged(SkinInventoryCardUI card, SkinInventoryEntry entry, bool selected)
    {
        if (entry == null)
            return;

        if (selected)
        {
            if (entry.rarity == ItemRarity.Covert || entry.rarity == ItemRarity.Knife || entry.rarity == ItemRarity.Contraband)
            {
                card?.SetSelected(false);
                return;
            }

            if (lockedRarity.HasValue && entry.rarity != lockedRarity.Value)
            {
                card?.SetSelected(false);
                return;
            }

            if (lockedStatTrak.HasValue && entry.isStatTrak != lockedStatTrak.Value)
            {
                card?.SetSelected(false);
                return;
            }

            if (!selectedEntries.Contains(entry))
                selectedEntries.Add(entry);

            if (selectedEntries.Count == 1)
            {
                lockedRarity = entry.rarity;
                lockedStatTrak = entry.isStatTrak;
                ApplyFilters();
            }

            if (selectedEntries.Count > requiredCount)
            {
                selectedEntries.Remove(entry);
                if (card != null)
                    card.SetSelected(false);
            }
        }
        else
        {
            selectedEntries.Remove(entry);
            if (selectedEntries.Count == 0)
            {
                lockedRarity = null;
                lockedStatTrak = null;
                ApplyFilters();
            }
        }

        UpdateSelectedCountText();
    }

    private void HandleCancel()
    {
        ClearSelection();
        SidebarController.Instance?.SelectTab(inventoryTabId);
    }

    private void HandleSkinRemoved(SkinInventoryEntry entry)
    {
        if (entry == null) return;

        bool wasSelected = selectedEntries.Remove(entry);
        if (!wasSelected) return;

        // Deselect the card visually if it still exists
        foreach (var card in subscribedCards)
        {
            if (card != null && card.CurrentEntry == entry)
            {
                card.SetSelected(false);
                break;
            }
        }

        // Reset rarity/StatTrak locks when selection is now empty
        if (selectedEntries.Count == 0)
        {
            lockedRarity = null;
            lockedStatTrak = null;
        }

        ApplyFilters();
        UpdateSelectedCountText();
    }

    private void HandleSubmit()
    {
        // Remove any selected entries that have since been sold or removed from inventory.
        if (SkinInventoryManager.Instance != null)
        {
            var live = SkinInventoryManager.Instance.Entries;
            for (int i = selectedEntries.Count - 1; i >= 0; i--)
            {
                bool exists = false;
                for (int j = 0; j < live.Count; j++)
                {
                    if (live[j] == selectedEntries[i]) { exists = true; break; }
                }
                if (!exists)
                {
                    selectedEntries.RemoveAt(i);
                    UpdateSelectedCountText();
                }
            }
        }

        if (!ValidateSelection(out var inputRarity, out var isStatTrak))
            return;

        ItemRarity outputRarity = GetNextRarity(inputRarity);
        if (outputRarity == inputRarity)
        {
            Debug.LogWarning("[TradeUp] Selected rarity cannot be traded up.");
            return;
        }

        var awardedResult = GetWeightedTradeUpItem(selectedEntries, outputRarity);
        if (awardedResult.Item == null)
        {
            Debug.LogWarning("[TradeUp] Failed to roll a trade-up item.");
            return;
        }

        float floatValue = GetTradeUpFloatValue(awardedResult.Item, selectedEntries);
        float marketValue = awardedResult.Item.GetValueForFloat(floatValue);

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.RemoveEntries(selectedEntries);
            SkinInventoryManager.Instance.AddSkin(awardedResult.Item, awardedResult.CaseId, marketValue, floatValue);
        }

        TryShowDetailPreview(awardedResult.Item, awardedResult.CaseId, floatValue, marketValue, outputRarity, isStatTrak);
        ClearSelection();
        RefreshInventories();
    }

    private bool ValidateSelection(out ItemRarity inputRarity, out bool isStatTrak)
    {
        inputRarity = default;
        isStatTrak = false;

        if (selectedEntries.Count != requiredCount)
        {
            Debug.LogWarning($"[TradeUp] Select exactly {requiredCount} items.");
            return false;
        }

        inputRarity = selectedEntries[0].rarity;
        isStatTrak = selectedEntries[0].isStatTrak;

        if (inputRarity == ItemRarity.Covert || inputRarity == ItemRarity.Knife || inputRarity == ItemRarity.Contraband)
        {
            Debug.LogWarning("[TradeUp] Covert/Knife items cannot be traded up.");
            return false;
        }

        for (int i = 1; i < selectedEntries.Count; i++)
        {
            var entry = selectedEntries[i];
            if (entry == null) continue;
            if (entry.rarity != inputRarity)
            {
                Debug.LogWarning("[TradeUp] All items must be the same rarity.");
                return false;
            }
            if (entry.isStatTrak != isStatTrak)
            {
                Debug.LogWarning("[TradeUp] All items must match StatTrak.");
                return false;
            }
        }

        return true;
    }
    private void TryShowDetailPreview(CaseItemData item, string caseId, float floatValue, float marketValue, ItemRarity rarity, bool isStatTrak)
    {
        if (detailPanel == null || detailPreviewSeconds <= 0f || item == null)
            return;

        var entry = new SkinInventoryEntry
        {
            instanceId = System.Guid.NewGuid().ToString("N"),
            sourceCaseId = caseId,
            itemId = item.itemId,
            itemName = item.itemName,
            weaponName = item.weaponName,
            skinName = item.skinName,
            itemIcon = item.itemIcon,
            rarity = rarity,
            wear = GetWearFromFloat(floatValue),
            isStatTrak = isStatTrak,
            marketValue = marketValue,
            floatValue = floatValue
        };

        if (detailPreviewRoutine != null)
            StopCoroutine(detailPreviewRoutine);

        detailPreviewRoutine = StartCoroutine(ShowDetailThenReturn(entry));
    }

    private System.Collections.IEnumerator ShowDetailThenReturn(SkinInventoryEntry entry)
    {
        detailPanel.Show(entry);
        yield return new WaitForSecondsRealtime(detailPreviewSeconds);
        detailPanel.Hide();

        if (!string.IsNullOrEmpty(tradeTabId))
            SidebarController.Instance?.SelectTab(tradeTabId);

        detailPreviewRoutine = null;
    }

    private static ItemWear GetWearFromFloat(float value)
    {
        float v = Mathf.Clamp01(value);
        if (v < 0.07f) return ItemWear.FactoryNew;
        if (v < 0.15f) return ItemWear.MinimalWear;
        if (v < 0.38f) return ItemWear.FieldTested;
        if (v < 0.45f) return ItemWear.WellWorn;
        return ItemWear.BattleScarred;
    }

    private void ClearSelection()
    {
        foreach (var card in subscribedCards)
        {
            if (card != null)
                card.SetSelected(false);
        }
        selectedEntries.Clear();
        lockedRarity = null;
        lockedStatTrak = null;
        ApplyFilters();
        UpdateSelectedCountText();
    }

    private void RefreshInventories()
    {
        for (int i = 0; i < refreshInventories.Count; i++)
        {
            var ui = refreshInventories[i];
            if (ui != null)
                ui.RebuildFromSnapshot();
        }
    }

    private void ApplyFilters()
    {
        foreach (var card in subscribedCards)
        {
            if (card == null) continue;
            var entry = card.CurrentEntry;
            bool show = true;

            if (entry != null && (entry.rarity == ItemRarity.Covert || entry.rarity == ItemRarity.Knife || entry.rarity == ItemRarity.Contraband))
                show = false;

            if (lockedRarity.HasValue)
                show &= entry != null && entry.rarity == lockedRarity.Value;

            if (lockedStatTrak.HasValue)
                show &= entry != null && entry.isStatTrak == lockedStatTrak.Value;

            card.gameObject.SetActive(show);
        }
    }

    private (CaseItemData Item, string CaseId) GetWeightedTradeUpItem(IReadOnlyList<SkinInventoryEntry> inputs, ItemRarity outputRarity)
    {
        if (inputs == null || inputs.Count == 0)
            return (null, null);

        var countsByCase = new Dictionary<string, int>();
        for (int i = 0; i < inputs.Count; i++)
        {
            var entry = inputs[i];
            if (entry == null || string.IsNullOrEmpty(entry.sourceCaseId))
                return (null, null);

            if (!countsByCase.ContainsKey(entry.sourceCaseId))
                countsByCase[entry.sourceCaseId] = 0;
            countsByCase[entry.sourceCaseId]++;
        }

        var weightedItems = new List<(CaseItemData Item, string CaseId, float Weight)>();
        foreach (var pair in countsByCase)
        {
            if (!CaseCardUI.TryGetCaseDataById(pair.Key, out var caseData) || caseData == null)
                return (null, null);

            var items = new List<CaseItemData>();
            if (caseData.possibleItems != null)
            {
                for (int i = 0; i < caseData.possibleItems.Count; i++)
                {
                    var item = caseData.possibleItems[i];
                    if (item == null) continue;
                    if (item.GetEffectiveRarity() == outputRarity)
                        items.Add(item);
                }
            }

            if (items.Count == 0)
                return (null, null);

            float perItemWeight = (float)pair.Value / items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                weightedItems.Add((items[i], pair.Key, perItemWeight));
            }
        }

        return RollWeightedItem(weightedItems);
    }

    private static (CaseItemData Item, string CaseId) RollWeightedItem(List<(CaseItemData Item, string CaseId, float Weight)> items)
    {
        if (items == null || items.Count == 0)
            return (null, null);

        float totalWeight = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            totalWeight += Mathf.Max(0f, items[i].Weight);
        }

        if (totalWeight <= 0f)
        {
            int index = Random.Range(0, items.Count);
            return (items[index].Item, items[index].CaseId);
        }

        float roll = Random.Range(0f, totalWeight);
        float running = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            running += Mathf.Max(0f, items[i].Weight);
            if (roll <= running)
                return (items[i].Item, items[i].CaseId);
        }

        return (items[items.Count - 1].Item, items[items.Count - 1].CaseId);
    }

    private void UpdateSelectedCountText()
    {
        string text = $"{selectedEntries.Count}/{requiredCount} items selected";
        if (selectedCountText != null)
            selectedCountText.text = text;
        if (selectedCountTmpText != null)
            selectedCountTmpText.text = text;
    }

    private static float GetTradeUpFloatValue(CaseItemData item, IReadOnlyList<SkinInventoryEntry> inputs)
    {
        if (item == null)
            return 0f;

        float floatMin = Mathf.Clamp01(Mathf.Min(item.floatMin, item.floatMax));
        float floatMax = Mathf.Clamp01(Mathf.Max(item.floatMin, item.floatMax));

        if (inputs == null || inputs.Count == 0)
            return floatMax > floatMin ? Random.Range(floatMin, floatMax) : floatMin;

        float total = 0f;
        int count = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            var entry = inputs[i];
            if (entry == null) continue;
            total += Mathf.Clamp01(entry.floatValue);
            count++;
        }

        if (count == 0)
            return floatMax > floatMin ? Random.Range(floatMin, floatMax) : floatMin;

        float avg = total / count;
        return Mathf.Lerp(floatMin, floatMax, avg);
    }


    private static ItemRarity GetNextRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.ConsumerGrade: return ItemRarity.IndustrialGrade;
            case ItemRarity.IndustrialGrade: return ItemRarity.MilSpec;
            case ItemRarity.MilSpec: return ItemRarity.Restricted;
            case ItemRarity.Restricted: return ItemRarity.Classified;
            case ItemRarity.Classified: return ItemRarity.Covert;
            case ItemRarity.Covert: return ItemRarity.Contraband;
            default: return rarity;
        }
    }
}