#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  Price JSON Updater — Stage 3B (local, no API key required)
//  Open via: Tools → CaseClicker → Price JSON Updater
//
//  Imports per-wear-tier prices from a local JSON file and writes them into
//  CaseItemData.cachedPrices.  No network calls, no API key.
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PriceJsonUpdaterWindow : EditorWindow
{
    // ── Serialisable JSON DTOs ────────────────────────────────────────────────
    [Serializable]
    private class PriceFile
    {
        public string            currency;       // top-level default, e.g. "USD"
        public string            lastUpdatedUtc; // top-level default timestamp
        public List<PriceRecord> prices = new List<PriceRecord>();
    }

    [Serializable]
    private class PriceRecord
    {
        // Identifier — matched against CaseItemData in priority order:
        //   1. item.marketHashName   2. item.itemName   3. weaponName + " | " + skinName
        public string marketHashName;

        // Normal wear prices (0 = not set / skip)
        public float factoryNew;
        public float minimalWear;
        public float fieldTested;
        public float wellWorn;
        public float battleScarred;

        // StatTrak wear prices (0 = not set / skip)
        public float statTrakFactoryNew;
        public float statTrakMinimalWear;
        public float statTrakFieldTested;
        public float statTrakWellWorn;
        public float statTrakBattleScarred;

        // Per-record overrides (optional — falls back to top-level values)
        public string currency;
        public string lastUpdatedUtc;
    }

    // ── UI state ──────────────────────────────────────────────────────────────
    private TextAsset  _priceJson;
    private bool       _allInScene  = true;
    private CaseCardUI _targetCard;
    private Vector2    _scroll;
    private readonly StringBuilder _log = new StringBuilder();

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/Price JSON Updater")]
    public static void ShowWindow() => GetWindow<PriceJsonUpdaterWindow>("Price JSON Updater");

    // ── GUI ───────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Price JSON Updater", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Imports per-wear-tier prices from a local JSON file into CaseItemData.cachedPrices.\n" +
            "No API key or network connection required.",
            MessageType.None);

        EditorGUILayout.Space(4);

        // ── Source ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        _priceJson = (TextAsset)EditorGUILayout.ObjectField("Price JSON", _priceJson, typeof(TextAsset), false);

        EditorGUILayout.Space(4);

        // ── Target ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        _allInScene = EditorGUILayout.Toggle("All CaseCardUI in Scene", _allInScene);
        if (!_allInScene)
            _targetCard = (CaseCardUI)EditorGUILayout.ObjectField("Specific Card", _targetCard, typeof(CaseCardUI), true);

        EditorGUILayout.Space(4);

        // ── Actions ───────────────────────────────────────────────────────────
        bool canRun = _priceJson != null && (_allInScene || _targetCard != null);
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("Dry Run  (log matches — no data changed)"))
                RunImport(dryRun: true);

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Apply Prices"))
            {
                if (EditorUtility.DisplayDialog("Apply Prices",
                    "This will overwrite cachedPrices on all matched CaseItemData entries.\n\nContinue?",
                    "Apply", "Cancel"))
                    RunImport(dryRun: false);
            }
        }

        if (_priceJson == null)
            EditorGUILayout.HelpBox("Assign a Price JSON TextAsset first.", MessageType.Warning);

        EditorGUILayout.Space(4);

        // ── Log ───────────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
        EditorGUILayout.TextArea(_log.ToString(), EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ── Core import logic ─────────────────────────────────────────────────────
    private void RunImport(bool dryRun)
    {
        _log.Clear();
        Log(dryRun ? "=== DRY RUN ===" : "=== APPLY PRICES ===");

        // ── Parse JSON ────────────────────────────────────────────────────────
        PriceFile file;
        try
        {
            file = JsonUtility.FromJson<PriceFile>(_priceJson.text);
        }
        catch (Exception ex)
        {
            Log($"JSON parse error: {ex.Message}");
            return;
        }

        if (file == null || file.prices == null || file.prices.Count == 0)
        {
            Log("No price records found in the JSON. Check that the file has a \"prices\" array.");
            return;
        }

        string topCurrency  = !string.IsNullOrWhiteSpace(file.currency)       ? file.currency       : "USD";
        string topTimestamp = !string.IsNullOrWhiteSpace(file.lastUpdatedUtc) ? file.lastUpdatedUtc
                              : DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        Log($"Records in file : {file.prices.Count}");
        Log($"Currency        : {topCurrency}");
        Log($"Last updated    : {topTimestamp}");

        // ── Collect target items ──────────────────────────────────────────────
        var targets = CollectTargetItems();
        Log($"Case items found: {targets.Count}");
        Log("");

        // Build a lookup from CaseCardUI for marking dirty
        var itemOwner = new Dictionary<CaseItemData, CaseCardUI>();
        foreach (var (card, item) in targets)
            if (!itemOwner.ContainsKey(item)) itemOwner[item] = card;

        // ── Match & apply ─────────────────────────────────────────────────────
        int matchCount   = 0;
        int unmatchCount = 0;
        int updatedCount = 0;

        var exMatches   = new List<string>();
        var exUnmatched = new List<string>();

        foreach (var record in file.prices)
        {
            if (string.IsNullOrWhiteSpace(record.marketHashName))
            {
                Log("  ! Skipping record with empty marketHashName.");
                continue;
            }

            CaseItemData matched = FindMatchingItem(record.marketHashName, targets);

            if (matched == null)
            {
                unmatchCount++;
                if (exUnmatched.Count < 6)
                    exUnmatched.Add($"  {record.marketHashName}");
                continue;
            }

            matchCount++;
            if (exMatches.Count < 5)
                exMatches.Add($"  \"{record.marketHashName}\" → {matched.itemName ?? matched.weaponName}");

            if (!dryRun)
            {
                bool changed = ApplyRecord(matched, record, topCurrency, topTimestamp);
                if (changed)
                {
                    updatedCount++;
                    if (itemOwner.TryGetValue(matched, out var owner))
                    {
                        EditorUtility.SetDirty(owner);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
                    }
                }
            }
        }

        // ── Summary ───────────────────────────────────────────────────────────
        Log($"Matched   : {matchCount}");
        Log($"Unmatched : {unmatchCount}");
        if (!dryRun) Log($"Updated   : {updatedCount}");
        Log("");

        if (exMatches.Count > 0)
        {
            Log("Example matches:");
            foreach (var ex in exMatches) Log(ex);
            Log("");
        }

        if (exUnmatched.Count > 0)
        {
            Log("Unmatched records (no CaseItemData found):");
            foreach (var ex in exUnmatched) Log(ex);
            if (unmatchCount > exUnmatched.Count)
                Log($"  … and {unmatchCount - exUnmatched.Count} more.");
            Log("");
            Log("Tip: set marketHashName on the CaseItemData to match exactly,");
            Log("     or ensure itemName equals the JSON marketHashName.");
        }

        if (!dryRun)
        {
            if (updatedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Log($"Saved {updatedCount} updated item(s).");

                // Trigger live inventory reprice if in Play Mode
                if (EditorApplication.isPlaying && SkinInventoryManager.Instance != null)
                {
                    SkinInventoryManager.Instance.AutoRepriceAfterLoad();
                    Log("Triggered live inventory reprice (Play Mode active).");
                }
                else
                {
                    Log("Inventory will reprice automatically on next game load.");
                }
            }
            else
            {
                Log("No items were updated (all unmatched, or all prices were 0).");
            }

            Log("\n=== DONE ===");
        }
        else
        {
            Log("=== DRY RUN complete — no data was modified ===");
        }
    }

    // ── Apply one price record to one CaseItemData ────────────────────────────
    private static bool ApplyRecord(CaseItemData item, PriceRecord rec, string topCurrency, string topTimestamp)
    {
        if (item.cachedPrices == null)
            item.cachedPrices = new ItemPriceData();

        var p = item.cachedPrices;
        bool changed = false;

        void Set(ref double field, float value)
        {
            if (value > 0f) { field = value; changed = true; }
        }

        Set(ref p.factoryNew,    rec.factoryNew);
        Set(ref p.minimalWear,   rec.minimalWear);
        Set(ref p.fieldTested,   rec.fieldTested);
        Set(ref p.wellWorn,      rec.wellWorn);
        Set(ref p.battleScarred, rec.battleScarred);

        Set(ref p.statTrakFactoryNew,    rec.statTrakFactoryNew);
        Set(ref p.statTrakMinimalWear,   rec.statTrakMinimalWear);
        Set(ref p.statTrakFieldTested,   rec.statTrakFieldTested);
        Set(ref p.statTrakWellWorn,      rec.statTrakWellWorn);
        Set(ref p.statTrakBattleScarred, rec.statTrakBattleScarred);

        if (changed)
        {
            p.currency       = !string.IsNullOrWhiteSpace(rec.currency)       ? rec.currency       : topCurrency;
            p.lastUpdatedUtc = !string.IsNullOrWhiteSpace(rec.lastUpdatedUtc) ? rec.lastUpdatedUtc : topTimestamp;
        }

        return changed;
    }

    // ── Matching ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Tries to match the JSON record's marketHashName to a CaseItemData using three strategies
    /// in priority order:
    ///   1. item.marketHashName  (exact, case-sensitive)
    ///   2. item.itemName        (exact, case-insensitive)
    ///   3. "{weaponName} | {skinName}" constructed name (case-insensitive)
    /// Returns the first match found, or null.
    /// </summary>
    private static CaseItemData FindMatchingItem(
        string hashName,
        List<(CaseCardUI card, CaseItemData item)> targets)
    {
        // Pass 1 — marketHashName exact match
        foreach (var (_, item) in targets)
            if (!string.IsNullOrEmpty(item.marketHashName) &&
                item.marketHashName.Equals(hashName, StringComparison.Ordinal))
                return item;

        // Pass 2 — itemName case-insensitive
        foreach (var (_, item) in targets)
            if (!string.IsNullOrEmpty(item.itemName) &&
                item.itemName.Equals(hashName, StringComparison.OrdinalIgnoreCase))
                return item;

        // Pass 3 — "weaponName | skinName" construction
        foreach (var (_, item) in targets)
        {
            item.GetDisplayNames(out string weapon, out string skin, out _);
            if (string.IsNullOrEmpty(weapon) && string.IsNullOrEmpty(skin)) continue;

            string constructed = string.IsNullOrEmpty(skin) || skin.Equals(weapon, StringComparison.OrdinalIgnoreCase)
                ? weapon
                : $"{weapon} | {skin}";

            if (constructed.Equals(hashName, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    // ── Target collection ─────────────────────────────────────────────────────
    private List<(CaseCardUI card, CaseItemData item)> CollectTargetItems()
    {
        var result = new List<(CaseCardUI, CaseItemData)>();

        CaseCardUI[] cards = _allInScene
            ? UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None)
            : (_targetCard != null ? new[] { _targetCard } : Array.Empty<CaseCardUI>());

        foreach (var card in cards)
        {
            if (card?.data?.possibleItems == null) continue;
            foreach (var item in card.data.possibleItems)
                if (item != null) result.Add((card, item));
        }
        return result;
    }

    // ── Utility ───────────────────────────────────────────────────────────────
    private void Log(string msg) { _log.AppendLine(msg); Repaint(); }
}
#endif
