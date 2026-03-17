using UnityEngine;
using System.Collections.Generic;[System.Serializable]
public class BoardRegionConfig
{
    public string regionName = "森林区域";[Tooltip("拖入你拼好的 UI 区域预制体 (挂有 MapRegionLayout)")]
    public GameObject regionPrefab; 
}[CreateAssetMenu(menuName = "Map/Board Map Config")]
public class BoardMapConfigSO : ScriptableObject
{[Tooltip("地图由哪些区域按顺序拼接而成")]
    public List<BoardRegionConfig> regions = new List<BoardRegionConfig>();
}