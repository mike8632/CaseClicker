#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  Placeholder Price JSON Generator — Stage 4D
//  Open via: Tools → CaseClicker → Generate Placeholder Price JSON
//
//  Generates reasonable placeholder prices based on ItemRarity and wear tier.
//  Output is a price JSON file compatible with the existing Price JSON Updater.
//  Prices are deterministic: same seed + same item name → same output.
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PlaceholderPriceGeneratorWindow : EditorWindow
{
    // ── UI state ──────────────────────────────────────────────────────────────
    private bool       _allInScene     = true;
    private CaseCardUI _targetCard;
    private string     _currency       = "USD";
    private string     _outputPath     = "Assets/Prices/placeholder_prices.json";
    private float      _statTrakMult   = 2.0f;
    private float      _variationPct   = 15f;    // ±% random variation
    private int        _seed           = 12345;
    private Vector2    _scroll;
    private readonly StringBuilder _log = new StringBuilder();

    // ── Base prices per rarity (Field-Tested baseline) ────────────────────────
    // These are the FT reference prices. All other wear tiers scale from here.
    private static readonly Dictionary<ItemRarity, float> RarityBasePrices = new Dictionary<ItemRarity, float>
    {
        { ItemRarity.ConsumerGrade,   0.10f  },
        { ItemRarity.IndustrialGrade, 0.30f  },
        { ItemRarity.MilSpec,         1.00f  },
        { ItemRarity.Restricted,      4.00f  },
        { ItemRarity.Classified,      15.00f },
        { ItemRarity.Covert,          45.00f },
        { ItemRarity.Contraband,     200.00f },
        { ItemRarity.Knife,          100.00f },
    };

    // Wear tier multipliers relative to Field-Tested (index 0=FN … 4=BS)
    private static readonly float[] WearMultipliers = { 1.80f, 1.30f, 1.00f, 0.75f, 0.55f };

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/Generate Placeholder Price JSON")]
    public static void ShowWindow() => GetWindow<PlaceholderPriceGeneratorWindow>("Placeholder Prices");

    // ══════════════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Placeholder Price JSON Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generates deterministic placeholder prices based on rarity and wear tier.\n" +
            "Import the exported file with: Tools → CaseClicker → Price JSON Updater",
            MessageType.None);

        EditorGUILayout.Space(4);

        // ── Target ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        _allInScene = EditorGUILayout.Toggle("All CaseCardUI in Scene", _allInScene);
        if (!_allInScene)
            _targetCard = (CaseCardUI)EditorGUILayout.ObjectField("Specific Card", _targetCard, typeof(CaseCardUI), true);

        EditorGUILayout.Space(4);

        // ── Settings ──────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        _currency     = EditorGUILayout.TextField("Currency", _currency);
        _statTrakMult = EditorGUILayout.Slider("StatTrak Multiplier", _statTrakMult, 1.0f, 5.0f);
        _variationPct = EditorGUILayout.Slider("Random Variation %", _variationPct, 0f, 50f);
        _seed         = EditorGUILayout.IntField(
            new GUIContent("Deterministic Seed",
                "Same seed + same item names always produce the same prices. Change the seed to get a different spread."),
            _seed);

        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("Rarity Base Prices (Field-Tested baseline)", EditorStyles.miniLabel);
        foreach (var kv in RarityBasePrices)
            EditorGUILayout.LabelField($"  {kv.Key,-20}  ${kv.Value:F2}", EditorStyles.miniLabel);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Wear Multipliers  FN×1.80  MW×1.30  FT×1.00  WW×0.75  BS×0.55", EditorStyles.miniLabel);

        EditorGUILayout.Space(4);

        // ── Output path ───────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
        if (GUILayout.Button("…", GUILayout.Width(28)))
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(_outputPath)) ?? Application.dataPath;
            string file = EditorUtility.SaveFilePanel("Save Placeholder Prices", dir, "placeholder_prices", "json");
            if (!string.IsNullOrEmpty(file) && file.StartsWith(Application.dataPath))
                _outputPath = "Assets" + file.Substring(Application.dataPath.Length).Replace("\\", "/");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Action ────────────────────────────────────────────────────────────
        if (GUILayout.Button("Generate & Export Placeholder Prices"))
            RunExport();

        EditorGUILayout.Space(4);

        // ── Log ───────────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
        EditorGUILayout.TextArea(_log.ToString(), EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Export logic
    // ══════════════════════════════════════════════════════════════════════════
    private void RunExport()
    {
        _log.Clear();
        Log("=== GENERATE PLACEHOLDER PRICES ===");

        // ── Collect unique skins ──────────────────────────────────────────────
        CaseCardUI[] cards = _allInScene
            ? UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None)
            : (_targetCard != null ? new[] { _targetCard } : Array.Empty<CaseCardUI>());

        // key=marketHashName, value=rarity (first occurrence wins)
        var skinRarity = new Dictionary<string, ItemRarity>(StringComparer.OrdinalIgnoreCase);
        int skipped = 0;

        foreach (var card in cards)
        {
            if (card?.data?.possibleItems == null) continue;
            foreach (var item in card.data.possibleItems)
            {
                if (item == null) continue;
                string key = !string.IsNullOrWhiteSpace(item.marketHashName)
                    ? item.marketHashName.Trim()
                    : !string.IsNullOrWhiteSpace(item.itemName)
                        ? item.itemName.Trim()
                        : null;

                if (key == null) { skipped++; continue; }
                if (!skinRarity.ContainsKey(key))
                    skinRarity[key] = item.rarity;
            }
        }

        var sortedNames = new List<string>(skinRarity.Keys);
        sortedNames.Sort(StringComparer.OrdinalIgnoreCase);

        Log($"CaseCardUI scanned : {cards.Length}");
        Log($"Unique skins       : {sortedNames.Count}");
        if (skipped > 0) Log($"Skipped (no name)  : {skipped}");
        Log("");

        if (sortedNames.Count == 0)
        {
            Log("Nothing to export. Run the ByMykel Bulk Case Importer first.");
            return;
        }

        // ── Generate prices ───────────────────────────────────────────────────
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string currency  = string.IsNullOrWhiteSpace(_currency) ? "USD" : _currency.Trim().ToUpperInvariant();

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"currency\": \"{currency}\",");
        sb.AppendLine($"  \"lastUpdatedUtc\": \"{timestamp}\",");
        sb.AppendLine("  \"prices\": [");

        var exampleLines = new List<string>();

        for (int i = 0; i < sortedNames.Count; i++)
        {
            string name   = sortedNames[i];
            ItemRarity rar = skinRarity[name];

            float basePrice = RarityBasePrices.TryGetValue(rar, out float bp) ? bp : 1.00f;

            // Per-item variation: seed with global seed XOR stable hash of the name
            // This makes each item deterministic but different from others
            int itemSeed = _seed ^ StableHash(name);
            var rng = new System.Random(itemSeed);
            float variation = _variationPct / 100f;

            // Generate normal wear prices
            float[] wearPrices = new float[5];
            for (int w = 0; w < 5; w++)
            {
                float raw = basePrice * WearMultipliers[w];
                float spread = (float)(rng.NextDouble() * 2.0 - 1.0) * variation;
                wearPrices[w] = (float)Math.Round(raw * (1f + spread), 2);
                if (wearPrices[w] < 0.01f) wearPrices[w] = 0.01f;
            }

            // StatTrak prices
            float stMult = Math.Max(1.0f, _statTrakMult);
            float[] stPrices = new float[5];
            for (int w = 0; w < 5; w++)
                stPrices[w] = (float)Math.Round(wearPrices[w] * stMult, 2);

            if (exampleLines.Count < 4)
                exampleLines.Add($"  {name,-40} [{rar}]  FT=${wearPrices[2]:F2}  FN=${wearPrices[0]:F2}");

            // Write JSON entry
            bool last = i == sortedNames.Count - 1;
            string safeName = name.Replace("\\", "\\\\").Replace("\"", "\\\"");

            sb.AppendLine("    {");
            sb.AppendLine($"      \"marketHashName\": \"{safeName}\",");
            sb.AppendLine($"      \"factoryNew\": {F(wearPrices[0])},");
            sb.AppendLine($"      \"minimalWear\": {F(wearPrices[1])},");
            sb.AppendLine($"      \"fieldTested\": {F(wearPrices[2])},");
            sb.AppendLine($"      \"wellWorn\": {F(wearPrices[3])},");
            sb.AppendLine($"      \"battleScarred\": {F(wearPrices[4])},");
            sb.AppendLine($"      \"statTrakFactoryNew\": {F(stPrices[0])},");
            sb.AppendLine($"      \"statTrakMinimalWear\": {F(stPrices[1])},");
            sb.AppendLine($"      \"statTrakFieldTested\": {F(stPrices[2])},");
            sb.AppendLine($"      \"statTrakWellWorn\": {F(stPrices[3])},");
            sb.AppendLine($"      \"statTrakBattleScarred\": {F(stPrices[4])}");
            sb.Append(last ? "    }" : "    },");
            sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.Append("}");

        // ── Write file ────────────────────────────────────────────────────────
        try
        {
            string fullPath = Path.GetFullPath(_outputPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssetDatabase.ImportAsset(_outputPath);
            AssetDatabase.Refresh();

            Log($"Exported {sortedNames.Count} skin(s) to:");
            Log($"  {_outputPath}");
            Log("");
            Log("Example prices generated:");
            foreach (var ex in exampleLines) Log(ex);
            Log("");
            Log("Next step: Tools → CaseClicker → Price JSON Updater  →  Apply Prices");
        }
        catch (Exception ex)
        {
            Log($"Export failed: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Log(string message)
    {
        _log.AppendLine(message);
        Debug.Log("[PlaceholderPriceGen] " + message);
    }

    /// <summary>Formats a float to 2 decimal places for JSON output (invariant culture).</summary>
    private static string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Stable, order-independent integer hash of a string.
    /// Does not use GetHashCode() which can change between .NET runs.
    /// </summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }
    }
}
#endif
