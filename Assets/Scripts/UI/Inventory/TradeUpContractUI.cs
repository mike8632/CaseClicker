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

    [Header("Navigation")]
    [SerializeField] private string inventoryTabId = "4";

    [Header("Validation")]
    [SerializeField] private int requiredCount = 10;

    [Header("Refresh")]
    [SerializeField] private List<SkinInventoryUI> refreshInventories = new List<SkinInventoryUI>();

    private readonly List<SkinInventoryEntry> selectedEntries = new List<SkinInventoryEntry>();
    private readonly HashSet<SkinInventoryCardUI> subscribedCards = new HashSet<SkinInventoryCardUI>();
    private string lockedCaseId;
    private ItemRarity? lockedRarity;
    private bool? lockedStatTrak;

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
    }

    private void OnDisable()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(HandleCancel);

        if (submitButton != null)
            submitButton.onClick.RemoveListener(HandleSubmit);

        UnsubscribeFromInventory();
    }

    private void SubscribeToInventory()
    {
        if (sourceInventoryUI == null)
            return;

        sourceInventoryUI.CardSpawned += HandleCardSpawned;

        var existingCards = sourceInventoryUI.GetComponentsInChildren<SkinInventoryCardUI>(true);
        for (int i = 0; i < existingCards.Length; i++)
        {
            RegisterCard(existingCards[i]);
        }
    }

    private void UnsubscribeFromInventory()
    {
        if (sourceInventoryUI != null)
            sourceInventoryUI.CardSpawned -= HandleCardSpawned;

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
            if (!string.IsNullOrEmpty(lockedCaseId) && entry.sourceCaseId != lockedCaseId)
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
                lockedCaseId = entry.sourceCaseId;
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
                lockedCaseId = null;
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

    private void HandleSubmit()
    {
        if (!ValidateSelection(out var caseId, out var inputRarity))
            return;

        if (!CaseCardUI.TryGetCaseDataById(caseId, out var caseData) || caseData == null)
        {
            Debug.LogWarning("[TradeUp] Could not find case data for selected items.");
            return;
        }

        ItemRarity outputRarity = GetNextRarity(inputRarity);
        if (outputRarity == inputRarity)
        {
            Debug.LogWarning("[TradeUp] Selected rarity cannot be traded up.");
            return;
        }

        var candidates = new List<CaseItemData>();
        if (caseData.possibleItems != null)
        {
            for (int i = 0; i < caseData.possibleItems.Count; i++)
            {
                var item = caseData.possibleItems[i];
                if (item == null) continue;
                if (item.GetEffectiveRarity() == outputRarity)
                    candidates.Add(item);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[TradeUp] No items available for the next rarity in this case.");
            return;
        }

        var awardedItem = GetWeightedRandomItem(candidates);
        if (awardedItem == null)
        {
            Debug.LogWarning("[TradeUp] Failed to roll a trade-up item.");
            return;
        }

        float floatValue = GetRandomFloatValue(awardedItem);
        float marketValue = awardedItem.GetValueForFloat(floatValue);

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.RemoveEntries(selectedEntries);
            SkinInventoryManager.Instance.AddSkin(awardedItem, caseId, marketValue, floatValue);
        }

        ClearSelection();
        RefreshInventories();
    }

    private bool ValidateSelection(out string caseId, out ItemRarity inputRarity)
    {
        caseId = null;
        inputRarity = default;

        if (selectedEntries.Count != requiredCount)
        {
            Debug.LogWarning($"[TradeUp] Select exactly {requiredCount} items.");
            return false;
        }

        caseId = selectedEntries[0].sourceCaseId;
        inputRarity = selectedEntries[0].rarity;

        for (int i = 1; i < selectedEntries.Count; i++)
        {
            var entry = selectedEntries[i];
            if (entry == null) continue;
            if (entry.sourceCaseId != caseId)
            {
                Debug.LogWarning("[TradeUp] All items must be from the same collection.");
                return false;
            }

            if (entry.rarity != inputRarity)
            {
                Debug.LogWarning("[TradeUp] All items must be the same rarity.");
                return false;
            }
        }

        if (string.IsNullOrEmpty(caseId))
        {
            Debug.LogWarning("[TradeUp] Selected items have no collection id.");
            return false;
        }

        return true;
    }

    private void ClearSelection()
    {
        foreach (var card in subscribedCards)
        {
            if (card != null)
                card.SetSelected(false);
        }
        selectedEntries.Clear();
        lockedCaseId = null;
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

            if (!string.IsNullOrEmpty(lockedCaseId))
                show &= entry != null && entry.sourceCaseId == lockedCaseId;

            if (lockedRarity.HasValue)
                show &= entry != null && entry.rarity == lockedRarity.Value;

            if (lockedStatTrak.HasValue)
                show &= entry != null && entry.isStatTrak == lockedStatTrak.Value;

            card.gameObject.SetActive(show);
        }
    }

    private void UpdateSelectedCountText()
    {
        string text = $"{selectedEntries.Count}/{requiredCount} items selected";
        if (selectedCountText != null)
            selectedCountText.text = text;
        if (selectedCountTmpText != null)
            selectedCountTmpText.text = text;
    }

    private static float GetRandomFloatValue(CaseItemData item)
    {
        float floatMin = Mathf.Clamp01(Mathf.Min(item.floatMin, item.floatMax));
        float floatMax = Mathf.Clamp01(Mathf.Max(item.floatMin, item.floatMax));
        return floatMax > floatMin ? Random.Range(floatMin, floatMax) : floatMin;
    }

    private static CaseItemData GetWeightedRandomItem(List<CaseItemData> items)
    {
        float totalWeight = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null) continue;
            totalWeight += Mathf.Max(0f, GetEffectiveDropChance(item));
        }

        if (totalWeight <= 0f)
            return items[Random.Range(0, items.Count)];

        float roll = Random.Range(0f, totalWeight);
        float running = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null) continue;
            running += Mathf.Max(0f, GetEffectiveDropChance(item));
            if (roll <= running)
                return item;
        }

        return items[items.Count - 1];
    }

    private static float GetEffectiveDropChance(CaseItemData item)
    {
        if (CaseInventoryManager.Instance != null)
            return CaseInventoryManager.Instance.GetEffectiveDropChance(item);

        return item != null ? Mathf.Max(0f, item.dropChance) : 0f;
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
