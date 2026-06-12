using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class TextureCropperWindow : EditorWindow
{
    private readonly List<CropEntry> _entries = new();
    private Vector2 _scrollPos;
    private int _padding;
    private int _alphaThreshold = 1;
    private bool _overwriteOriginal;

    [MenuItem("Tools/Texture Cropper")]
    public static void ShowWindow()
    {
        var window = GetWindow<TextureCropperWindow>("贴图裁剪工具");
        window.minSize = new Vector2(400, 350);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);

        // ── Drop area ──
        var dropRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.helpBox, GUILayout.Height(60));
        GUI.Box(dropRect, "拖拽贴图到此处\n(Drag textures here)", EditorStyles.centeredGreyMiniLabel);

        HandleDrop(dropRect);

        EditorGUILayout.Space(5);

        // ── Options ──
        EditorGUILayout.BeginHorizontal();
        _padding = EditorGUILayout.IntField("边距 (Padding)", _padding);
        _alphaThreshold = EditorGUILayout.IntSlider("透明阈值 (Alpha Threshold)", _alphaThreshold, 0, 255);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _overwriteOriginal = EditorGUILayout.Toggle("覆盖原文件 (Overwrite)", _overwriteOriginal);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("刷新预览", GUILayout.Width(80)))
        {
            foreach (var e in _entries)
                e.Recompute(_alphaThreshold, _padding);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // ── Entry list ──
        EditorGUILayout.LabelField($"贴图列表 ({_entries.Count})", EditorStyles.boldLabel);

        if (_entries.Count > 0)
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                DrawEntry(_entries[i], i);
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(5);

        // ── Bottom buttons ──
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空列表", GUILayout.Height(30)))
        {
            _entries.Clear();
        }
        GUI.enabled = _entries.Count > 0;
        if (GUILayout.Button("开始裁剪", GUILayout.Height(30), GUILayout.MinWidth(120)))
        {
            CropAll();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void HandleDrop(Rect dropRect)
    {
        var evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition)) return;

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D tex && !Contains(tex))
                            _entries.Add(new CropEntry(tex, _alphaThreshold, _padding));
                    }
                }

                evt.Use();
                break;
        }
    }

    private bool Contains(Texture2D tex)
    {
        foreach (var e in _entries)
            if (e.OriginalTexture == tex) return true;
        return false;
    }

    private void DrawEntry(CropEntry entry, int index)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        // Thumbnail
        var rect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
        EditorGUI.DrawPreviewTexture(rect, entry.OriginalTexture);

        // Info
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(entry.OriginalTexture.name, EditorStyles.boldLabel);
        var orig = entry.OriginalSize;
        if (entry.HasCropPreview)
        {
            var crop = entry.CroppedSize;
            var reduction = (1f - (float)(crop.x * crop.y) / (orig.x * orig.y)) * 100f;
            EditorGUILayout.LabelField(
                $"{orig.x}x{orig.y}  →  {crop.x}x{crop.y}  (-{reduction:F0}%)",
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField($"{orig.x}x{orig.y}  —  无需裁剪 / 无法读取",
                EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        // Remove button
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(22)))
        {
            _entries.RemoveAt(index);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void CropAll()
    {
        int successCount = 0;
        int skipCount = 0;
        int failCount = 0;

        try
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                EditorUtility.DisplayProgressBar("裁剪中...",
                    entry.OriginalTexture.name, (float)i / _entries.Count);

                if (!entry.HasCropPreview)
                {
                    skipCount++;
                    continue;
                }

                try
                {
                    CropOne(entry);
                    successCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"裁剪失败: {entry.OriginalTexture.name} — {e.Message}");
                    failCount++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("裁剪完成",
            $"成功: {successCount}\n跳过: {skipCount}\n失败: {failCount}", "确定");
    }

    private void CropOne(CropEntry entry)
    {
        var tex = entry.OriginalTexture;
        var path = AssetDatabase.GetAssetPath(tex);
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);

        // Ensure readable
        bool wasReadable = EnsureReadable(path);

        // Get pixels via a readable copy
        var readableTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Color32[] pixels = readableTex.GetPixels32();
        int w = readableTex.width;
        int h = readableTex.height;

        // Compute crop rect
        var (minX, minY, maxX, maxY) = entry.CropRect;
        minX = Mathf.Max(0, minX - _padding);
        minY = Mathf.Max(0, minY - _padding);
        maxX = Mathf.Min(w - 1, maxX + _padding);
        maxY = Mathf.Min(h - 1, maxY + _padding);

        int newW = maxX - minX + 1;
        int newH = maxY - minY + 1;

        // Create cropped texture
        var cropped = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
        Color32[] croppedPixels = new Color32[newW * newH];
        for (int y = 0; y < newH; y++)
        {
            for (int x = 0; x < newW; x++)
            {
                croppedPixels[y * newW + x] = pixels[(minY + y) * w + (minX + x)];
            }
        }
        cropped.SetPixels32(croppedPixels);
        cropped.Apply();

        // Save
        byte[] pngData = ImageConversion.EncodeToPNG(cropped);
        string outPath;
        if (_overwriteOriginal)
        {
            outPath = path;
        }
        else
        {
            outPath = Path.Combine(dir, $"{name}_cropped.png");
            int suffix = 1;
            while (File.Exists(outPath))
                outPath = Path.Combine(dir, $"{name}_cropped_{suffix++}.png");
        }

        File.WriteAllBytes(outPath, pngData);
        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);

        // Restore readability setting if we changed it
        if (!wasReadable)
            RestoreReadability(path);

        // Cleanup temp texture
        if (cropped != null)
            DestroyImmediate(cropped);
    }

    private static bool EnsureReadable(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null || importer.isReadable)
            return true;

        importer.isReadable = true;
        importer.SaveAndReimport();
        return false;
    }

    private static void RestoreReadability(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    // ── Data ──

    private class CropEntry
    {
        public readonly Texture2D OriginalTexture;
        public readonly Vector2Int OriginalSize;
        public bool HasCropPreview;
        public Vector2Int CroppedSize;
        public int MinX, MinY, MaxX, MaxY;

        public (int, int, int, int) CropRect => (MinX, MinY, MaxX, MaxY);

        public void Recompute(int alphaThreshold, int padding)
        {
            ComputeCrop(alphaThreshold, padding);
        }

        public CropEntry(Texture2D tex, int alphaThreshold, int padding)
        {
            OriginalTexture = tex;
            OriginalSize = new Vector2Int(tex.width, tex.height);
            ComputeCrop(alphaThreshold, padding);
        }

        private void ComputeCrop(int alphaThreshold, int padding)
        {
            var path = AssetDatabase.GetAssetPath(OriginalTexture);
            if (string.IsNullOrEmpty(path))
            {
                HasCropPreview = false;
                return;
            }

            bool wasReadable = EnsureReadable(path);
            var readableTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (readableTex == null)
            {
                HasCropPreview = false;
                return;
            }

            try
            {
                Color32[] pixels = readableTex.GetPixels32();
                int w = readableTex.width;
                int h = readableTex.height;

                MinX = w;
                MinY = h;
                MaxX = 0;
                MaxY = 0;
                bool found = false;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (pixels[y * w + x].a > alphaThreshold)
                        {
                            found = true;
                            if (x < MinX) MinX = x;
                            if (y < MinY) MinY = y;
                            if (x > MaxX) MaxX = x;
                            if (y > MaxY) MaxY = y;
                        }
                    }
                }

                if (found && (MinX > 0 || MinY > 0 || MaxX < w - 1 || MaxY < h - 1))
                {
                    HasCropPreview = true;
                    // Apply padding to preview size (clamped to image bounds)
                    int pMinX = Mathf.Max(0, MinX - padding);
                    int pMinY = Mathf.Max(0, MinY - padding);
                    int pMaxX = Mathf.Min(w - 1, MaxX + padding);
                    int pMaxY = Mathf.Min(h - 1, MaxY + padding);
                    CroppedSize = new Vector2Int(pMaxX - pMinX + 1, pMaxY - pMinY + 1);
                }
                else
                {
                    HasCropPreview = false;
                }
            }
            finally
            {
                if (!wasReadable)
                    RestoreReadability(path);
            }
        }
    }
}
