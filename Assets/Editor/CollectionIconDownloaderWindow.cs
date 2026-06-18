#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  Collection Icon Downloader
//  Open via: Tools → CaseClicker → Collection Icon Downloader
//
//  Downloads collection icons from CaseItemData.collectionIconUrl and assigns
//  them as Sprites to CaseItemData.collectionIcon.
//
//  Deduplicates by collectionId so each unique collection downloads one image,
//  then assigns it to every CaseItemData that belongs to that collection.
// ══════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class CollectionIconDownloaderWindow : EditorWindow
{
    // ── UI state ──────────────────────────────────────────────────────────────
    private bool       _allInScene    = true;
    private CaseCardUI _targetCard;
    private string     _outputFolder  = "Assets/Images/Collections";
    private Vector2    _scroll;
    private readonly StringBuilder _log = new StringBuilder();

    // ── Download pipeline state ───────────────────────────────────────────────
    private struct DownloadJob
    {
        public string              collectionId;
        public string              collectionName;
        public string              url;
        public string              assetPath;
        public List<CaseItemData>  targets;   // all items sharing this collection
        public List<UnityEngine.Object> parents;  // their owning objects for SetDirty
    }

    private Queue<DownloadJob>  _downloadQueue;
    private DownloadJob         _activeJob;
    private UnityWebRequest     _activeRequest;
    private bool                _isRunning;
    private int                 _totalJobs;
    private int                 _doneJobs;

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/Collection Icon Downloader")]
    public static void ShowWindow() => GetWindow<CollectionIconDownloaderWindow>("Collection Icons");

    // ══════════════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Collection Icon Downloader", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Downloads collection icons from CaseItemData.collectionIconUrl and assigns them as Sprites.\n" +
            "Run ByMykel Bulk Case Importer first to populate collectionIconUrl fields.",
            MessageType.None);

        EditorGUILayout.Space(4);

        // ── Target ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        _allInScene = EditorGUILayout.Toggle("All CaseCardUI in Scene", _allInScene);
        if (!_allInScene)
            _targetCard = (CaseCardUI)EditorGUILayout.ObjectField("Specific Card", _targetCard, typeof(CaseCardUI), true);

        EditorGUILayout.Space(4);

        // ── Output ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(_isRunning))
        {
            if (GUILayout.Button("Dry Run  (scan — no downloads)"))
                RunDryRun();

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Download Missing Icons"))
                StartDownload(reassignOnly: false);

            EditorGUILayout.Space(2);

            if (GUILayout.Button("Reassign Existing Icons  (no download)"))
                StartDownload(reassignOnly: true);
        }

        if (_isRunning)
        {
            EditorGUILayout.Space(4);
            float progress = _totalJobs > 0 ? (float)_doneJobs / _totalJobs : 0f;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(18)),
                progress, $"{_doneJobs} / {_totalJobs}");
        }

        EditorGUILayout.Space(4);

        // ── Log ───────────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(280));
        EditorGUILayout.TextArea(_log.ToString(), EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Dry run
    // ══════════════════════════════════════════════════════════════════════════
    private void RunDryRun()
    {
        _log.Clear();
        Log("=== DRY RUN ===");

        var (collMap, parentMap) = BuildCollectionMap();

        int withIcon    = 0;
        int withUrl     = 0;
        int noUrl       = 0;
        int fileExists  = 0;
        var exUrl       = new List<string>();
        var exNo        = new List<string>();

        foreach (var kv in collMap)
        {
            string collId   = kv.Key;
            var    targets  = kv.Value;
            bool   hasIcon  = targets[0].collectionIcon != null;
            bool   hasUrl   = !string.IsNullOrEmpty(targets[0].collectionIconUrl);
            string assetPath = BuildAssetPath(targets[0]);

            if (hasIcon)  withIcon++;
            if (hasUrl)   withUrl++;
            else          noUrl++;

            if (hasUrl && File.Exists(Path.GetFullPath(assetPath))) fileExists++;

            if (hasUrl && exUrl.Count < 4)
                exUrl.Add($"  [{targets[0].collectionName ?? collId}]  {targets.Count} item(s)  → {assetPath}");
            if (!hasUrl && exNo.Count < 4)
                exNo.Add($"  [{targets[0].collectionName ?? collId}]  {targets[0].itemName}");
        }

        Log($"Unique collections : {collMap.Count}");
        Log($"  Already have Sprite  : {withIcon}");
        Log($"  Have URL (can dl)    : {withUrl}");
        Log($"    File already exists: {fileExists}");
        Log($"  Missing URL          : {noUrl}");
        Log("");
        if (exUrl.Count > 0)
        {
            Log("Example collections with URL:");
            foreach (var ex in exUrl) Log(ex);
            Log("");
        }
        if (exNo.Count > 0)
        {
            Log("Example collections missing URL:");
            foreach (var ex in exNo) Log(ex);
            Log("");
        }
        Log("=== DRY RUN complete — no data changed ===");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Download / assign pipeline
    // ══════════════════════════════════════════════════════════════════════════
    private void StartDownload(bool reassignOnly)
    {
        _log.Clear();
        Log(reassignOnly ? "=== REASSIGN EXISTING ===" : "=== DOWNLOAD MISSING ===");

        EnsureOutputFolder();

        var (collMap, parentMap) = BuildCollectionMap();
        _downloadQueue = new Queue<DownloadJob>();

        foreach (var kv in collMap)
        {
            string collId  = kv.Key;
            var    targets = kv.Value;

            // Collect parent objects for SetDirty
            var parents = new List<UnityEngine.Object>();
            if (parentMap.TryGetValue(collId, out var ps)) parents = ps;

            string assetPath = BuildAssetPath(targets[0]);
            string fullPath  = Path.GetFullPath(assetPath);
            bool   hasUrl    = !string.IsNullOrEmpty(targets[0].collectionIconUrl);
            bool   fileExists = File.Exists(fullPath);

            if (reassignOnly)
            {
                if (fileExists)
                    _downloadQueue.Enqueue(new DownloadJob
                    {
                        collectionId   = collId,
                        collectionName = targets[0].collectionName,
                        url            = "",           // no download needed
                        assetPath      = assetPath,
                        targets        = targets,
                        parents        = parents
                    });
            }
            else
            {
                // Skip items that already have a Sprite assigned and file exists
                if (targets[0].collectionIcon != null && fileExists) continue;
                if (!hasUrl) { Log($"  skip (no URL): {targets[0].collectionName ?? collId}"); continue; }
                if (fileExists)
                {
                    // File exists — just reassign without downloading
                    _downloadQueue.Enqueue(new DownloadJob
                    {
                        collectionId   = collId,
                        collectionName = targets[0].collectionName,
                        url            = "",
                        assetPath      = assetPath,
                        targets        = targets,
                        parents        = parents
                    });
                }
                else
                {
                    _downloadQueue.Enqueue(new DownloadJob
                    {
                        collectionId   = collId,
                        collectionName = targets[0].collectionName,
                        url            = targets[0].collectionIconUrl,
                        assetPath      = assetPath,
                        targets        = targets,
                        parents        = parents
                    });
                }
            }
        }

        _totalJobs = _downloadQueue.Count;
        _doneJobs  = 0;

        if (_totalJobs == 0)
        {
            Log("Nothing to do.");
            return;
        }

        Log($"Jobs queued: {_totalJobs}");
        _isRunning = true;
        EditorApplication.update += ProcessQueue;
        ProcessQueue();
    }

    private void ProcessQueue()
    {
        // If a request is in-flight, wait for it
        if (_activeRequest != null)
        {
            if (!_activeRequest.isDone) return;

            // Request finished
            if (_activeRequest.result == UnityWebRequest.Result.Success)
            {
                byte[] bytes = _activeRequest.downloadHandler.data;
                SaveAndAssign(bytes, _activeJob);
            }
            else
            {
                Log($"  ✗ download failed [{_activeJob.collectionName ?? _activeJob.collectionId}]: {_activeRequest.error}");
            }

            _activeRequest.Dispose();
            _activeRequest = null;
            _doneJobs++;
            Repaint();
        }

        // Start next job
        if (_downloadQueue != null && _downloadQueue.Count > 0)
        {
            _activeJob = _downloadQueue.Dequeue();

            if (string.IsNullOrEmpty(_activeJob.url))
            {
                // No download needed — just load the existing file
                AssignExisting(_activeJob);
                _doneJobs++;
                Repaint();
            }
            else
            {
                _activeRequest = UnityWebRequestTexture.GetTexture(_activeJob.url);
                _activeRequest.SendWebRequest();
            }
        }
        else if (_activeRequest == null)
        {
            // All done
            _isRunning = false;
            EditorApplication.update -= ProcessQueue;
            AssetDatabase.SaveAssets();
            Log($"\nDone. {_doneJobs} collection icon(s) processed.");
            Repaint();
        }
    }

    // ── Save downloaded bytes, import, and assign ─────────────────────────────
    private void SaveAndAssign(byte[] bytes, DownloadJob job)
    {
        try
        {
            string fullPath = Path.GetFullPath(job.assetPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.ImportAsset(job.assetPath);

            // Configure as Sprite
            var importer = AssetImporter.GetAtPath(job.assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType  = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(job.assetPath);
            if (sprite == null)
            {
                Log($"  ✗ sprite load failed: {job.assetPath}");
                return;
            }

            AssignSprite(sprite, job);
            Log($"  ✓ {job.collectionName ?? job.collectionId}  → {job.assetPath}  ({job.targets.Count} item(s) updated)");
        }
        catch (Exception ex)
        {
            Log($"  ✗ error for [{job.collectionName ?? job.collectionId}]: {ex.Message}");
        }
    }

    // ── Load from existing file and assign ────────────────────────────────────
    private void AssignExisting(DownloadJob job)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(job.assetPath);
        if (sprite == null)
        {
            Log($"  ✗ file exists but no sprite: {job.assetPath}");
            return;
        }
        AssignSprite(sprite, job);
        Log($"  ✓ reassigned [{job.collectionName ?? job.collectionId}]  ({job.targets.Count} item(s))");
    }

    // ── Assign Sprite to all target items ─────────────────────────────────────
    private static void AssignSprite(Sprite sprite, DownloadJob job)
    {
        foreach (var item in job.targets)
            item.collectionIcon = sprite;
        foreach (var parent in job.parents)
            EditorUtility.SetDirty(parent);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Scans selected CaseCardUI objects and builds:
    ///   collectionMap  — collectionId → List of CaseItemData that share it
    ///   parentMap      — collectionId → List of parent objects (CaseCardUI) for SetDirty
    /// Only includes items with overrideCollection = true.
    /// </summary>
    private (Dictionary<string, List<CaseItemData>>, Dictionary<string, List<UnityEngine.Object>>) BuildCollectionMap()
    {
        CaseCardUI[] cards = _allInScene
            ? UnityEngine.Object.FindObjectsByType<CaseCardUI>(FindObjectsSortMode.None)
            : (_targetCard != null ? new[] { _targetCard } : Array.Empty<CaseCardUI>());

        var collMap   = new Dictionary<string, List<CaseItemData>>(StringComparer.OrdinalIgnoreCase);
        var parentMap = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            if (card?.data?.possibleItems == null) continue;
            foreach (var item in card.data.possibleItems)
            {
                if (item == null) continue;
                // Only items that override collection (i.e. have item-level collection data)
                if (!item.overrideCollection) continue;

                string key = !string.IsNullOrEmpty(item.collectionId)
                    ? item.collectionId
                    : item.collectionName ?? "";
                if (string.IsNullOrEmpty(key)) continue;

                if (!collMap.ContainsKey(key))
                {
                    collMap[key]   = new List<CaseItemData>();
                    parentMap[key] = new List<UnityEngine.Object>();
                }

                collMap[key].Add(item);
                if (!parentMap[key].Contains(card))
                    parentMap[key].Add(card);
            }
        }

        return (collMap, parentMap);
    }

    private string BuildAssetPath(CaseItemData item)
    {
        string baseName = !string.IsNullOrEmpty(item.collectionId)
            ? item.collectionId
            : item.collectionName ?? "collection";
        string safe = Regex.Replace(baseName, @"[^\w\-]", "_").Trim('_');
        if (string.IsNullOrEmpty(safe)) safe = "collection";
        return $"{_outputFolder.TrimEnd('/')}/{safe}.png";
    }

    private void EnsureOutputFolder()
    {
        string folder = _outputFolder.TrimEnd('/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/") ?? "Assets";
            string child  = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }

    private void Log(string msg)
    {
        _log.AppendLine(msg);
        Debug.Log("[CollectionIconDL] " + msg);
        Repaint();
    }
}
#endif
