using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the multi-select (bulk action) mode for the skin inventory.
///
/// ── Toggle button wiring ──────────────────────────────────────────────────
/// Wire the toggle button in ONE place only:
///   Option A (recommended): Assign the button to selectModeToggleButton in
///     the Inspector. The script adds the listener in code. Leave the button's
///     own Inspector OnClick list empty for this action.
///   Option B: Leave selectModeToggleButton unassigned and wire the button's
///     Inspector OnClick → InventorySelectionModeUI → ToggleSelectionMode().
/// Doing BOTH causes double-activation.
/// </summary>
public class InventorySelectionModeUI : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SkinInventoryUI inventoryUI;
    [SerializeField] private Button          selectModeToggleButton;
    [SerializeField] private GameObject      bulkActionBar;
    [SerializeField] private GameObject      normalButtonsBar;

    [Header("Bulk Action Buttons")]
    [SerializeField] private Button sellSelectedButton;
    [SerializeField] private Button lockSelectedButton;
    [SerializeField] private Button unlockSelectedButton;
    [SerializeField] private Button cancelButton;
    // [SerializeField] private Button moveToStorageButton; // Future: storage units

    [Header("Display")]
    [SerializeField] private Text     selectedCountText;
    [SerializeField] private TMP_Text selectedCountTmpText;

    // ── Private state ─────────────────────────────────────────────────────────

    private bool _isActive = false;

    private readonly HashSet<SkinInventoryCardUI> _trackedCards  = new HashSet<SkinInventoryCardUI>();
    private readonly HashSet<SkinInventoryCardUI> _selectedCards = new HashSet<SkinInventoryCardUI>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Wire toggle button via code (Option A).
        // If you chose Option B (Inspector OnClick), leave selectModeToggleButton unassigned.
        Wire(selectModeToggleButton, ToggleSelectionMode);
        Wire(sellSelectedButton,     HandleSellSelected);
        Wire(lockSelectedButton,     HandleLockSelected);
        Wire(unlockSelectedButton,   HandleUnlockSelected);
        Wire(cancelButton,           HandleCancel);

        if (inventoryUI != null)
        {
            inventoryUI.CardSpawned    += HandleCardSpawned;
            inventoryUI.OnCardsCleared += HandleCardsCleared;
        }

        // Ensure correct initial state
        if (bulkActionBar != null)   bulkActionBar.SetActive(false);
        if (!_isActive && normalButtonsBar != null) normalButtonsBar.SetActive(true);
    }

    private void OnDisable()
    {
        Unwire(selectModeToggleButton, ToggleSelectionMode);
        Unwire(sellSelectedButton,     HandleSellSelected);
        Unwire(lockSelectedButton,     HandleLockSelected);
        Unwire(unlockSelectedButton,   HandleUnlockSelected);
        Unwire(cancelButton,           HandleCancel);

        if (inventoryUI != null)
        {
            inventoryUI.CardSpawned    -= HandleCardSpawned;
            inventoryUI.OnCardsCleared -= HandleCardsCleared;
        }

        if (_isActive) ExitSelectionMode();
    }

    // ── Toggle ────────────────────────────────────────────────────────────────

    public void ToggleSelectionMode()
    {
        if (_isActive) ExitSelectionMode();
        else           EnterSelectionMode();
    }

    // ── Enter ─────────────────────────────────────────────────────────────────

    private void EnterSelectionMode()
    {
        _isActive = true;

        if (inventoryUI != null)
        {
            var cards = inventoryUI.GetCards(includeInactive: true);
            foreach (var card in cards) RegisterCard(card);
        }

        SetAllMode(SkinInventoryCardUI.CardClickMode.BulkSelect);

        if (normalButtonsBar != null) normalButtonsBar.SetActive(false);
        if (bulkActionBar != null)    bulkActionBar.SetActive(true);

        UpdateCountText();
    }

    // ── Exit ──────────────────────────────────────────────────────────────────

    private void ExitSelectionMode()
    {
        // Safe to call multiple times
        if (!_isActive && _trackedCards.Count == 0)
        {
            if (bulkActionBar != null) bulkActionBar.SetActive(false);
            return;
        }

        _isActive = false;

        SetAllMode(SkinInventoryCardUI.CardClickMode.Normal);

        foreach (var card in _trackedCards)
            if (card != null) card.SelectionChanged -= HandleCardSelectionChanged;
        _trackedCards.Clear();
        _selectedCards.Clear();

        if (bulkActionBar != null)    bulkActionBar.SetActive(false);
        if (normalButtonsBar != null) normalButtonsBar.SetActive(true);

        UpdateCountText();
    }

    // ── Card tracking ─────────────────────────────────────────────────────────

    private void RegisterCard(SkinInventoryCardUI card)
    {
        if (card == null || _trackedCards.Contains(card)) return;
        _trackedCards.Add(card);
        card.SelectionChanged += HandleCardSelectionChanged;
    }

    private void SetAllMode(SkinInventoryCardUI.CardClickMode mode)
    {
        foreach (var card in _trackedCards)
            if (card != null) card.SetClickMode(mode);
    }

    private void HandleCardSpawned(SkinInventoryCardUI card)
    {
        if (!_isActive) return;
        RegisterCard(card);
        card?.SetClickMode(SkinInventoryCardUI.CardClickMode.BulkSelect);
    }

    private void HandleCardsCleared()
    {
        foreach (var card in _trackedCards)
            if (card != null) card.SelectionChanged -= HandleCardSelectionChanged;
        _trackedCards.Clear();
        _selectedCards.Clear();
        UpdateCountText();
        // _isActive stays true; new cards register via HandleCardSpawned
    }

    private void HandleCardSelectionChanged(SkinInventoryCardUI card, SkinInventoryEntry entry, bool selected)
    {
        if (card == null) return;
        if (selected) _selectedCards.Add(card);
        else          _selectedCards.Remove(card);
        UpdateCountText();
    }

    // ── Bulk actions ──────────────────────────────────────────────────────────

    private void HandleSellSelected()
    {
        if (SkinInventoryManager.Instance == null) return;

        var toSell = new List<SkinInventoryEntry>();
        foreach (var card in _selectedCards)
        {
            if (card == null || card.CurrentEntry == null) continue;
            if (card.CurrentEntry.isLocked) continue; // locked skins skipped in bulk sell
            toSell.Add(card.CurrentEntry);
        }
        foreach (var entry in toSell)
            SkinInventoryManager.Instance.SellSkin(entry);

        ExitSelectionMode();
    }

    private void HandleLockSelected()
    {
        if (SkinInventoryManager.Instance == null) return;
        foreach (var entry in CollectSelectedEntries())
            SkinInventoryManager.Instance.SetLocked(entry, true);
        ExitSelectionMode();
    }

    private void HandleUnlockSelected()
    {
        if (SkinInventoryManager.Instance == null) return;
        foreach (var entry in CollectSelectedEntries())
            SkinInventoryManager.Instance.SetLocked(entry, false);
        ExitSelectionMode();
    }

    private void HandleCancel() => ExitSelectionMode();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<SkinInventoryEntry> CollectSelectedEntries()
    {
        var list = new List<SkinInventoryEntry>(_selectedCards.Count);
        foreach (var card in _selectedCards)
            if (card != null && card.CurrentEntry != null)
                list.Add(card.CurrentEntry);
        return list;
    }

    private void UpdateCountText()
    {
        int    count = _selectedCards.Count;
        string text  = _isActive
            ? (count == 0 ? "Select skins" : $"{count} selected")
            : string.Empty;
        if (selectedCountText != null)    selectedCountText.text    = text;
        if (selectedCountTmpText != null) selectedCountTmpText.text = text;
    }

    private static void Wire(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    private static void Unwire(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(action);
    }
}
