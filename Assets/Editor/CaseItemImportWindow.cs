#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class CaseItemImportWindow : EditorWindow
{
    // ── Shared ────────────────────────────────────────────────────────────────
    private CaseCardUI targetCard;
    private bool       clearExisting    = true;
    private bool       applyTopLevel    = true;
    private string     lastStatus;
    private UnityWebRequest request;
    private FetchMode  _fetchMode;

    private enum FetchMode { Csv, Json }

    // ── CSV ───────────────────────────────────────────────────────────────────
    private TextAsset csvFile;
    private string    csvApiUrl;

    // ── JSON ──────────────────────────────────────────────────────────────────
    private TextAsset jsonFile;
    private string    jsonApiUrl;

    // ── JSON data classes ─────────────────────────────────────────────────────

    [Serializable]
    private class CaseJson
    {
        public string         caseId;
        public string         caseName;
        public string         collectionId;
        public string         collectionName;
        public List<ItemJson> items = new List<ItemJson>();
    }

    [Serializable]
    private class ItemJson
    {
        public string weaponName;
        public string skinName;
        public string itemName;
        public string itemId;
        public string marketHashName;
        public string rarity;
        public string weaponCategory;
        public string collectionId;
        public string collectionName;
        public bool   overrideCollection;
        public float  dropChance;
        public float  minValue;
        public float  maxValue;
        public float  floatMin;
        public float  floatMax;
        public string iconPath;   // local Assets/ path — assign sprite from project
    }

    // ── Menu entry ────────────────────────────────────────────────────────────

    [MenuItem("Tools/Case Items/Import Case Items")]
    public static void ShowWindow()
    {
        GetWindow<CaseItemImportWindow>("Case Item Importer");
    }

    // ── GUI ───────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        // Shared options
        targetCard    = (CaseCardUI)EditorGUILayout.ObjectField("Case Card", targetCard, typeof(CaseCardUI), true);
        clearExisting = EditorGUILayout.Toggle("Clear Existing Items", clearExisting);
        applyTopLevel = EditorGUILayout.Toggle(
            new GUIContent("Apply Top-Level Data",
                "Update CaseData.caseId / caseName / collectionId / collectionName from the JSON root object."),
            applyTopLevel);

        EditorGUILayout.Space();

        // ── CSV ────────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);
        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
        using (new EditorGUI.DisabledScope(targetCard == null || csvFile == null))
        {
            if (GUILayout.Button("Import From CSV"))
                ImportFromCsv(csvFile.text);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Web API (CSV)", EditorStyles.boldLabel);
        csvApiUrl = EditorGUILayout.TextField("URL", csvApiUrl);
        using (new EditorGUI.DisabledScope(targetCard == null || string.IsNullOrWhiteSpace(csvApiUrl) || request != null))
        {
            if (GUILayout.Button("Fetch & Import CSV"))
                StartFetch(csvApiUrl, FetchMode.Csv);
        }

        EditorGUILayout.Space();

        // ── JSON ───────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("JSON Import", EditorStyles.boldLabel);
        jsonFile = (TextAsset)EditorGUILayout.ObjectField("JSON File", jsonFile, typeof(TextAsset), false);
        using (new EditorGUI.DisabledScope(targetCard == null || jsonFile == null))
        {
            if (GUILayout.Button("Import From JSON"))
                ImportFromJson(jsonFile.text);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Web API (JSON)", EditorStyles.boldLabel);
        jsonApiUrl = EditorGUILayout.TextField("URL", jsonApiUrl);
        using (new EditorGUI.DisabledScope(targetCard == null || string.IsNullOrWhiteSpace(jsonApiUrl) || request != null))
        {
            if (GUILayout.Button("Fetch & Import JSON"))
                StartFetch(jsonApiUrl, FetchMode.Json);
        }

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(lastStatus, MessageType.Info);
        }
    }

    // ── Web fetch ─────────────────────────────────────────────────────────────

    private void StartFetch(string url, FetchMode mode)
    {
        if (request != null)
        {
            request.Dispose();
            request = null;
        }

        _fetchMode = mode;
        request    = UnityWebRequest.Get(url);
        request.SendWebRequest();
        lastStatus = "Fetching…";
        EditorApplication.update += PollRequest;
        Repaint();
    }

    private void PollRequest()
    {
        if (request == null)
        {
            EditorApplication.update -= PollRequest;
            return;
        }

        if (!request.isDone)
            return;

        EditorApplication.update -= PollRequest;

        if (request.result != UnityWebRequest.Result.Success)
        {
            lastStatus = $"Fetch failed: {request.error}";
            request.Dispose();
            request = null;
            Repaint();
            return;
        }

        string text = request.downloadHandler.text;
        request.Dispose();
        request = null;

        if (_fetchMode == FetchMode.Json)
            ImportFromJson(text);
        else
            ImportFromCsv(text);

        Repaint();
    }

    // ── CSV import ────────────────────────────────────────────────────────────

    private void ImportFromCsv(string csvText)
    {
        if (targetCard == null) { lastStatus = "Assign a CaseCardUI target first."; return; }

        var items = ParseCsv(csvText);
        if (items.Count == 0) { lastStatus = "No rows found in CSV."; return; }

        EnsureCaseData();
        if (clearExisting) targetCard.data.possibleItems.Clear();
        targetCard.data.possibleItems.AddRange(items);
        MarkDirty();
        lastStatus = $"Imported {items.Count} item(s) from CSV.";
    }

    // ── JSON import ───────────────────────────────────────────────────────────

    private void ImportFromJson(string jsonText)
    {
        if (targetCard == null) { lastStatus = "Assign a CaseCardUI target first."; return; }
        if (string.IsNullOrWhiteSpace(jsonText)) { lastStatus = "JSON text is empty."; return; }

        CaseJson parsed;
        try   { parsed = JsonUtility.FromJson<CaseJson>(jsonText); }
        catch (Exception ex) { lastStatus = $"JSON parse error: {ex.Message}"; return; }

        if (parsed == null || parsed.items == null || parsed.items.Count == 0)
        {
            lastStatus = "No items found. Ensure the JSON has an \"items\" array.";
            return;
        }

        EnsureCaseData();

        // Optionally overwrite top-level case fields
        if (applyTopLevel)
            ApplyTopLevelCaseData(parsed);

        if (clearExisting) targetCard.data.possibleItems.Clear();
        var items = ConvertJsonItems(parsed.items);
        targetCard.data.possibleItems.AddRange(items);
        MarkDirty();
        lastStatus = $"Imported {items.Count} item(s) from JSON.";
    }

    private void ApplyTopLevelCaseData(CaseJson src)
    {
        if (!string.IsNullOrWhiteSpace(src.caseId))         targetCard.data.caseId         = src.caseId;
        if (!string.IsNullOrWhiteSpace(src.caseName))       targetCard.data.caseName       = src.caseName;
        if (!string.IsNullOrWhiteSpace(src.collectionId))   targetCard.data.collectionId   = src.collectionId;
        if (!string.IsNullOrWhiteSpace(src.collectionName)) targetCard.data.collectionName = src.collectionName;
    }

    private static List<CaseItemData> ConvertJsonItems(List<ItemJson> src)
    {
        var result = new List<CaseItemData>(src.Count);
        foreach (var j in src)
        {
            if (j == null) continue;

            var item = new CaseItemData
            {
                weaponName         = j.weaponName      ?? string.Empty,
                skinName           = j.skinName         ?? string.Empty,
                itemName           = j.itemName         ?? string.Empty,
                itemId             = j.itemId           ?? string.Empty,
                marketHashName     = j.marketHashName   ?? string.Empty,
                dropChance         = j.dropChance,
                minValue           = j.minValue,
                maxValue           = j.maxValue,
                floatMin           = j.floatMin,
                floatMax           = j.floatMax > 0f ? j.floatMax : 1f,
                rarity             = ParseRarity(j.rarity),
                weaponCategory     = ParseWeaponCategory(j.weaponCategory),
                collectionId       = j.collectionId   ?? string.Empty,
                collectionName     = j.collectionName ?? string.Empty,
                overrideCollection = j.overrideCollection,
            };

            // Auto-build itemName from "Weapon | Skin" if missing
            if (string.IsNullOrWhiteSpace(item.itemName))
            {
                bool hasWeapon = !string.IsNullOrWhiteSpace(item.weaponName);
                bool hasSkin   = !string.IsNullOrWhiteSpace(item.skinName);
                if (hasWeapon && hasSkin)       item.itemName = $"{item.weaponName} | {item.skinName}";
                else if (hasWeapon)             item.itemName = item.weaponName;
                else if (hasSkin)               item.itemName = item.skinName;
            }

            // Try to load icon from local project path
            if (!string.IsNullOrWhiteSpace(j.iconPath))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(j.iconPath)
                             ?? Resources.Load<Sprite>(j.iconPath);
                item.itemIcon = sprite;
            }

            result.Add(item);
        }
        return result;
    }

    // ── CSV parsing ───────────────────────────────────────────────────────────

    private static List<CaseItemData> ParseCsv(string csvText)
    {
        var result = new List<CaseItemData>();
        if (string.IsNullOrWhiteSpace(csvText)) return result;

        using var reader = new StringReader(csvText);
        string headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine)) return result;

        var headerMap = BuildHeaderMap(ParseCsvLine(headerLine));

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var col = ParseCsvLine(line);
            var item = new CaseItemData
            {
                itemName           = GetString(col, headerMap, "itemName", "name"),
                weaponName         = GetString(col, headerMap, "weaponName"),
                skinName           = GetString(col, headerMap, "skinName"),
                itemId             = GetString(col, headerMap, "itemId", "id"),
                marketHashName     = GetString(col, headerMap, "marketHashName", "market_hash_name"),
                dropChance         = GetFloat(col, headerMap, "dropChance"),
                minValue           = GetFloat(col, headerMap, "minValue", "minPrice"),
                maxValue           = GetFloat(col, headerMap, "maxValue", "maxPrice"),
                floatMin           = GetFloat(col, headerMap, "floatMin"),
                floatMax           = GetFloat(col, headerMap, "floatMax"),
                rarity             = GetRarity(col, headerMap, "rarity"),
                weaponCategory     = GetWeaponCategory(col, headerMap, "weaponCategory"),
                collectionId       = GetString(col, headerMap, "collectionId", "collection_id"),
                collectionName     = GetString(col, headerMap, "collectionName", "collection_name"),
                overrideCollection = GetBool(col, headerMap, "overrideCollection"),
            };

            // floatMax default if absent/zero
            if (item.floatMax <= 0f) item.floatMax = 1f;

            string iconPath = GetString(col, headerMap, "itemIcon", "iconPath", "icon");
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath)
                             ?? Resources.Load<Sprite>(iconPath);
                item.itemIcon = sprite;
            }

            // Auto-build itemName
            if (string.IsNullOrWhiteSpace(item.itemName) &&
                (!string.IsNullOrWhiteSpace(item.weaponName) || !string.IsNullOrWhiteSpace(item.skinName)))
            {
                item.itemName = (!string.IsNullOrWhiteSpace(item.weaponName) && !string.IsNullOrWhiteSpace(item.skinName))
                    ? $"{item.weaponName} | {item.skinName}"
                    : string.IsNullOrWhiteSpace(item.weaponName) ? item.skinName : item.weaponName;
            }

            result.Add(item);
        }
        return result;
    }

    // ── Parse helpers ─────────────────────────────────────────────────────────

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            string key = header[i]?.Trim();
            if (!string.IsNullOrEmpty(key)) map[key] = i;
        }
        return map;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values  = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
                continue;
            }
            if (c == ',' && !inQuotes) { values.Add(current.ToString()); current.Length = 0; continue; }
            current.Append(c);
        }
        values.Add(current.ToString());
        return values;
    }

    private static string GetString(IReadOnlyList<string> col, IReadOnlyDictionary<string, int> hdr, params string[] keys)
    {
        foreach (var key in keys)
            if (hdr.TryGetValue(key, out int i) && i >= 0 && i < col.Count) return col[i].Trim();
        return string.Empty;
    }

    private static float GetFloat(IReadOnlyList<string> col, IReadOnlyDictionary<string, int> hdr, params string[] keys)
    {
        string v = GetString(col, hdr, keys);
        if (string.IsNullOrWhiteSpace(v)) return 0f;
        return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : 0f;
    }

    private static bool GetBool(IReadOnlyList<string> col, IReadOnlyDictionary<string, int> hdr, params string[] keys)
    {
        string v = GetString(col, hdr, keys).ToLowerInvariant();
        return v is "true" or "1" or "yes";
    }

    private static ItemRarity GetRarity(IReadOnlyList<string> col, IReadOnlyDictionary<string, int> hdr, params string[] keys)
        => ParseRarity(GetString(col, hdr, keys));

    private static WeaponCategory GetWeaponCategory(IReadOnlyList<string> col, IReadOnlyDictionary<string, int> hdr, params string[] keys)
        => ParseWeaponCategory(GetString(col, hdr, keys));

    private static ItemRarity ParseRarity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ItemRarity.ConsumerGrade;

        string n = value.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        foreach (ItemRarity r in Enum.GetValues(typeof(ItemRarity)))
            if (r.ToString().ToLowerInvariant() == n) return r;

        return n switch
        {
            "white" or "cg"        => ItemRarity.ConsumerGrade,
            "lightblue" or "ig"    => ItemRarity.IndustrialGrade,
            "blue" or "ms"         => ItemRarity.MilSpec,
            "milspec"              => ItemRarity.MilSpec,
            "purple" or "res"      => ItemRarity.Restricted,
            "pink" or "cl"         => ItemRarity.Classified,
            "red" or "cov"         => ItemRarity.Covert,
            "gold" or "contraband" => ItemRarity.Contraband,
            "yellow" or "knife"    => ItemRarity.Knife,
            _                      => ItemRarity.ConsumerGrade
        };
    }

    private static WeaponCategory ParseWeaponCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return WeaponCategory.Unknown;

        string n = value.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        foreach (WeaponCategory c in Enum.GetValues(typeof(WeaponCategory)))
            if (c.ToString().ToLowerInvariant() == n) return c;

        return n switch
        {
            "pistols"             => WeaponCategory.Pistol,
            "rifles"              => WeaponCategory.Rifle,
            "smgs"                => WeaponCategory.SMG,
            "snipers"             => WeaponCategory.Sniper,
            "knives"              => WeaponCategory.Knife,
            "gloves"              => WeaponCategory.Glove,
            _                     => WeaponCategory.Unknown
        };
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private void EnsureCaseData()
    {
        if (targetCard.data == null)
            targetCard.data = new CaseData();
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(targetCard);
        PrefabUtility.RecordPrefabInstancePropertyModifications(targetCard);
    }
}
#endif
