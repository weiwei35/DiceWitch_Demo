using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapViewController : MonoBehaviour
{[Header("References")]
    public Transform contentParent; 

    // 缓存所有格子的 UI 坐标，供角色移动使用
    public Dictionary<int, RectTransform> nodeUIRects = new Dictionary<int, RectTransform>();
    public Dictionary<int, MapNodeAnchor> nodeAnchors = new Dictionary<int, MapNodeAnchor>();

    [Header("Route Lines (Temporary)")]
    public bool showRouteLines = true;       
    public GameObject linePrefab;            
    public float lineWidth = 8f;             
    public Color routeLineColor = new Color(1f, 1f, 1f, 0.5f); 

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
        nodeUIRects.Clear(); 
        nodeAnchors.Clear(); 

        var nodes = MapManager.Instance.boardNodes;
        if (nodes == null || nodes.Count == 0) return;

        int globalNodeIndex = 0;
        float totalWidth = 0;
        float maxHeight = 0;

        // 【新增】用来做“三明治层级”的临时列表
        List<Transform> bgLayers = new List<Transform>();
        List<Transform> nodeLayers = new List<Transform>();

        // 2. 绘制区域与节点
        foreach (var region in MapManager.Instance.boardConfig.regions)
        {
            if (region.regionPrefab == null) continue;

            // ==========================================
            // 分身 A：纯背景层 (剥离所有房间节点)
            // ==========================================
            GameObject bgObj = Instantiate(region.regionPrefab, contentParent);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.localScale = Vector3.one;
            bgRect.localRotation = Quaternion.identity;
            
            // 删除脚本和子节点，只留一张背景图
            Destroy(bgObj.GetComponent<MapRegionLayout>());
            foreach (Transform child in bgObj.transform) Destroy(child.gameObject);

            // ==========================================
            // 分身 B：纯节点层 (隐藏背景图)
            // ==========================================
            GameObject nodesObj = Instantiate(region.regionPrefab, contentParent);
            RectTransform nodesRect = nodesObj.GetComponent<RectTransform>();
            nodesRect.localScale = Vector3.one;
            nodesRect.localRotation = Quaternion.identity;

            // 把这层本身的 Image 关掉，变成一个透明容器
            Image nodesBgImg = nodesObj.GetComponent<Image>();
            if (nodesBgImg != null) nodesBgImg.enabled = false;

            // 统一计算位置
            float pivotOffsetX = bgRect.pivot.x * bgRect.sizeDelta.x;
            Vector2 pos = new Vector2(totalWidth + pivotOffsetX, 0);
            bgRect.anchoredPosition = pos;
            nodesRect.anchoredPosition = pos;

            totalWidth += bgRect.sizeDelta.x;
            if (bgRect.sizeDelta.y > maxHeight) maxHeight = bgRect.sizeDelta.y;

            // 记录到层级列表
            bgLayers.Add(bgRect);
            nodeLayers.Add(nodesRect);

            // 只从分身 B (节点层) 读取房间数据
            MapRegionLayout layout = nodesObj.GetComponent<MapRegionLayout>();
            if (layout == null) continue;

            for (int i = 0; i < layout.orderedRooms.Count; i++)
            {
                var room = layout.orderedRooms[i];
                if (room == null) continue;

                for (int j = 0; j < room.roomNodes.Count; j++)
                {
                    if (globalNodeIndex >= nodes.Count) break;

                    MapNodeAnchor anchor = room.roomNodes[j];
                    RectTransform anchorRect = anchor.GetComponent<RectTransform>();

                    int nodeIndex = nodes[globalNodeIndex].index;
                    nodeUIRects[nodeIndex] = anchorRect;
                    nodeAnchors[nodeIndex] = anchor;

                    globalNodeIndex++;
                }
            }
        }

        // 3. 调整 Content 的最终宽高
        RectTransform contentRect = contentParent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(totalWidth, maxHeight);

        Canvas.ForceUpdateCanvases();
        
        // 4. 画节点之间的连线 (夹心层)
        GameObject linesContainer = null;
        if (showRouteLines && linePrefab != null)
        {
            linesContainer = new GameObject("RouteLinesContainer");
            linesContainer.transform.SetParent(contentParent, false);

            for (int i = 0; i < nodes.Count - 1; i++)
            {
                if (nodeUIRects.TryGetValue(nodes[i].index, out RectTransform rectA) &&
                    nodeUIRects.TryGetValue(nodes[i + 1].index, out RectTransform rectB))
                {
                    DrawLineBetweenNodes(rectA, rectB, linesContainer.transform);
                }
            }
        }

        // =========================================================
        // 【魔法时刻】强制重排渲染层级，制作三明治！
        // =========================================================
        // 第一层：把所有纯背景图垫在最底下
        foreach (var bg in bgLayers) bg.SetAsLastSibling();
        
        // 第二层：把连线层放在背景图上面
        if (linesContainer != null) linesContainer.transform.SetAsLastSibling();
        
        // 第三层：把所有的房间和节点放在最顶上！
        foreach (var nodeLayer in nodeLayers) nodeLayer.SetAsLastSibling();
        // =========================================================

        // 5. 滚动条复位
        ScrollRect scrollRect = contentParent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null) 
        {
            scrollRect.horizontalNormalizedPosition = 0f; 
            scrollRect.verticalNormalizedPosition = 0.5f; 
        }

        // 6. 刷新状态
        UpdateNodeStates(MapManager.Instance.currentPlayerNodeIndex);

        // 7. 初始化棋子 (棋子也会调用 SetAsLastSibling，所以它在第四层，最最顶上)
        MapInteractionManager interactionMgr = FindObjectOfType<MapInteractionManager>();
        if (interactionMgr != null) interactionMgr.InitPawnPosition();
    }

    private void DrawLineBetweenNodes(RectTransform rectA, RectTransform rectB, Transform parent)
    {
        GameObject lineObj = Instantiate(linePrefab, parent);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();

        Vector3 localPosA = parent.InverseTransformPoint(rectA.position);
        Vector3 localPosB = parent.InverseTransformPoint(rectB.position);

        Vector3 dir = localPosB - localPosA;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRect.localPosition = localPosA;
        lineRect.sizeDelta = new Vector2(distance, lineWidth);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);

        Image img = lineObj.GetComponent<Image>();
        if (img != null) img.color = routeLineColor;
    }

    public void UpdateNodeStates(int currentIndex)
    {
        foreach (var kvp in nodeAnchors)
        {
            int nodeIndex = kvp.Key;
            MapNodeAnchor anchor = kvp.Value;
            BoardNode dataNode = MapManager.Instance.boardNodes[nodeIndex];

            if (dataNode.isInvalidated) anchor.SetState(MapNodeAnchor.NodeState.Disabled);
            else if (nodeIndex < currentIndex) anchor.SetState(MapNodeAnchor.NodeState.Passed);
            else if (nodeIndex == currentIndex) anchor.SetState(MapNodeAnchor.NodeState.Current);
            else anchor.SetState(MapNodeAnchor.NodeState.Future);
        }
    }
}