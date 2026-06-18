#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  Price JSON Template Exporter — Stage 4C
//  Open via: Tools → CaseClicker → Export Price JSON Template
//
//  Scans CaseCardUI.data.possibleItems and exports a zero-valued price JSON
//  template that is immediately usable by the Price JSON Updater tool.
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PriceJsonTemplateExporterWindow : EditorWindow
{
    // ── UI state ──────────────────────────────────────────────────────────────
    private bool       _allInScene  = true;
    private CaseCardUI _targetCard;
    private string     _currency    = "USD";
    private string     _outputPath  = "Assets/Prices/price_template.json";
    private Vector2    _scroll;
    private readonly StringBuilder _log = new StringBuilder();

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/Export Price JSON Template")]
    public static void ShowWindow() => GetWindow<PriceJsonTemplateExporterWindow>("Price Template Exporter");

    // ══════════════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Price JSON Template Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Exports a zero-valued price JSON template for every skin found in the selected CaseCardUI objects.\n" +
            "Open the exported file, fill in prices, then import it with the Price JSON Updater.",
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
        _currency = EditorGUILayout.TextField("Currency", _currency);

        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
        if (GUILayout.Button("…", GUILayout.Width(28)))
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(_outputPath)) ?? Application.dataPath;
            string file = EditorUtility.SaveFilePanel("Save Price Template", dir, "price_template", "json");
            if (!string.IsNullOrEmpty(file) && file.StartsWith(Application.dataPath))
                _outputPath = "Assets" + file.Substring(Application.dataPath.Length).Replace("\\", "/");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Action ────────────────────────────────────────────────────────────
        if (GUILayout.Button("Export Price JSON Template"))
            RunExport();

        EditorGUILayout.Space(4);

        // ── Log ───────────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(240));
        EditorGUILayout.TextArea(_log.ToString(), EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Export logic
    // ══════════════════════════════════════════════════════════════════════════
    private void RunExport()
    {
        _log.Clear();
        Log("=== EXPORT PRICE JSON TEMPLATE ===");

        // ── Collect items ─────────────────────────────────────────────────────
        CaseCardUI[] cards = _allInScene
            ? UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None)
            : (_targetCard != null ? new[] { _targetCard } : Array.Empty<CaseCardUI>());

        // Deduplicate by marketHashName (or itemName as fallback)
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();

        int totalItems    = 0;
        int missingName   = 0;

        foreach (var card in cards)
        {
            if (card?.data?.possibleItems == null) continue;
            foreach (var item in card.data.possibleItems)
            {
                if (item == null) continue;
                totalItems++;

                string key = !string.IsNullOrWhiteSpace(item.marketHashName)
                    ? item.marketHashName.Trim()
                    : !string.IsNullOrWhiteSpace(item.itemName)
                        ? item.itemName.Trim()
                        : null;

                if (key == null) { missingName++; continue; }

                if (seen.Add(key))
                    names.Add(key);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);

        Log($"CaseCardUI scanned  : {cards.Length}");
        Log($"Total item entries  : {totalItems}");
        Log($"Unique skins        : {names.Count}");
        if (missingName > 0)
            Log($"Skipped (no name)   : {missingName}  (set marketHashName or itemName on those CaseItemData entries)");
        Log("");

        if (names.Count == 0)
        {
            Log("Nothing to export. Import skins with the ByMykel Bulk Case Importer first.");
            return;
        }

        // ── Build JSON ────────────────────────────────────────────────────────
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string currency  = string.IsNullOrWhiteSpace(_currency) ? "USD" : _currency.Trim().ToUpperInvariant();

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"currency\": \"{currency}\",");
        sb.AppendLine($"  \"lastUpdatedUtc\": \"{timestamp}\",");
        sb.AppendLine("  \"prices\": [");

        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i].Replace("\\", "\\\\").Replace("\"", "\\\"");
            bool last = i == names.Count - 1;

            sb.AppendLine("    {");
            sb.AppendLine($"      \"marketHashName\": \"{name}\",");
            sb.AppendLine("      \"factoryNew\": 0,");
            sb.AppendLine("      \"minimalWear\": 0,");
            sb.AppendLine("      \"fieldTested\": 0,");
            sb.AppendLine("      \"wellWorn\": 0,");
            sb.AppendLine("      \"battleScarred\": 0,");
            sb.AppendLine("      \"statTrakFactoryNew\": 0,");
            sb.AppendLine("      \"statTrakMinimalWear\": 0,");
            sb.AppendLine("      \"statTrakFieldTested\": 0,");
            sb.AppendLine("      \"statTrakWellWorn\": 0,");
            sb.AppendLine("      \"statTrakBattleScarred\": 0");
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

            Log($"Exported {names.Count} skin(s) to:");
            Log($"  {_outputPath}");
            Log("");
            Log("Next steps:");
            Log("  1. Open the exported JSON file and fill in the price values.");
            Log("  2. Import it with: Tools → CaseClicker → Price JSON Updater");
        }
        catch (Exception ex)
        {
            Log($"Export failed: {ex.Message}");
        }
    }

    private void Log(string msg) { _log.AppendLine(msg); Repaint(); }
}
#endif
