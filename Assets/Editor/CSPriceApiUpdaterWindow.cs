#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  CSPriceAPI Price Updater — Stage 3B
//  Open via: Tools → CaseClicker → CSPriceAPI Price Updater
//
//  Fetches per-wear-tier prices from selected CSPriceAPI markets and writes
//  averaged values into CaseItemData.cachedPrices.
//
//  API key is stored ONLY in EditorPrefs — never in scenes, prefabs, or saves.
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class CSPriceApiUpdaterWindow : EditorWindow
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const string PrefKeyApiKey = "CaseClicker.CSPriceAPI.Key";
    private const string BaseUrl       = "https://api.cspriceapi.com";

    // Market IDs as used in the endpoint path /v1/prices/{market}
    private static readonly string[] MarketIds =
    {
        "csfloat", "skinport", "skinbaron",
        "c5game",  "buff",     "youpin",
        "marketcsgo", "ecosteam"
    };

    // Wear suffix strings used in Steam market_hash_name keys
    private static readonly string[] WearSuffixes =
    {
        "(Factory New)",    // 0
        "(Minimal Wear)",   // 1
        "(Field-Tested)",   // 2
        "(Well-Worn)",      // 3
        "(Battle-Scarred)"  // 4
    };

    // ── Persistent UI state ───────────────────────────────────────────────────
    private string     _apiKey    = "";
    private bool       _showKey   = false;
    private bool       _allInScene = true;
    private CaseCardUI _targetCard;
    private Vector2    _scroll;
    private readonly bool[] _marketOn = new bool[8]; // index matches MarketIds

    // ── Fetch pipeline state ──────────────────────────────────────────────────
    private bool   _isFetching;
    private bool   _isDryRun;
    private string _activeMarket;
    private UnityWebRequest _activeRequest;
    private readonly Queue<string>             _fetchQueue = new Queue<string>();
    private readonly Dictionary<string, string> _fetched   = new Dictionary<string, string>();

    // ── Log ───────────────────────────────────────────────────────────────────
    private readonly StringBuilder _log = new StringBuilder();

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/CSPriceAPI Price Updater")]
    public static void ShowWindow() => GetWindow<CSPriceApiUpdaterWindow>("CSPriceAPI Updater");

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        _apiKey = EditorPrefs.GetString(PrefKeyApiKey, "");
        // Default: enable first three markets on first open
        if (!_marketOn[0] && !_marketOn[1])
        {
            _marketOn[0] = true; // csfloat
            _marketOn[1] = true; // skinport
            _marketOn[2] = true; // skinbaron
        }
    }

    private void OnDisable() => CancelFetch();

    // ── GUI ───────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        DrawApiKeySection();
        EditorGUILayout.Space(6);
        DrawMarketsSection();
        EditorGUILayout.Space(6);
        DrawTargetSection();
        EditorGUILayout.Space(6);
        DrawActionSection();
        EditorGUILayout.Space(6);
        DrawLogSection();
    }

    private void DrawApiKeySection()
    {
        EditorGUILayout.LabelField("CSPriceAPI Key", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _apiKey = _showKey
            ? EditorGUILayout.TextField(_apiKey)
            : EditorGUILayout.PasswordField(_apiKey);
        _showKey = GUILayout.Toggle(_showKey, "Show", GUILayout.Width(48));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Key"))
        {
            EditorPrefs.SetString(PrefKeyApiKey, _apiKey);
            Log("API key saved to EditorPrefs (local machine only).");
        }
        if (GUILayout.Button("Clear Key"))
        {
            _apiKey = "";
            EditorPrefs.DeleteKey(PrefKeyApiKey);
            Log("API key cleared.");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Key is stored in EditorPrefs only — never in scenes, prefabs, or player saves.", MessageType.None);
    }

    private void DrawMarketsSection()
    {
        EditorGUILayout.LabelField("Markets to fetch", EditorStyles.boldLabel);

        // 2 rows × 4 columns
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < MarketIds.Length; i++)
        {
            if (i == 4)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
            _marketOn[i] = EditorGUILayout.ToggleLeft(MarketIds[i], _marketOn[i], GUILayout.Width(105));
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        _allInScene = EditorGUILayout.Toggle("All CaseCardUI in Scene", _allInScene);
        if (!_allInScene)
            _targetCard = (CaseCardUI)EditorGUILayout.ObjectField("Specific Card", _targetCard, typeof(CaseCardUI), true);
    }

    private void DrawActionSection()
    {
        bool hasKey    = !string.IsNullOrWhiteSpace(_apiKey);
        bool anyMarket = HasAnyMarket();
        bool ready     = hasKey && anyMarket && !_isFetching;

        if (_isFetching)
        {
            EditorGUILayout.HelpBox(
                $"Fetching '{_activeMarket}'…  ({_fetched.Count} / {CountMarkets()} markets done)",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Dry Run  (fetch prices, log matches — no data changed)"))
                BeginFetch(dryRun: true);

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Fetch And Apply Prices"))
            {
                if (EditorUtility.DisplayDialog("Apply Prices",
                    "This will overwrite cachedPrices on all matched CaseItemData entries in the selected target.\n\nContinue?",
                    "Apply", "Cancel"))
                    BeginFetch(dryRun: false);
            }
        }

        if (!hasKey)    EditorGUILayout.HelpBox("Enter and save a CSPriceAPI key first.", MessageType.Warning);
        if (!anyMarket) EditorGUILayout.HelpBox("Select at least one market.", MessageType.Warning);
    }

    private void DrawLogSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
        EditorGUILayout.TextArea(_log.ToString(), EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ── Fetch pipeline ────────────────────────────────────────────────────────
    private void BeginFetch(bool dryRun)
    {
        _isDryRun = dryRun;
        _fetched.Clear();
        _fetchQueue.Clear();

        for (int i = 0; i < MarketIds.Length; i++)
            if (_marketOn[i]) _fetchQueue.Enqueue(MarketIds[i]);

        _log.Clear();
        Log(dryRun ? "=== DRY RUN ===" : "=== FETCH AND APPLY ===");
        Log($"Markets: {string.Join(", ", _fetchQueue)}");
        Log("");

        _isFetching = true;
        EditorApplication.update += PollFetch;
        StartNextRequest();
        Repaint();
    }

    private void StartNextRequest()
    {
        if (_fetchQueue.Count == 0) { FinishFetch(); return; }

        _activeMarket = _fetchQueue.Dequeue();
        string url = $"{BaseUrl}/v1/prices/{_activeMarket}";
        Log($"→ Fetching {_activeMarket} …");

        _activeRequest = UnityWebRequest.Get(url);
        _activeRequest.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
        _activeRequest.SetRequestHeader("Accept", "application/json");
        _activeRequest.SendWebRequest();
        Repaint();
    }

    private void PollFetch()
    {
        if (_activeRequest == null || !_activeRequest.isDone) return;

        if (_activeRequest.result == UnityWebRequest.Result.Success)
        {
            string body = _activeRequest.downloadHandler.text;
            _fetched[_activeMarket] = body;
            Log($"  ✓ {_activeMarket}: {body.Length} chars received");
        }
        else
        {
            Log($"  ✗ {_activeMarket} failed: {_activeRequest.error}  (HTTP {_activeRequest.responseCode})");
        }

        _activeRequest.Dispose();
        _activeRequest = null;
        StartNextRequest();
        Repaint();
    }

    private void FinishFetch()
    {
        EditorApplication.update -= PollFetch;
        _isFetching   = false;
        _activeMarket = null;

        Log($"\nAll requests done. {_fetched.Count} market(s) returned data.");

        if (_fetched.Count > 0)
            ProcessResults(_isDryRun);
        else
            Log("No data to process.");

        Repaint();
    }

    private void CancelFetch()
    {
        EditorApplication.update -= PollFetch;
        _isFetching = false;
        _activeRequest?.Dispose();
        _activeRequest = null;
    }

    // ── Processing ────────────────────────────────────────────────────────────
    private void ProcessResults(bool dryRun)
    {
        var targets = CollectTargetItems();
        Log($"\nCase items to match: {targets.Count}");

        // Dry Run: print a sample of the raw response so you can verify the schema
        if (dryRun && _fetched.Count > 0)
        {
            string sampleMarket = "";
            string sampleJson   = "";
            foreach (var kv in _fetched) { sampleMarket = kv.Key; sampleJson = kv.Value; break; }

            Log($"\n--- Raw response preview ({sampleMarket}, first 1000 chars) ---");
            Log(sampleJson.Length > 1000 ? sampleJson.Substring(0, 1000) + "\n…(truncated)" : sampleJson);
            Log("--- end preview ---\n");
            Log("If items are not matching, verify the marketHashName field on your CaseItemData");
            Log("matches the key format in the response above (e.g. \"AK-47 | Redline (Factory New)\").");
            Log("");
        }

        int matched   = 0;
        int unmatched = 0;
        int updated   = 0;

        var exMatch   = new List<string>();
        var exNoMatch = new List<string>();

        // Accumulation buffers reused per item
        var priceSums   = new double[10];
        var priceCounts = new int[10];

        foreach (var (card, item) in targets)
        {
            Array.Clear(priceSums,   0, 10);
            Array.Clear(priceCounts, 0, 10);

            bool itemMatched = false;

            foreach (var kv in _fetched)
            {
                string json = kv.Value;

                for (int w = 0; w < 5; w++)
                {
                    double np = FindPrice(json, BuildHashName(item, w, isStatTrak: false));
                    if (np > 0.0) { priceSums[w]     += np; priceCounts[w]++;     itemMatched = true; }

                    double sp = FindPrice(json, BuildHashName(item, w, isStatTrak: true));
                    if (sp > 0.0) { priceSums[5 + w] += sp; priceCounts[5 + w]++; itemMatched = true; }
                }
            }

            if (itemMatched)
            {
                matched++;
                if (exMatch.Count < 5)
                {
                    var sb2 = new StringBuilder();
                    sb2.Append($"  {item.itemName ?? item.weaponName ?? "(unnamed)"}");
                    for (int w = 0; w < 5; w++)
                        if (priceCounts[w] > 0)
                            sb2.Append($"\n    {WearSuffixes[w]}: ${priceSums[w] / priceCounts[w]:F2} (avg ×{priceCounts[w]})");
                    exMatch.Add(sb2.ToString());
                }

                if (!dryRun)
                {
                    bool changed = ApplyPrices(item, priceSums, priceCounts);
                    if (changed)
                    {
                        updated++;
                        EditorUtility.SetDirty(card);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(card);
                    }
                }
            }
            else
            {
                unmatched++;
                if (exNoMatch.Count < 5)
                    exNoMatch.Add($"  {item.itemName ?? item.weaponName ?? "(unnamed)"}");
            }
        }

        // ── Summary ──────────────────────────────────────────────────────────
        Log($"Results:  matched={matched}  unmatched={unmatched}  {(dryRun ? "(dry run — no data changed)" : $"updated={updated}")}");

        if (exMatch.Count > 0)
        {
            Log("\nExample matched items:");
            foreach (var ex in exMatch) Log(ex);
        }

        if (exNoMatch.Count > 0)
        {
            Log("\nExample unmatched items (no price found in any market):");
            foreach (var ex in exNoMatch) Log(ex);
            Log("  → Set marketHashName on those CaseItemData entries to match the API key exactly.");
        }

        if (!dryRun)
        {
            if (updated > 0)
            {
                AssetDatabase.SaveAssets();
                Log($"\nSaved {updated} updated item(s).");

                // If in Play Mode, trigger an immediate live inventory reprice
                if (EditorApplication.isPlaying && SkinInventoryManager.Instance != null)
                {
                    SkinInventoryManager.Instance.AutoRepriceAfterLoad();
                    Log("Triggered live inventory reprice (Play Mode active).");
                }
                else
                {
                    Log("Not in Play Mode — inventory will reprice automatically on next game load.");
                }
            }
            else
            {
                Log("\nNo items were updated (prices matched but values unchanged, or all unmatched).");
            }
        }
        else
        {
            Log("\n=== DRY RUN complete — no data was modified ===");
        }
    }

    // ── Price search ──────────────────────────────────────────────────────────
    /// <summary>
    /// Locates the price for a single market_hash_name inside a raw JSON string.
    /// Handles the two most common CSPriceAPI response shapes:
    ///   Shape A — value is a number:  "AK-47 | Redline (FN)": 45.50
    ///   Shape B — value is an object: "AK-47 | Redline (FN)": { "price": 45.50, ... }
    /// Also scans inside common wrapper keys ("data", "items") if present.
    /// Returns 0 if not found or not parseable.
    /// </summary>
    private static double FindPrice(string json, string hashName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(hashName))
            return 0.0;

        // Build the exact JSON key string (with quotes and escaping)
        string jsonKey = "\"" + hashName.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        int keyPos = json.IndexOf(jsonKey, StringComparison.Ordinal);
        if (keyPos < 0) return 0.0;

        int pos = keyPos + jsonKey.Length;
        pos = SkipWs(json, pos);
        if (pos >= json.Length || json[pos] != ':') return 0.0;
        pos = SkipWs(json, pos + 1);
        if (pos >= json.Length) return 0.0;

        char c = json[pos];

        // Shape A: bare number
        if (c == '-' || char.IsDigit(c))
            return ParseDouble(json, pos);

        // Shape B: object — scan for known price keys
        if (c == '{')
        {
            int closePos = FindClosingBrace(json, pos);
            string[] priceKeys = { "\"price\"", "\"lowest_price\"", "\"avg_price\"", "\"value\"", "\"avg\"" };
            foreach (string pk in priceKeys)
            {
                int pkPos = json.IndexOf(pk, pos, StringComparison.OrdinalIgnoreCase);
                if (pkPos < 0 || pkPos >= closePos) continue;

                int vp = SkipWs(json, pkPos + pk.Length);
                if (vp >= json.Length || json[vp] != ':') continue;
                vp = SkipWs(json, vp + 1);

                double v = ParseDouble(json, vp);
                if (v > 0.0) return v;
            }
        }

        return 0.0;
    }

    // ── Market hash name builder ──────────────────────────────────────────────
    /// <summary>
    /// Constructs the Steam market_hash_name for a given item/wear/StatTrak combination.
    /// Format examples:
    ///   "AK-47 | Redline (Factory New)"
    ///   "StatTrak™ AK-47 | Redline (Field-Tested)"
    ///   "★ Karambit | Doppler (Factory New)"
    ///   "★ StatTrak™ M9 Bayonet | Slaughter (Minimal Wear)"
    /// </summary>
    private static string BuildHashName(CaseItemData item, int wearIdx, bool isStatTrak)
    {
        // If the item has an explicit marketHashName, use it as the base (strip wear suffix, re-add below)
        string baseName = "";

        if (!string.IsNullOrWhiteSpace(item.marketHashName))
        {
            // Strip any existing wear suffix so we can re-append the correct one
            string mhn = item.marketHashName.Trim();
            foreach (string ws in WearSuffixes)
            {
                if (mhn.EndsWith(ws, StringComparison.OrdinalIgnoreCase))
                {
                    mhn = mhn.Substring(0, mhn.Length - ws.Length).TrimEnd();
                    break;
                }
            }
            baseName = mhn;
        }
        else
        {
            // Build from weapon + skin
            item.GetDisplayNames(out string weapon, out string skin, out _);
            bool isKnife = item.GetEffectiveRarity() == ItemRarity.Knife;

            string core = (!string.IsNullOrWhiteSpace(skin) &&
                           !skin.Equals(weapon, StringComparison.OrdinalIgnoreCase))
                ? $"{weapon} | {skin}"
                : weapon;

            if (isKnife && isStatTrak) baseName = $"★ StatTrak™ {core}";
            else if (isKnife)          baseName = $"★ {core}";
            else if (isStatTrak)       baseName = $"StatTrak™ {core}";
            else                       baseName = core;
        }

        if (string.IsNullOrWhiteSpace(baseName)) return string.Empty;
        return $"{baseName} {WearSuffixes[wearIdx]}";
    }

    // ── Apply prices ──────────────────────────────────────────────────────────
    private static bool ApplyPrices(CaseItemData item, double[] sums, int[] counts)
    {
        if (item.cachedPrices == null)
            item.cachedPrices = new ItemPriceData();

        var p = item.cachedPrices;
        bool changed = false;

        // Only overwrites a tier if we have valid new data (count > 0 and average > 0).
        double Avg(int i) => counts[i] > 0 ? sums[i] / counts[i] : 0.0;

        if (Avg(0) > 0) { p.factoryNew    = Avg(0); changed = true; }
        if (Avg(1) > 0) { p.minimalWear   = Avg(1); changed = true; }
        if (Avg(2) > 0) { p.fieldTested   = Avg(2); changed = true; }
        if (Avg(3) > 0) { p.wellWorn      = Avg(3); changed = true; }
        if (Avg(4) > 0) { p.battleScarred = Avg(4); changed = true; }

        if (Avg(5) > 0) { p.statTrakFactoryNew    = Avg(5); changed = true; }
        if (Avg(6) > 0) { p.statTrakMinimalWear   = Avg(6); changed = true; }
        if (Avg(7) > 0) { p.statTrakFieldTested   = Avg(7); changed = true; }
        if (Avg(8) > 0) { p.statTrakWellWorn      = Avg(8); changed = true; }
        if (Avg(9) > 0) { p.statTrakBattleScarred = Avg(9); changed = true; }

        if (changed)
        {
            p.currency       = "USD";
            p.lastUpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        return changed;
    }

    // ── Target item collection ────────────────────────────────────────────────
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

    // ── JSON helpers ──────────────────────────────────────────────────────────
    private static int SkipWs(string s, int pos)
    {
        while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\r' || s[pos] == '\n'))
            pos++;
        return pos;
    }

    private static int FindClosingBrace(string s, int openPos)
    {
        int depth = 0;
        for (int i = openPos; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}') { depth--; if (depth == 0) return i; }
        }
        return s.Length - 1;
    }

    private static double ParseDouble(string s, int pos)
    {
        if (pos >= s.Length) return 0.0;
        int start = pos;
        if (s[pos] == '-') pos++;
        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' ||
               s[pos] == 'e' || s[pos] == 'E' || s[pos] == '+' || s[pos] == '-'))
            pos++;
        if (pos == start) return 0.0;
        return double.TryParse(s.Substring(start, pos - start),
            NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0.0 ? v : 0.0;
    }

    // ── Utility ───────────────────────────────────────────────────────────────
    private bool HasAnyMarket() { foreach (var v in _marketOn) if (v) return true; return false; }
    private int  CountMarkets() { int n = 0; foreach (var v in _marketOn) if (v) n++; return n; }

    private void Log(string msg) { _log.AppendLine(msg); Repaint(); }
}
#endif
