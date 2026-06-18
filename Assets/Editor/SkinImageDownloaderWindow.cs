#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  Skin Image Downloader — Stage 4B
//  Open via: Tools → CaseClicker → Skin Image Downloader
//
//  Downloads skin images from CaseItemData.itemIconUrl, saves them as PNG
//  Sprites in the project, and assigns them as CaseItemData.itemIcon.
//
//  Prerequisites:
//    • Run ByMykel Bulk Case Importer first so itemIconUrl is populated.
//    • Internet access (images are hosted on ByMykel's GitHub CDN).
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class SkinImageDownloaderWindow : EditorWindow
{
    // ── UI state ──────────────────────────────────────────────────────────────
    private bool       _allInScene   = true;
    private CaseCardUI _targetCard;
    private string     _outputFolder = "Assets/Images/Skins";
    private Vector2    _scroll;
    private readonly StringBuilder _log = new StringBuilder();

    // ── Download pipeline ─────────────────────────────────────────────────────
    private bool   _isDownloading;
    private int    _totalJobs;
    private int    _doneJobs;

    // One job = one unique URL (deduplicated across all cases)
    private class DownloadJob
    {
        public string url;
        public string assetPath;           // e.g. Assets/Images/Skins/skin-ak47-inheritance.png
        public List<(CaseCardUI card, CaseItemData item)> targets = new List<(CaseCardUI, CaseItemData)>();
    }

    private readonly Queue<DownloadJob> _queue        = new Queue<DownloadJob>();
    private UnityWebRequest             _activeRequest;
    private DownloadJob                 _activeJob;

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/Skin Image Downloader")]
    public static void ShowWindow() => GetWindow<SkinImageDownloaderWindow>("Skin Image Downloader");

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void OnDisable() => CancelDownload();

    // ══════════════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Skin Image Downloader", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Downloads skin images from CaseItemData.itemIconUrl and assigns them as itemIcon sprites.\n" +
            "Run the ByMykel Bulk Case Importer first to populate itemIconUrl.",
            MessageType.None);

        EditorGUILayout.Space(4);

        // ── Target ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        _allInScene = EditorGUILayout.Toggle("All CaseCardUI in Scene", _allInScene);
        if (!_allInScene)
            _targetCard = (CaseCardUI)EditorGUILayout.ObjectField("Specific Card", _targetCard, typeof(CaseCardUI), true);

        // ── Output folder ─────────────────────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
        if (GUILayout.Button("…", GUILayout.Width(28)))
        {
            string abs = EditorUtility.OpenFolderPanel("Select output folder", Application.dataPath, "Skins");
            if (!string.IsNullOrEmpty(abs) && abs.StartsWith(Application.dataPath))
                _outputFolder = "Assets" + abs.Substring(Application.dataPath.Length).Replace("\\", "/");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Progress ──────────────────────────────────────────────────────────
        if (_isDownloading)
        {
            float progress = _totalJobs > 0 ? (float)_doneJobs / _totalJobs : 0f;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 18),
                progress, $"{_doneJobs} / {_totalJobs}  ({_activeJob?.url ?? "…"})");
            EditorGUILayout.Space(2);
        }

        // ── Actions ───────────────────────────────────────────────────────────
        using (new EditorGUI.DisabledScope(_isDownloading))
        {
            if (GUILayout.Button("Dry Run  (count items — no download)"))
                RunDryRun();

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Download Missing Images"))
                StartDownload(reassignOnly: false);

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Reassign Existing Images  (no download)"))
                StartDownload(reassignOnly: true);
        }

        EditorGUILayout.Space(4);

        // ── Log ───────────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(280));
        EditorGUILayout.TextArea(_log.ToString(), EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Dry Run
    // ══════════════════════════════════════════════════════════════════════════
    private void RunDryRun()
    {
        _log.Clear();
        Log("=== DRY RUN ===");

        var all = CollectAllItems();
        Log($"Total items scanned : {all.Count}");

        int hasIcon    = 0;
        int hasUrl     = 0;
        int missingAll = 0;
        var exNeedDownload = new List<string>();
        var exMissingUrl   = new List<string>();

        foreach (var (_, item) in all)
        {
            bool iconAssigned = item.itemIcon != null;
            bool hasIconUrl   = !string.IsNullOrEmpty(item.itemIconUrl);

            if (iconAssigned)       hasIcon++;
            else if (hasIconUrl)  { hasUrl++;     if (exNeedDownload.Count < 5) exNeedDownload.Add($"  {item.itemName}  →  {item.itemIconUrl}"); }
            else                  { missingAll++; if (exMissingUrl.Count  < 5) exMissingUrl.Add($"  {item.itemName}"); }
        }

        Log($"itemIcon assigned   : {hasIcon}");
        Log($"Need download       : {hasUrl}  (have URL, no Sprite)");
        Log($"Missing URL + icon  : {missingAll}  (no URL — run ByMykel importer first)");
        Log("");

        if (exNeedDownload.Count > 0)
        {
            Log("Example items to download:");
            foreach (var ex in exNeedDownload) Log(ex);
            Log("");
        }

        if (exMissingUrl.Count > 0)
        {
            Log("Example items with no URL:");
            foreach (var ex in exMissingUrl) Log(ex);
        }

        Log("\n=== DRY RUN complete ===");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Download / Reassign
    // ══════════════════════════════════════════════════════════════════════════
    private void StartDownload(bool reassignOnly)
    {
        _log.Clear();
        Log(reassignOnly ? "=== REASSIGN EXISTING IMAGES ===" : "=== DOWNLOAD MISSING IMAGES ===");

        EnsureOutputFolder();

        var all = CollectAllItems();
        Log($"Items scanned: {all.Count}");

        // Build deduplicated job list (one job per unique URL)
        var jobsByUrl = new Dictionary<string, DownloadJob>(StringComparer.OrdinalIgnoreCase);

        foreach (var (card, item) in all)
        {
            if (item.itemIcon != null) continue;                      // already has icon
            if (string.IsNullOrEmpty(item.itemIconUrl)) continue;     // no URL

            string assetPath = BuildAssetPath(item);

            if (!jobsByUrl.TryGetValue(item.itemIconUrl, out var job))
            {
                job = new DownloadJob { url = item.itemIconUrl, assetPath = assetPath };
                jobsByUrl[item.itemIconUrl] = job;
            }
            job.targets.Add((card, item));
        }

        if (jobsByUrl.Count == 0)
        {
            Log("Nothing to do — all items either have a Sprite assigned or no URL.");
            return;
        }

        // Separate into "file already exists" and "needs download"
        var needDownload  = new List<DownloadJob>();
        var canReassign   = new List<DownloadJob>();

        foreach (var job in jobsByUrl.Values)
        {
            if (File.Exists(Path.Combine(Application.dataPath, "../", job.assetPath)))
                canReassign.Add(job);
            else
                needDownload.Add(job);
        }

        // Reassign existing files immediately (no download needed)
        int reassigned = 0;
        foreach (var job in canReassign)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(job.assetPath);
            if (sprite != null)
            {
                AssignSprite(job, sprite);
                reassigned++;
            }
            else
            {
                // File exists on disk but not imported yet — import first
                AssetDatabase.ImportAsset(job.assetPath);
                EnsureSpriteImportSettings(job.assetPath);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(job.assetPath);
                if (sprite != null) { AssignSprite(job, sprite); reassigned++; }
            }
        }
        if (reassigned > 0) Log($"Reassigned {reassigned} existing image(s) without downloading.");

        if (reassignOnly || needDownload.Count == 0)
        {
            if (needDownload.Count > 0)
                Log($"{needDownload.Count} image(s) would need downloading (run 'Download Missing' to fetch them).");
            FinalizeAndSave();
            return;
        }

        Log($"Queuing {needDownload.Count} download(s)…");

        _queue.Clear();
        foreach (var job in needDownload) _queue.Enqueue(job);

        _totalJobs     = needDownload.Count;
        _doneJobs      = 0;
        _isDownloading = true;
        EditorApplication.update += PollDownload;
        StartNextDownload();
        Repaint();
    }

    // ── Download pipeline ─────────────────────────────────────────────────────
    private void StartNextDownload()
    {
        if (_queue.Count == 0) { FinishDownload(); return; }

        _activeJob     = _queue.Dequeue();
        _activeRequest = UnityWebRequestTexture.GetTexture(_activeJob.url);
        _activeRequest.SendWebRequest();
        Repaint();
    }

    private void PollDownload()
    {
        if (_activeRequest == null || !_activeRequest.isDone) return;

        if (_activeRequest.result == UnityWebRequest.Result.Success)
        {
            Texture2D tex = DownloadHandlerTexture.GetContent(_activeRequest);
            if (tex != null)
            {
                SaveAndAssign(_activeJob, tex);
                _doneJobs++;
            }
            else
            {
                Log($"  ✗ Null texture for {_activeJob.url}");
            }
        }
        else
        {
            Log($"  ✗ Download failed ({_activeRequest.responseCode}): {_activeJob.url}");
        }

        _activeRequest.Dispose();
        _activeRequest = null;
        StartNextDownload();
        Repaint();
    }

    private void FinishDownload()
    {
        EditorApplication.update -= PollDownload;
        _isDownloading = false;
        _activeJob     = null;
        Log($"\nDownloads complete: {_doneJobs} / {_totalJobs} succeeded.");
        FinalizeAndSave();
        Repaint();
    }

    private void CancelDownload()
    {
        EditorApplication.update -= PollDownload;
        _isDownloading = false;
        _activeRequest?.Dispose();
        _activeRequest = null;
    }

    // ── Save texture as PNG and assign sprite ─────────────────────────────────
    private void SaveAndAssign(DownloadJob job, Texture2D tex)
    {
        try
        {
            byte[] png = tex.EncodeToPNG();
            string fullPath = Path.GetFullPath(job.assetPath).Replace("\\", "/");
            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(job.assetPath, ImportAssetOptions.ForceUpdate);
            EnsureSpriteImportSettings(job.assetPath);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(job.assetPath);
            if (sprite != null)
            {
                AssignSprite(job, sprite);
                Log($"  ✓ {job.assetPath}  ({job.targets.Count} item(s))");
            }
            else
            {
                Log($"  ! Sprite not found after import: {job.assetPath}");
            }
        }
        catch (Exception ex)
        {
            Log($"  ! Save error for {job.assetPath}: {ex.Message}");
        }
    }

    private static void AssignSprite(DownloadJob job, Sprite sprite)
    {
        foreach (var (card, item) in job.targets)
        {
            item.itemIcon = sprite;
            EditorUtility.SetDirty(card);
            PrefabUtility.RecordPrefabInstancePropertyModifications(card);
        }
    }

    private static void EnsureSpriteImportSettings(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite) return; // already correct

        importer.textureType       = TextureImporterType.Sprite;
        importer.spriteImportMode  = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private void FinalizeAndSave()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Log("Assets saved.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════
    private List<(CaseCardUI card, CaseItemData item)> CollectAllItems()
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

    /// <summary>
    /// Builds the local asset path for an item's image.
    /// Priority: itemId → marketHashName → itemName.
    /// Sanitises the name to be a safe filename.
    /// </summary>
    private string BuildAssetPath(CaseItemData item)
    {
        string baseName = !string.IsNullOrEmpty(item.itemId)
            ? item.itemId
            : !string.IsNullOrEmpty(item.marketHashName)
                ? item.marketHashName
                : item.itemName ?? "unknown";

        string safe = SanitiseFilename(baseName);
        return $"{_outputFolder}/{safe}.png";
    }

    private static string SanitiseFilename(string name)
    {
        // Replace invalid characters with underscores, collapse runs
        string s = Regex.Replace(name, @"[^\w\-.]", "_");
        s = Regex.Replace(s, @"_+", "_").Trim('_');
        return s.Length > 0 ? s : "skin";
    }

    private void EnsureOutputFolder()
    {
        string fullPath = Path.GetFullPath(_outputFolder);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            // Create intermediate Assets/.../... folders via AssetDatabase
            AssetDatabase.Refresh();
            Log($"Created output folder: {_outputFolder}");
        }
    }

    private void Log(string msg) { _log.AppendLine(msg); Repaint(); }
}
#endif
