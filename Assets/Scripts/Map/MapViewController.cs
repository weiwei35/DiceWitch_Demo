using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapViewController : MonoBehaviour
{
    [Header("References")]
    public Transform contentParent; // ScrollRect 的 Content 对象
    public GameObject nodePrefab;   // 上面做的 Button Prefab
    public GameObject linePrefab;   // 一个简单的 Image Prefab (下面会教你做)

    [Header("Layout Settings")]
    public float xSpacing = 200f; // 节点的水平间距
    public float ySpacing = 150f; // 节点的垂直间距 (层与层之间)
    public float lineThickness = 5f; // 线条粗细

    // 缓存生成的 GameObject，方便刷新状态
    private Dictionary<Vector2Int, MapNodeButton> _spawnedNodes = new Dictionary<Vector2Int, MapNodeButton>();

    void Start()
    {
        // 如果 MapManager 已经生成好数据了，我们就画出来
        if (MapManager.Instance != null && MapManager.Instance.mapNodes.Count > 0)
        {
            DrawMap();
        }
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapGenerated += DrawMap;
            MapManager.Instance.OnRoomLoaded += EnterRoom;
        }
    }


    void OnDestroy()
    {
        // 养成好习惯：销毁时取消订阅，防止报错
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapGenerated -= DrawMap;
            MapManager.Instance.OnRoomLoaded -= EnterRoom;
        }
    }
    private void EnterRoom()
    {
        gameObject.SetActive(false);
    }
    public void DrawMap()
    {
        // 1. 清理
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        _spawnedNodes.Clear();
    
        var nodes = MapManager.Instance.mapNodes;
        var config = MapManager.Instance.mapConfig; // 获取配置引用
        if (nodes == null || nodes.Count == 0) return;
    
        // --- 【新增】计算全局最大高度 (用于居中) ---
        // 先取默认规则的最大值
        int maxPossibleNodes = config.defaultRule.maxNodes;
        // 再遍历特殊层，看有没有哪一层允许生成更多的节点，取最大值
        foreach (var entry in config.specificLayers)
        {
            if (entry.rule.maxNodes > maxPossibleNodes)
            {
                maxPossibleNodes = entry.rule.maxNodes;
            }
        }
        // ------------------------------------------

        float leftPadding = 200f; 
    
        foreach (var node in nodes)
        {
            GameObject obj = Instantiate(nodePrefab, contentParent);
            MapNodeButton nodeScript = obj.GetComponent<MapNodeButton>();
    
            // X轴：往右排
            float posX = (node.gridPosition.x * xSpacing) + leftPadding;
    
            // Y轴：根据刚才算出的 maxPossibleNodes 进行居中
            // 计算居中偏移量
            float centerOffset = (maxPossibleNodes - 1) * ySpacing / 2f;
            float posY = (node.gridPosition.y * ySpacing) - centerOffset;
    
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(posX, posY);
    
            nodeScript.Setup(node);
            _spawnedNodes.Add(node.gridPosition, nodeScript);
        }
    
        // 2. 画线 (代码逻辑不用变，CreateLineConnection 会自动处理两点间的坐标)
        foreach (var node in nodes)
        {
            if (!_spawnedNodes.ContainsKey(node.gridPosition)) continue;
            RectTransform startRect = _spawnedNodes[node.gridPosition].GetComponent<RectTransform>();
    
            foreach (var targetPos in node.outgoing)
            {
                if (_spawnedNodes.ContainsKey(targetPos))
                {
                    RectTransform endRect = _spawnedNodes[targetPos].GetComponent<RectTransform>();
                    CreateLineConnection(startRect.anchoredPosition, endRect.anchoredPosition);
                }
            }
        }
    
        // --- 【改动3】计算 Content 总宽度 ---
        int totalLayers = MapManager.Instance.mapConfig.totalLayers;
        
        // 总宽度 = (层数 * 横向间距) + 左留白 + 右留白
        float requiredWidth = (totalLayers * xSpacing) + leftPadding + 300f;
    
        RectTransform contentRect = contentParent.GetComponent<RectTransform>();
        // 注意：这里改的是 x (Width)，保持 y (Height) 不变
        contentRect.sizeDelta = new Vector2(requiredWidth, contentRect.sizeDelta.y);
    
        // --- 【改动4】重置水平滚动条 ---
        ScrollRect scrollRect = contentParent.parent.parent.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.horizontalNormalizedPosition = 0f; // 0 是最左边
        }
    }

    // --- 纯数学画线法 (无需插件) ---
    private void CreateLineConnection(Vector2 startPos, Vector2 endPos)
    {
        // 实例化一个 Image
        GameObject lineObj = Instantiate(linePrefab, contentParent);
        lineObj.transform.SetAsFirstSibling(); // 放在最底层，别挡住按钮

        RectTransform rect = lineObj.GetComponent<RectTransform>();
        
        // 计算中点、长度、角度
        Vector2 direction = endPos - startPos;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 设置位置和尺寸
        rect.anchoredPosition = startPos + direction / 2f; // 放中点
        rect.sizeDelta = new Vector2(distance, lineThickness); // 宽=距离，高=粗细
        rect.localRotation = Quaternion.Euler(0, 0, angle);
    }
}