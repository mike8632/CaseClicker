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
    private CaseCardUI targetCard;
    private TextAsset csvFile;
    private string apiUrl;
    private bool clearExisting = true;
    private string lastStatus;
    private UnityWebRequest request;

    [MenuItem("Tools/Case Items/Import Case Items")]
    public static void ShowWindow()
    {
        GetWindow<CaseItemImportWindow>("Case Item Importer");
    }

    private void OnGUI()
    {
        targetCard = (CaseCardUI)EditorGUILayout.ObjectField("Case Card", targetCard, typeof(CaseCardUI), true);
        clearExisting = EditorGUILayout.Toggle("Clear Existing", clearExisting);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);
        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
        using (new EditorGUI.DisabledScope(targetCard == null || csvFile == null))
        {
            if (GUILayout.Button("Import From CSV"))
            {
                ImportFromCsv(csvFile.text);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Web API (CSV)", EditorStyles.boldLabel);
        apiUrl = EditorGUILayout.TextField("API URL", apiUrl);
        using (new EditorGUI.DisabledScope(targetCard == null || string.IsNullOrWhiteSpace(apiUrl)))
        {
            if (GUILayout.Button("Fetch & Import"))
            {
                StartFetch();
            }
        }

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(lastStatus, MessageType.Info);
        }
    }

    private void StartFetch()
    {
        if (request != null)
        {
            request.Dispose();
            request = null;
        }

        request = UnityWebRequest.Get(apiUrl);
        request.SendWebRequest();
        lastStatus = "Fetching data...";
        EditorApplication.update += PollRequest;
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
            return;
        }

        string csvText = request.downloadHandler.text;
        request.Dispose();
        request = null;
        ImportFromCsv(csvText);
    }

    private void ImportFromCsv(string csvText)
    {
        if (targetCard == null)
        {
            lastStatus = "Assign a CaseCardUI target first.";
            return;
        }

        var items = ParseCsv(csvText);
        if (items.Count == 0)
        {
            lastStatus = "No rows found to import.";
            return;
        }

        if (targetCard.data == null)
        {
            targetCard.data = new CaseData();
        }

        if (clearExisting)
            targetCard.data.possibleItems.Clear();

        targetCard.data.possibleItems.AddRange(items);

        EditorUtility.SetDirty(targetCard);
        PrefabUtility.RecordPrefabInstancePropertyModifications(targetCard);
        lastStatus = $"Imported {items.Count} items.";
    }

    private static List<CaseItemData> ParseCsv(string csvText)
    {
        var result = new List<CaseItemData>();
        if (string.IsNullOrWhiteSpace(csvText))
            return result;

        using var reader = new StringReader(csvText);
        string headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
            return result;

        var header = ParseCsvLine(headerLine);
        var headerMap = BuildHeaderMap(header);

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvLine(line);
            var item = new CaseItemData
            {
                itemName = GetString(columns, headerMap, "itemName", "name"),
                weaponName = GetString(columns, headerMap, "weaponName"),
                skinName = GetString(columns, headerMap, "skinName"),
                itemId = GetString(columns, headerMap, "itemId", "id"),
                dropChance = GetFloat(columns, headerMap, "dropChance"),
                minValue = GetFloat(columns, headerMap, "minValue", "minPrice"),
                maxValue = GetFloat(columns, headerMap, "maxValue", "maxPrice"),
                floatMin = GetFloat(columns, headerMap, "floatMin"),
                floatMax = GetFloat(columns, headerMap, "floatMax"),
                rarity = GetRarity(columns, headerMap, "rarity")
            };

            string iconPath = GetString(columns, headerMap, "itemIcon", "iconPath", "icon");
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (icon == null)
                    icon = Resources.Load<Sprite>(iconPath);
                item.itemIcon = icon;
            }

            if (string.IsNullOrWhiteSpace(item.itemName) && (!string.IsNullOrWhiteSpace(item.weaponName) || !string.IsNullOrWhiteSpace(item.skinName)))
            {
                if (!string.IsNullOrWhiteSpace(item.weaponName) && !string.IsNullOrWhiteSpace(item.skinName))
                    item.itemName = $"{item.weaponName} | {item.skinName}";
                else
                    item.itemName = string.IsNullOrWhiteSpace(item.weaponName) ? item.skinName : item.weaponName;
            }

            result.Add(item);
        }

        return result;
    }

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            string key = header[i]?.Trim();
            if (string.IsNullOrEmpty(key))
                continue;
            map[key] = i;
        }

        return map;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Length = 0;
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values;
    }

    private static string GetString(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> header, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (header.TryGetValue(keys[i], out int index) && index >= 0 && index < columns.Count)
                return columns[index].Trim();
        }

        return string.Empty;
    }

    private static float GetFloat(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> header, params string[] keys)
    {
        string value = GetString(columns, header, keys);
        if (string.IsNullOrWhiteSpace(value))
            return 0f;

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : 0f;
    }

    private static ItemRarity GetRarity(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> header, params string[] keys)
    {
        string value = GetString(columns, header, keys);
        if (string.IsNullOrWhiteSpace(value))
            return ItemRarity.ConsumerGrade;

        string normalized = value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
        {
            string rarityName = rarity.ToString().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            if (normalized == rarityName)
                return rarity;
        }

        return ItemRarity.ConsumerGrade;
    }
}
#endif
