using UnityEngine;
using System.Collections.Generic;

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

    [Header("Regions")]
    [Tooltip("地图由哪些章节按顺序拼接而成")]
    public List<BoardRegionConfig> regions = new List<BoardRegionConfig>();
}
