using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that produces normalized copies of knife icon PNGs.
///
/// Menu: Tools → CaseClicker → Normalize Knife Icons
///
/// For each source PNG in Assets/images/knifes (excluding the Normalized sub-folder):
///   1. Load the texture pixels (makes it temporarily readable if needed).
///   2. Detect the alpha bounding box (tight crop around non-transparent pixels).
///   3. Crop to that bounding box.
///   4. Bilinear-scale the crop so its longest side fits inside MAX_KNIFE_SIZE pixels.
///   5. Center the scaled image on a transparent OUTPUT_SIZE × OUTPUT_SIZE canvas.
///   6. Save the result as a PNG to Assets/images/knifes/Normalized/ with the same filename.
///   7. (Re-)import it as Sprite (Single, Alpha Transparency, no mipmaps).
/// </summary>
public class NormalizeKnifeIconsWindow : EditorWindow
{
    // ── Constants ──────────────────────────────────────────────────────────────
    private const string SOURCE_FOLDER     = "Assets/images/knifes";
    private const string NORMALIZED_FOLDER = "Assets/images/knifes/Normalized";
    private const int    OUTPUT_SIZE       = 512;   // canvas size of each normalized PNG
    private const int    MAX_KNIFE_SIZE    = 430;   // longest side of knife after scaling
    private const float  ALPHA_THRESHOLD   = 0.01f; // pixels below this alpha are treated as transparent

    // ── Window state ───────────────────────────────────────────────────────────
    private Vector2 _scroll;
    private string  _log = "";

    // ── Menu ───────────────────────────────────────────────────────────────────
    [MenuItem("Tools/CaseClicker/Normalize Knife Icons")]
    public static void OpenWindow()
    {
        GetWindow<NormalizeKnifeIconsWindow>("Normalize Knife Icons").minSize = new Vector2(480, 360);
    }

    // ── GUI ────────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        GUILayout.Label("Knife Icon Normalizer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"Scans '{SOURCE_FOLDER}' (skips '{NORMALIZED_FOLDER}').\n" +
            $"Output: {OUTPUT_SIZE}×{OUTPUT_SIZE} canvas, knife longest side ≤ {MAX_KNIFE_SIZE} px.",
            MessageType.Info);

        GUILayout.Space(6);

        if (GUILayout.Button("Normalize All Knife Icons", GUILayout.Height(30)))
        {
            _log = "";
            NormalizeAll();
        }

        GUILayout.Space(6);
        GUILayout.Label("Log:", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ── Main pass ──────────────────────────────────────────────────────────────
    private void NormalizeAll()
    {
        // Gather source textures (exclude anything already inside Normalized/)
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SOURCE_FOLDER });

        var sourcePaths = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(NORMALIZED_FOLDER + "/", System.StringComparison.OrdinalIgnoreCase))
                continue;
            sourcePaths.Add(path);
        }

        Log($"Found {sourcePaths.Count} source texture(s) in '{SOURCE_FOLDER}'.");

        if (sourcePaths.Count == 0)
        {
            Log("Nothing to process.");
            Repaint();
            return;
        }

        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder(NORMALIZED_FOLDER))
        {
            AssetDatabase.CreateFolder(SOURCE_FOLDER, "Normalized");
            Log($"Created folder: {NORMALIZED_FOLDER}");
        }

        // Process each source texture
        int success = 0, fail = 0;
        var writtenPaths = new List<string>();

        foreach (string srcPath in sourcePaths)
        {
            try
            {
                string outPath = ProcessTexture(srcPath);
                if (outPath != null)
                {
                    writtenPaths.Add(outPath);
                    success++;
                }
                else
                {
                    fail++;
                }
            }
            catch (System.Exception ex)
            {
                Log($"  ERROR '{Path.GetFileName(srcPath)}': {ex.Message}");
                fail++;
            }
        }

        // Refresh so Unity discovers the new PNG files
        AssetDatabase.Refresh();

        // Apply Sprite import settings to every output file
        foreach (string outPath in writtenPaths)
            ApplySpriteImportSettings(outPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Log($"\nFinished — {success} normalized, {fail} failed/skipped.");
        Log($"Use sprites from '{NORMALIZED_FOLDER}' in ContainerInfoPanelUI.specialItemSprites.");
        Repaint();
    }

    // ── Per-texture processing ─────────────────────────────────────────────────

    /// <summary>
    /// Normalizes one texture. Returns the output asset path on success, null on skip/failure.
    /// </summary>
    private string ProcessTexture(string srcAssetPath)
    {
        string fileName = Path.GetFileName(srcAssetPath);

        // ── Make source readable ───────────────────────────────────────────────
        var srcImporter = AssetImporter.GetAtPath(srcAssetPath) as TextureImporter;
        bool wasReadable = false;

        if (srcImporter != null)
        {
            wasReadable = srcImporter.isReadable;
            if (!wasReadable)
            {
                srcImporter.isReadable = true;
                srcImporter.SaveAndReimport();
            }
        }

        Texture2D src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcAssetPath);

        if (src == null)
        {
            Log($"  SKIP (could not load): {fileName}");
            RestoreReadable(srcImporter, wasReadable);
            return null;
        }

        // ── Find alpha bounding box ────────────────────────────────────────────
        RectInt bounds = FindAlphaBounds(src);

        if (bounds.width <= 0 || bounds.height <= 0)
        {
            Log($"  SKIP (fully transparent): {fileName}");
            RestoreReadable(srcImporter, wasReadable);
            return null;
        }

        // ── Crop to bounds ─────────────────────────────────────────────────────
        Color[] cropped = src.GetPixels(bounds.x, bounds.y, bounds.width, bounds.height);

        // ── Restore original readable state ────────────────────────────────────
        RestoreReadable(srcImporter, wasReadable);

        // ── Scale so longest side ≤ MAX_KNIFE_SIZE ────────────────────────────
        float scale   = (float)MAX_KNIFE_SIZE / Mathf.Max(bounds.width, bounds.height);
        int   scaledW = Mathf.Max(1, Mathf.RoundToInt(bounds.width  * scale));
        int   scaledH = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));

        Color[] scaled = BilinearResize(cropped, bounds.width, bounds.height, scaledW, scaledH);

        // ── Compose centered on transparent OUTPUT_SIZE × OUTPUT_SIZE canvas ──
        int     offX   = (OUTPUT_SIZE - scaledW) / 2;
        int     offY   = (OUTPUT_SIZE - scaledH) / 2;
        Color[] canvas = new Color[OUTPUT_SIZE * OUTPUT_SIZE]; // default: transparent black

        for (int y = 0; y < scaledH; y++)
        {
            for (int x = 0; x < scaledW; x++)
            {
                int dstX = offX + x;
                int dstY = offY + y;
                if (dstX >= 0 && dstX < OUTPUT_SIZE && dstY >= 0 && dstY < OUTPUT_SIZE)
                    canvas[dstY * OUTPUT_SIZE + dstX] = scaled[y * scaledW + x];
            }
        }

        // ── Encode to PNG and write ────────────────────────────────────────────
        var outTex = new Texture2D(OUTPUT_SIZE, OUTPUT_SIZE, TextureFormat.RGBA32, false);
        outTex.SetPixels(canvas);
        outTex.Apply();
        byte[] png = outTex.EncodeToPNG();
        DestroyImmediate(outTex);

        string outAssetPath = $"{NORMALIZED_FOLDER}/{fileName}";
        string outFullPath  = Path.GetFullPath(
            Path.Combine(Application.dataPath, outAssetPath.Substring("Assets/".Length)));

        File.WriteAllBytes(outFullPath, png);
        Log($"  OK: {fileName}  " +
            $"{src.width}×{src.height} → crop {bounds.width}×{bounds.height} → " +
            $"scaled {scaledW}×{scaledH} → canvas {OUTPUT_SIZE}×{OUTPUT_SIZE}");

        return outAssetPath;
    }

    // ── Alpha bounds ───────────────────────────────────────────────────────────

    private static RectInt FindAlphaBounds(Texture2D tex)
    {
        int w = tex.width;
        int h = tex.height;
        Color[] pixels = tex.GetPixels();

        int minX = w, maxX = -1, minY = h, maxY = -1;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a > ALPHA_THRESHOLD)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY) return new RectInt(0, 0, 0, 0);

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // ── Bilinear resize ────────────────────────────────────────────────────────

    private static Color[] BilinearResize(Color[] src, int srcW, int srcH, int dstW, int dstH)
    {
        Color[] dst    = new Color[dstW * dstH];
        float   scaleX = srcW  > 1 ? (float)(srcW - 1) / (dstW - 1) : 0f;
        float   scaleY = srcH  > 1 ? (float)(srcH - 1) / (dstH - 1) : 0f;

        for (int y = 0; y < dstH; y++)
        {
            for (int x = 0; x < dstW; x++)
            {
                float fx = x * scaleX;
                float fy = y * scaleY;

                int x0 = Mathf.FloorToInt(fx);
                int y0 = Mathf.FloorToInt(fy);
                int x1 = Mathf.Min(x0 + 1, srcW - 1);
                int y1 = Mathf.Min(y0 + 1, srcH - 1);

                float tx = fx - x0;
                float ty = fy - y0;

                Color c00 = src[y0 * srcW + x0];
                Color c10 = src[y0 * srcW + x1];
                Color c01 = src[y1 * srcW + x0];
                Color c11 = src[y1 * srcW + x1];

                dst[y * dstW + x] = Color.Lerp(
                    Color.Lerp(c00, c10, tx),
                    Color.Lerp(c01, c11, tx),
                    ty);
            }
        }

        return dst;
    }

    // ── Import settings ────────────────────────────────────────────────────────

    private static void ApplySpriteImportSettings(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.alphaSource         = TextureImporterAlphaSource.FromInput;
        importer.isReadable          = false;
        importer.mipmapEnabled       = false;
        importer.filterMode          = FilterMode.Bilinear;
        importer.maxTextureSize      = 1024;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();
    }

    private static void RestoreReadable(TextureImporter importer, bool originalValue)
    {
        if (importer == null || importer.isReadable == originalValue) return;
        importer.isReadable = originalValue;
        importer.SaveAndReimport();
    }

    // ── Logging ────────────────────────────────────────────────────────────────

    private void Log(string message)
    {
        _log += message + "\n";
        Debug.Log("[NormalizeKnifeIcons] " + message);
    }
}
