#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  ByMykel Bulk Case Importer — Stage 4A
//  Open via: Tools → CaseClicker → ByMykel Bulk Case Importer
//
//  Imports skins from local ByMykel CSGO-API JSON files into CaseCardUI objects.
//  Data source: https://github.com/ByMykel/CSGO-API  (releases/latest)
//  Required: crates.json + skins.json as TextAssets
//  Optional: skins_not_grouped.json
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ByMykelBulkImporterWindow : EditorWindow
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ByMykel JSON DTOs  (minimal fields — JsonUtility ignores unknown ones)
    // ══════════════════════════════════════════════════════════════════════════

    // Root arrays are wrapped so JsonUtility can parse them
    [Serializable] private class CratesWrapper { public BmCrate[]  items; }
    [Serializable] private class SkinsWrapper  { public BmSkin[]   items; }

    [Serializable]
    private class BmCrate
    {
        public string          id;
        public string          name;
        public string          market_hash_name;
        public string          type;             // "Case", "Souvenir", "Sticker Capsule" …
        public BmSkinRef[]     contains;
        public BmSkinRef[]     contains_rare;
    }

    [Serializable]
    private class BmSkinRef                     // lightweight entry inside crates
    {
        public string   id;
        public string   name;
        public string   image;                  // URL — stored as CaseItemData.itemIconUrl
        public BmRarity rarity = new BmRarity();
    }

    [Serializable]
    private class BmSkin                        // full detail entry from skins.json
    {
        public string           id;
        public string           name;
        public string           image;          // URL — stored as CaseItemData.itemIconUrl
        public BmWeapon         weapon      = new BmWeapon();
        public BmCategory       category    = new BmCategory();
        public BmPattern        pattern     = new BmPattern();
        public float            min_float;
        public float            max_float;
        public BmRarity         rarity      = new BmRarity();
        public bool             stattrak;
        public BmCollectionRef[] collections;
    }

    [Serializable] private class BmRarity        { public string id; public string name; public string color; }
    [Serializable] private class BmWeapon        { public string id; public string name; }
    [Serializable] private class BmCategory      { public string id; public string name; }
    [Serializable] private class BmPattern       { public string id; public string name; }
    [Serializable] private class BmCollectionRef { public string id; public string name; public string image; }

    // ══════════════════════════════════════════════════════════════════════════
    //  UI state
    // ══════════════════════════════════════════════════════════════════════════
    private TextAsset  _cratesJson;
    private TextAsset  _skinsJson;
    private TextAsset  _skinsNotGroupedJson;
    private TextAsset  _collectionsJson;         // optional — reserved for future use

    private bool       _allInScene    = true;
    private CaseCardUI _targetCard;
    private bool       _clearExisting = true;
    private bool       _includeRare   = false;

    private Vector2    _scroll;
    private readonly StringBuilder _log = new StringBuilder();

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/ByMykel Bulk Case Importer")]
    public static void ShowWindow() => GetWindow<ByMykelBulkImporterWindow>("ByMykel Importer");

    // ══════════════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        EditorGUILayout.LabelField("ByMykel Bulk Case Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Imports skins from ByMykel CSGO-API JSON files into CaseCardUI.data.possibleItems.\n" +
            "Get data from: https://github.com/ByMykel/CSGO-API  (Releases → latest)",
            MessageType.None);

        EditorGUILayout.Space(4);

        // ── Source files ──────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Source JSON Files", EditorStyles.boldLabel);
        _cratesJson          = (TextAsset)EditorGUILayout.ObjectField("crates.json (required)",            _cratesJson,          typeof(TextAsset), false);
        _skinsJson           = (TextAsset)EditorGUILayout.ObjectField("skins.json (required)",             _skinsJson,           typeof(TextAsset), false);
        _skinsNotGroupedJson = (TextAsset)EditorGUILayout.ObjectField("skins_not_grouped.json (optional)", _skinsNotGroupedJson, typeof(TextAsset), false);
        _collectionsJson     = (TextAsset)EditorGUILayout.ObjectField("collections.json (optional)",       _collectionsJson,     typeof(TextAsset), false);

        EditorGUILayout.Space(4);

        // ── Target ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        _allInScene = EditorGUILayout.Toggle("All CaseCardUI in Scene", _allInScene);
        if (!_allInScene)
            _targetCard = (CaseCardUI)EditorGUILayout.ObjectField("Specific Card", _targetCard, typeof(CaseCardUI), true);

        EditorGUILayout.Space(2);

        // ── Options ───────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        _clearExisting = EditorGUILayout.Toggle("Clear Existing Items Before Import", _clearExisting);
        _includeRare   = EditorGUILayout.Toggle(
            new GUIContent("Include Rare / Special Items",
                "Imports contains_rare entries (knives / gloves). " +
                "Drop weight is not automatically tuned — adjust dropChance manually afterwards."),
            _includeRare);

        EditorGUILayout.Space(4);

        // ── Actions ───────────────────────────────────────────────────────────
        bool canRun = _cratesJson != null && _skinsJson != null &&
                      (_allInScene || _targetCard != null);

        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("Dry Run  (log matches — no data changed)"))
                RunImport(dryRun: true);

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Apply Import"))
            {
                string warning = _clearExisting
                    ? "CLEAR existing possibleItems and reimport from ByMykel data.\n\nCached prices will be preserved where item IDs / names match.\n\nContinue?"
                    : "APPEND ByMykel items to existing possibleItems.\n\nContinue?";
                if (EditorUtility.DisplayDialog("Apply ByMykel Import", warning, "Apply", "Cancel"))
                    RunImport(dryRun: false);
            }
        }

        if (_cratesJson == null || _skinsJson == null)
            EditorGUILayout.HelpBox("Assign crates.json and skins.json first.", MessageType.Warning);

        EditorGUILayout.Space(4);

        // ── Log ───────────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(320));
        EditorGUILayout.TextArea(_log.ToString(), EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Core import logic
    // ══════════════════════════════════════════════════════════════════════════
    private void RunImport(bool dryRun)
    {
        _log.Clear();
        Log(dryRun ? "=== DRY RUN ===" : "=== APPLY IMPORT ===");

        // ── Parse files ───────────────────────────────────────────────────────
        BmCrate[] crates = ParseCrates(_cratesJson.text,  "crates.json");
        BmSkin[]  skins  = ParseSkins(_skinsJson.text,    "skins.json");
        if (crates == null || skins == null) return;

        // Merge optional skins_not_grouped
        if (_skinsNotGroupedJson != null)
        {
            BmSkin[] extra = ParseSkins(_skinsNotGroupedJson.text, "skins_not_grouped.json");
            if (extra != null)
            {
                var merged = new BmSkin[skins.Length + extra.Length];
                Array.Copy(skins, 0, merged, 0, skins.Length);
                Array.Copy(extra, 0, merged, skins.Length, extra.Length);
                skins = merged;
                Log($"  + {extra.Length} skins merged from skins_not_grouped.json");
            }
        }

        // Build lookup dictionaries
        var skinById   = new Dictionary<string, BmSkin>(StringComparer.OrdinalIgnoreCase);
        var skinByName = new Dictionary<string, BmSkin>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in skins)
        {
            if (!string.IsNullOrEmpty(s.id)   && !skinById.ContainsKey(s.id))     skinById[s.id]     = s;
            if (!string.IsNullOrEmpty(s.name)  && !skinByName.ContainsKey(s.name)) skinByName[s.name] = s;
        }

        Log($"Parsed {crates.Length} crate(s), {skins.Length} skin definition(s).");

        // Filter to Case type only (skip souvenir packages, sticker capsules …)
        var caseCrates = new List<BmCrate>();
        foreach (var c in crates)
        {
            string t = (c.type ?? "").Trim().ToLowerInvariant();
            if (t == "" || t == "case") caseCrates.Add(c);
        }
        Log($"Case-type crates:  {caseCrates.Count}");
        Log("");

        // ── Collect scene CaseCardUI ──────────────────────────────────────────
        CaseCardUI[] cards = _allInScene
            ? UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None)
            : (_targetCard != null ? new[] { _targetCard } : Array.Empty<CaseCardUI>());

        Log($"CaseCardUI in scene: {cards.Length}");
        Log("");

        // ── Match cards to crates and process ─────────────────────────────────
        int matchedCases     = 0;
        int unmatchedCards   = 0;
        int totalImported    = 0;
        int totalSkippedRare = 0;
        var exMatches        = new List<string>();
        var exUnmatched      = new List<string>();

        foreach (var card in cards)
        {
            if (card?.data == null) continue;

            BmCrate crate = FindMatchingCrate(card.data, caseCrates);
            if (crate == null)
            {
                unmatchedCards++;
                if (exUnmatched.Count < 6)
                    exUnmatched.Add($"  \"{card.data.caseName ?? card.data.caseId ?? "(unnamed)"}\"");
                continue;
            }

            matchedCases++;
            int normalCount = crate.contains?.Length      ?? 0;
            int rareCount   = crate.contains_rare?.Length ?? 0;

            if (exMatches.Count < 5)
                exMatches.Add($"  \"{card.data.caseName}\"  ←  \"{crate.name}\"" +
                              $"  ({normalCount} skin(s){(rareCount > 0 ? $" + {rareCount} rare" : "")})");

            if (dryRun)
            {
                totalImported    += normalCount + (_includeRare ? rareCount : 0);
                totalSkippedRare += _includeRare ? 0 : rareCount;
            }
            else
            {
                int imported = ApplyToCard(card, crate, skinById, skinByName);
                totalImported    += imported;
                totalSkippedRare += _includeRare ? 0 : rareCount;
            }
        }

        // ── Summary ───────────────────────────────────────────────────────────
        Log($"Cases matched   : {matchedCases} / {cards.Length}");
        Log($"Cards unmatched : {unmatchedCards}");
        Log($"Skins {(dryRun ? "would import" : "imported")}: {totalImported}");
        if (totalSkippedRare > 0)
            Log($"Rare items skipped: {totalSkippedRare}  (enable 'Include Rare' to import knives / gloves)");
        Log("");

        if (exMatches.Count > 0)
        {
            Log("Example matches:");
            foreach (var ex in exMatches) Log(ex);
            Log("");
        }

        if (exUnmatched.Count > 0)
        {
            Log("Unmatched CaseCardUI (no ByMykel crate found):");
            foreach (var ex in exUnmatched) Log(ex);
            if (unmatchedCards > exUnmatched.Count)
                Log($"  … and {unmatchedCards - exUnmatched.Count} more.");
            Log("");
            Log("Fix: set CaseData.caseName to match the ByMykel crate name exactly,");
            Log("     OR set CaseData.caseId to the ByMykel crate id (e.g. 'crate-kilowatt').");
        }

        if (!dryRun && totalImported > 0)
        {
            AssetDatabase.SaveAssets();
            Log($"\nSaved {matchedCases} case(s), {totalImported} skin(s) total.");
            Log("=== DONE ===");
        }
        else if (dryRun)
        {
            Log("=== DRY RUN complete — no data was modified ===");
        }
    }

    // ── Apply one matched crate to one CaseCardUI ─────────────────────────────
    private int ApplyToCard(
        CaseCardUI card,
        BmCrate crate,
        Dictionary<string, BmSkin> skinById,
        Dictionary<string, BmSkin> skinByName)
    {
        // Save cached prices before clearing, keyed by itemId then itemName
        var priceCache = new Dictionary<string, ItemPriceData>(StringComparer.OrdinalIgnoreCase);
        if (_clearExisting && card.data.possibleItems != null)
        {
            foreach (var existing in card.data.possibleItems)
            {
                if (existing?.cachedPrices == null || !existing.cachedPrices.HasAnyPrice()) continue;
                if (!string.IsNullOrEmpty(existing.itemId)   && !priceCache.ContainsKey(existing.itemId))
                    priceCache[existing.itemId]   = existing.cachedPrices;
                if (!string.IsNullOrEmpty(existing.itemName) && !priceCache.ContainsKey(existing.itemName))
                    priceCache[existing.itemName] = existing.cachedPrices;
            }
        }

        if (_clearExisting)
            card.data.possibleItems.Clear();

        int count = 0;

        if (crate.contains != null)
        {
            foreach (var skinRef in crate.contains)
            {
                if (skinRef == null) continue;
                BmSkin detail = FindSkin(skinRef, skinById, skinByName);
                card.data.possibleItems.Add(BuildItem(skinRef, detail, priceCache));
                count++;
            }
        }

        if (_includeRare && crate.contains_rare != null)
        {
            foreach (var skinRef in crate.contains_rare)
            {
                if (skinRef == null) continue;
                BmSkin detail = FindSkin(skinRef, skinById, skinByName);
                card.data.possibleItems.Add(BuildItem(skinRef, detail, priceCache));
                count++;
            }
        }

        EditorUtility.SetDirty(card);
        PrefabUtility.RecordPrefabInstancePropertyModifications(card);

        Log($"  ✓ {card.data.caseName}: {count} skin(s)" +
            (priceCache.Count > 0 ? $"  [{priceCache.Count} cached price(s) restored]" : ""));

        return count;
    }

    // ── Build one CaseItemData from ByMykel refs ──────────────────────────────
    private static CaseItemData BuildItem(
        BmSkinRef skinRef,
        BmSkin detail,
        Dictionary<string, ItemPriceData> priceCache)
    {
        var item = new CaseItemData();

        if (detail != null)
        {
            item.itemId         = detail.id   ?? skinRef.id   ?? "";
            item.itemName       = detail.name ?? skinRef.name ?? "";
            item.weaponName     = detail.weapon?.name   ?? "";
            item.skinName       = detail.pattern?.name  ?? "";
            item.marketHashName = detail.name ?? skinRef.name ?? "";  // base name, no wear suffix
            // Prefer the full-detail image; fall back to the crate reference image
            item.itemIconUrl    = !string.IsNullOrEmpty(detail.image) ? detail.image
                                : (skinRef.image ?? "");
            item.floatMin       = detail.min_float;
            item.floatMax       = detail.max_float > 0f ? detail.max_float : 1f;
            item.rarity         = MapRarity(detail.rarity?.name ?? skinRef.rarity?.name ?? "");
            item.weaponCategory = MapCategory(
                detail.category?.id   ?? "",
                detail.category?.name ?? "",
                detail.weapon?.name   ?? "");

            if (detail.collections != null && detail.collections.Length > 0)
            {
                var coll = detail.collections[0];
                item.collectionId       = coll.id   ?? "";
                item.collectionName     = coll.name ?? "";
                item.overrideCollection = true;
            }
        }
        else
        {
            // Fallback when skin is not found in skins.json
            item.itemId         = skinRef.id    ?? "";
            item.itemName       = skinRef.name  ?? "";
            item.marketHashName = skinRef.name  ?? "";
            item.itemIconUrl    = skinRef.image ?? "";
            item.rarity         = MapRarity(skinRef.rarity?.name ?? "");
            item.floatMax       = 1f;

            // Best-effort split from "Weapon | Skin" name
            if (!string.IsNullOrEmpty(item.itemName))
            {
                int pipe = item.itemName.IndexOf('|');
                if (pipe > 0)
                {
                    item.weaponName = item.itemName.Substring(0, pipe).Trim();
                    item.skinName   = item.itemName.Substring(pipe + 1).Trim();
                }
                else
                {
                    item.weaponName = item.itemName;
                }
                item.weaponCategory = CaseItemData.InferWeaponCategory(item.weaponName, item.itemName);
            }
        }

        // Restore any existing cached price by itemId then itemName
        if (!string.IsNullOrEmpty(item.itemId) && priceCache.TryGetValue(item.itemId, out var byId))
            item.cachedPrices = byId;
        else if (!string.IsNullOrEmpty(item.itemName) && priceCache.TryGetValue(item.itemName, out var byName))
            item.cachedPrices = byName;

        return item;
    }

    // ── Skin lookup ───────────────────────────────────────────────────────────
    private static BmSkin FindSkin(
        BmSkinRef skinRef,
        Dictionary<string, BmSkin> byId,
        Dictionary<string, BmSkin> byName)
    {
        if (!string.IsNullOrEmpty(skinRef.id)   && byId.TryGetValue(skinRef.id, out var s1))   return s1;
        if (!string.IsNullOrEmpty(skinRef.name) && byName.TryGetValue(skinRef.name, out var s2)) return s2;
        return null;
    }

    // ── Case matching ─────────────────────────────────────────────────────────
    /// <summary>
    /// Three-pass match in descending reliability:
    ///   Pass 1 — CaseData.caseId == crate.id (exact)
    ///   Pass 2 — CaseData.caseName == crate.name or market_hash_name (case-insensitive)
    ///   Pass 3 — Normalised slug match (strips "crate-", "_case", spaces, hyphens)
    ///            e.g. "kilowatt_case" → "kilowatt" == "crate-kilowatt" → "kilowatt"
    /// </summary>
    private static BmCrate FindMatchingCrate(CaseData data, List<BmCrate> crates)
    {
        if (data == null) return null;
        string caseId   = data.caseId   ?? "";
        string caseName = data.caseName ?? "";

        // Pass 1 — exact caseId match
        if (!string.IsNullOrEmpty(caseId))
            foreach (var c in crates)
                if (c.id != null && c.id.Equals(caseId, StringComparison.OrdinalIgnoreCase))
                    return c;

        // Pass 2 — exact name match
        if (!string.IsNullOrEmpty(caseName))
            foreach (var c in crates)
                if ((c.name != null            && c.name.Equals(caseName,            StringComparison.OrdinalIgnoreCase)) ||
                    (c.market_hash_name != null && c.market_hash_name.Equals(caseName, StringComparison.OrdinalIgnoreCase)))
                    return c;

        // Pass 3 — normalised slug
        string caseIdSlug   = Slug(caseId);
        string caseNameSlug = Slug(caseName);
        foreach (var c in crates)
        {
            string crateIdSlug   = Slug(c.id);
            string crateNameSlug = Slug(c.name);
            if ((!string.IsNullOrEmpty(caseIdSlug)   && (crateIdSlug == caseIdSlug   || crateNameSlug == caseIdSlug))   ||
                (!string.IsNullOrEmpty(caseNameSlug)  && (crateIdSlug == caseNameSlug || crateNameSlug == caseNameSlug)))
                return c;
        }

        return null;
    }

    // ── JSON parsing (concrete — JsonUtility does not support open generics) ──
    private BmCrate[] ParseCrates(string json, string label)
    {
        if (string.IsNullOrWhiteSpace(json)) { Log($"  ! {label} is empty."); return null; }
        try
        {
            var w = JsonUtility.FromJson<CratesWrapper>("{\"items\":" + json + "}");
            if (w?.items == null) { Log($"  ! Could not parse array from {label}."); return null; }
            Log($"  Parsed {w.items.Length} crate entries from {label}.");
            return w.items;
        }
        catch (Exception ex) { Log($"  ! Parse error in {label}: {ex.Message}"); return null; }
    }

    private BmSkin[] ParseSkins(string json, string label)
    {
        if (string.IsNullOrWhiteSpace(json)) { Log($"  ! {label} is empty."); return null; }
        try
        {
            var w = JsonUtility.FromJson<SkinsWrapper>("{\"items\":" + json + "}");
            if (w?.items == null) { Log($"  ! Could not parse array from {label}."); return null; }
            Log($"  Parsed {w.items.Length} skin entries from {label}.");
            return w.items;
        }
        catch (Exception ex) { Log($"  ! Parse error in {label}: {ex.Message}"); return null; }
    }

    // ── Rarity mapper ─────────────────────────────────────────────────────────
    private static ItemRarity MapRarity(string rarityName)
    {
        if (string.IsNullOrWhiteSpace(rarityName)) return ItemRarity.ConsumerGrade;
        string n = rarityName.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        return n switch
        {
            "consumergrade" or "stockitem" or "cg" or "white"          => ItemRarity.ConsumerGrade,
            "industrialgrade" or "ig" or "lightblue"                   => ItemRarity.IndustrialGrade,
            "milspec" or "militaryspec" or "militaryspecgrade" or "ms"  => ItemRarity.MilSpec,
            "restricted" or "res" or "purple"                          => ItemRarity.Restricted,
            "classified" or "cl" or "pink"                             => ItemRarity.Classified,
            "covert" or "cov" or "red"                                 => ItemRarity.Covert,
            "contraband" or "gold"                                     => ItemRarity.Contraband,
            // ByMykel uses "Extraordinary" for knives/gloves
            "extraordinary" or "knife" or "knives" or "gloves"
                or "ancient" or "ancientcharacter"                     => ItemRarity.Knife,
            _                                                          => ItemRarity.ConsumerGrade
        };
    }

    // ── Category mapper ───────────────────────────────────────────────────────
    private static WeaponCategory MapCategory(string catId, string catName, string weaponName)
    {
        string id = (catId ?? "").ToLowerInvariant();
        switch (id)
        {
            case "pistol":  case "pistols":                    return WeaponCategory.Pistol;
            case "rifle":   case "rifles":                     return WeaponCategory.Rifle;
            case "smg":     case "smgs":                       return WeaponCategory.SMG;
            case "sniper":  case "snipers": case "sniper_rifle": return WeaponCategory.Sniper;
            case "knife":   case "knives":                     return WeaponCategory.Knife;
            case "gloves":  case "glove":                      return WeaponCategory.Glove;
            case "heavy":   case "machinegun":                 return WeaponCategory.Heavy;
        }
        string nm = (catName ?? "").ToLowerInvariant();
        switch (nm)
        {
            case "pistol": case "pistols":                     return WeaponCategory.Pistol;
            case "rifle":  case "rifles":                      return WeaponCategory.Rifle;
            case "smg":    case "smgs":                        return WeaponCategory.SMG;
            case "sniper rifle": case "sniper":                return WeaponCategory.Sniper;
            case "knife":  case "knives":                      return WeaponCategory.Knife;
            case "gloves": case "glove":                       return WeaponCategory.Glove;
            case "heavy":  case "machine gun":                 return WeaponCategory.Heavy;
        }
        return CaseItemData.InferWeaponCategory(weaponName, "");
    }

    // ── Slug normalisation ────────────────────────────────────────────────────
    private static string Slug(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return s.ToLowerInvariant()
                .Replace("crate-", "")
                .Replace("_case",  "")
                .Replace(" case",  "")
                .Replace("-case",  "")
                .Replace("_", "").Replace("-", "").Replace(" ", "");
    }

    // ── Utility ───────────────────────────────────────────────────────────────
    private void Log(string msg) { _log.AppendLine(msg); Repaint(); }
}
#endif
