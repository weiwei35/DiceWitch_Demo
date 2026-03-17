using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapViewController : MonoBehaviour
{
    [Header("References")]
    public Transform contentParent; 
    // 【已删除】不再需要 nodePrefab，因为 MapNodeAnchor 本身就是格子UI

    // 缓存所有格子的 UI 坐标，供角色移动使用
    public Dictionary<int, RectTransform> nodeUIRects = new Dictionary<int, RectTransform>();
    // 【新增】缓存所有的 Anchor 引用，用来刷新状态颜色
    public Dictionary<int, MapNodeAnchor> nodeAnchors = new Dictionary<int, MapNodeAnchor>();

    void Start()
    {
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapGenerated += DrawMap;
            if (MapManager.Instance.boardNodes != null && MapManager.Instance.boardNodes.Count > 0)
            {
                DrawMap();
            }
        }
    }

    void OnDestroy()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.OnMapGenerated -= DrawMap;
    }

    public void DrawMap()
    {
        // 1. 清理旧内容
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        
        // 【关键】每次重画必须清空旧的坐标记录！
        nodeUIRects.Clear(); 
        nodeAnchors.Clear(); 

        var nodes = MapManager.Instance.boardNodes;
        if (nodes == null || nodes.Count == 0) return;

        int globalNodeIndex = 0;
        float totalHeight = 0;

        // 2. 绘制区域与节点
        foreach (var region in MapManager.Instance.boardConfig.regions)
        {
            if (region.regionPrefab == null) continue;

            // 实例化整个 Region 预制体 (连同里面的所有 Room 和 Anchor 一起生成了)
            GameObject regionObj = Instantiate(region.regionPrefab, contentParent);
            RectTransform regionRect = regionObj.GetComponent<RectTransform>();
            regionRect.localScale = Vector3.one;
            regionRect.localRotation = Quaternion.identity;
            
            totalHeight += regionRect.sizeDelta.y;

            // 获取克隆体身上的 Layout，这样我们拿到的节点都是 Scene 里真正实例化的对象
            MapRegionLayout layout = regionObj.GetComponent<MapRegionLayout>();
            if (layout == null) continue;

            for (int i = 0; i < layout.orderedRooms.Count; i++)
            {
                var room = layout.orderedRooms[i];
                if (room == null) continue;

                for (int j = 0; j < room.roomNodes.Count; j++)
                {
                    if (globalNodeIndex >= nodes.Count) break;

                    // 直接拿到场景里刚刚实例化出来的 Anchor 脚本
                    MapNodeAnchor anchor = room.roomNodes[j];
                    RectTransform anchorRect = anchor.GetComponent<RectTransform>();

                    int nodeIndex = nodes[globalNodeIndex].index;
                    
                    // ==========================================
                    // 【核心修改】直接记录 Anchor 自身的位置和脚本
                    // ==========================================
                    nodeUIRects[nodeIndex] = anchorRect;
                    nodeAnchors[nodeIndex] = anchor;

                    globalNodeIndex++;
                }
            }
        }

        // 3. 调整高度
        RectTransform contentRect = contentParent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, totalHeight);

        Canvas.ForceUpdateCanvases();
        ScrollRect scrollRect = contentParent.parent.parent.GetComponent<ScrollRect>();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f; 

        // 4. 画完地图后，初始化所有格子的进度颜色
        UpdateNodeStates(MapManager.Instance.currentPlayerNodeIndex);

        // 5. 确保地图画完、字典存满后，再主动召唤棋子！
        MapInteractionManager interactionMgr = FindObjectOfType<MapInteractionManager>();
        if (interactionMgr != null)
        {
            interactionMgr.InitPawnPosition();
        }
    }

    /// <summary>
    /// 【新增】根据当前玩家索引，刷新所有格子的进度颜色
    /// </summary>
    public void UpdateNodeStates(int currentIndex)
    {
        foreach (var kvp in nodeAnchors)
        {
            int nodeIndex = kvp.Key;
            MapNodeAnchor anchor = kvp.Value;
            
            if (nodeIndex < currentIndex) 
                anchor.SetState(MapNodeAnchor.NodeState.Passed);
            else if (nodeIndex == currentIndex) 
                anchor.SetState(MapNodeAnchor.NodeState.Current);
            else 
                anchor.SetState(MapNodeAnchor.NodeState.Future);
        }
    }
}