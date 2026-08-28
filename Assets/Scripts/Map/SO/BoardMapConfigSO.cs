using System;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class BoardRegionConfig
{
    public string regionName = "森林区域";

    [Tooltip("拖入你拼好的章节 UI 预制体 (挂有 MapRegionLayout)")]
    public GameObject regionPrefab; 
}

[CreateAssetMenu(menuName = "Map/Board Map Config")]
public class BoardMapConfigSO : ScriptableObject
{
    [Header("Shared Presentation")]
    [Tooltip("整张地图共用的房间图标、节点效果图标、tooltip 和颜色配置")]
    public MapPresentationCatalogSO presentationCatalog;

    [Header("Node Passed Backgrounds")]
    [Tooltip("拖入这张地图所有已抵达节点背景图所在的文件夹。节点通过 Sprite 名称查找图片。")]
    public UnityEngine.Object nodePassedBackgroundFolder;
    [SerializeField, HideInInspector]
    private List<Sprite> nodePassedBackgroundSprites = new List<Sprite>();

    [Header("Regions")]
    [Tooltip("地图由哪些章节按顺序拼接而成")]
    public List<BoardRegionConfig> regions = new List<BoardRegionConfig>();

    [NonSerialized] private Dictionary<string, Sprite> _nodePassedBackgroundByName;

    public int NodePassedBackgroundCount => nodePassedBackgroundSprites != null
        ? nodePassedBackgroundSprites.Count
        : 0;

    public Sprite GetNodePassedBackground(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;

        EnsureNodePassedBackgroundLookup();
        _nodePassedBackgroundByName.TryGetValue(spriteName.Trim(), out Sprite sprite);
        return sprite;
    }

    private void EnsureNodePassedBackgroundLookup()
    {
        if (_nodePassedBackgroundByName != null) return;

        _nodePassedBackgroundByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        if (nodePassedBackgroundSprites == null) return;

        foreach (Sprite sprite in nodePassedBackgroundSprites)
        {
            if (sprite == null || _nodePassedBackgroundByName.ContainsKey(sprite.name)) continue;
            _nodePassedBackgroundByName.Add(sprite.name, sprite);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh Node Passed Backgrounds From Folder")]
    public void RefreshNodePassedBackgroundsFromFolder()
    {
        nodePassedBackgroundSprites.Clear();
        _nodePassedBackgroundByName = null;

        string folderPath = AssetDatabase.GetAssetPath(nodePassedBackgroundFolder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("节点已抵达背景文件夹未配置或不是有效文件夹。", this);
            EditorUtility.SetDirty(this);
            return;
        }

        HashSet<Sprite> uniqueSprites = new HashSet<Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folderPath }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite sprite)
                    uniqueSprites.Add(sprite);
            }
        }

        nodePassedBackgroundSprites.AddRange(uniqueSprites);
        nodePassedBackgroundSprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        EditorUtility.SetDirty(this);
        Debug.Log($"地图节点背景索引已刷新：{nodePassedBackgroundSprites.Count} 张 Sprite。", this);
    }
#endif
}
