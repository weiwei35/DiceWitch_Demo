using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems; // 【新增】事件系统命名空间

public class MapViewController : MonoBehaviour
{[Header("References")]
    public Transform contentParent; 
    public Dictionary<int, RectTransform> nodeUIRects = new Dictionary<int, RectTransform>();
    public Dictionary<int, MapNodeAnchor> nodeAnchors = new Dictionary<int, MapNodeAnchor>();

    [Header("Route Lines (Temporary)")]
    public bool showRouteLines = true;       
    public GameObject linePrefab;            
    public float lineWidth = 8f;             
    public Color routeLineColor = new Color(1f, 1f, 1f, 0.5f); 

    [Header("Camera Follow")]
    public Transform targetPawn;           
    public float followSmoothTime = 0.2f;  
    
    // ==========================================
    // 【新增】自动跟随的状态开关
    // ==========================================
    public bool isAutoFollowing = true;    

    private float _scrollVelocity = 0f;    
    private ScrollRect _scrollRect;        

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
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        nodeUIRects.Clear(); 
        nodeAnchors.Clear(); 

        var nodes = MapManager.Instance.boardNodes;
        if (nodes == null || nodes.Count == 0) return;

        int globalNodeIndex = 0;
        float totalWidth = 0;
        float maxHeight = 0;

        List<Transform> bgLayers = new List<Transform>();
        List<Transform> nodeLayers = new List<Transform>();

        foreach (var region in MapManager.Instance.boardConfig.regions)
        {
            if (region.regionPrefab == null) continue;

            // 分身 A：纯背景层 
            GameObject bgObj = Instantiate(region.regionPrefab, contentParent);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.localScale = Vector3.one;
            bgRect.localRotation = Quaternion.identity;
            
            Destroy(bgObj.GetComponent<MapRegionLayout>());
            foreach (Transform child in bgObj.transform) Destroy(child.gameObject);

            // 分身 B：纯节点层 
            GameObject nodesObj = Instantiate(region.regionPrefab, contentParent);
            RectTransform nodesRect = nodesObj.GetComponent<RectTransform>();
            nodesRect.localScale = Vector3.one;
            nodesRect.localRotation = Quaternion.identity;

            Image nodesBgImg = nodesObj.GetComponent<Image>();
            if (nodesBgImg != null) nodesBgImg.enabled = false;

            float pivotOffsetX = bgRect.pivot.x * bgRect.sizeDelta.x;
            Vector2 pos = new Vector2(totalWidth + pivotOffsetX, 0);
            bgRect.anchoredPosition = pos;
            nodesRect.anchoredPosition = pos;

            totalWidth += bgRect.sizeDelta.x;
            if (bgRect.sizeDelta.y > maxHeight) maxHeight = bgRect.sizeDelta.y;

            bgLayers.Add(bgRect);
            nodeLayers.Add(nodesRect);

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

        RectTransform contentRect = contentParent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(totalWidth, maxHeight);

        Canvas.ForceUpdateCanvases();
        
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

        // 强制重排三明治层级
        foreach (var bg in bgLayers) bg.SetAsLastSibling();
        if (linesContainer != null) linesContainer.transform.SetAsLastSibling();
        foreach (var nodeLayer in nodeLayers) nodeLayer.SetAsLastSibling();

        _scrollRect = contentParent.GetComponentInParent<ScrollRect>();
        if (_scrollRect != null) 
        {
            _scrollRect.horizontalNormalizedPosition = 0f; 
            _scrollRect.verticalNormalizedPosition = 0.5f; 

            // =========================================================
            // 【新增】动态给 ScrollRect 挂载一个拖拽监听器，用来打断自动跟随
            // =========================================================
            MapDragListener dragListener = _scrollRect.gameObject.GetComponent<MapDragListener>();
            if (dragListener == null) dragListener = _scrollRect.gameObject.AddComponent<MapDragListener>();
            dragListener.mapUI = this;
        }

        UpdateNodeStates(MapManager.Instance.currentPlayerNodeIndex);

        MapInteractionManager interactionMgr = FindObjectOfType<MapInteractionManager>();
        if (interactionMgr != null) interactionMgr.InitPawnPosition();
    }

    // =========================================================
    // 【新增】供外部调用的接口：重新启动自动跟随
    // =========================================================
    public void ResumeAutoFollow()
    {
        isAutoFollowing = true;
    }

    void LateUpdate()
    {
        // 如果被玩家手动拖拽打断了，就不再执行跟随算法
        if (!isAutoFollowing || targetPawn == null || _scrollRect == null || _scrollRect.viewport == null) return;

        float viewportWidth = _scrollRect.viewport.rect.width;
        float contentWidth = contentParent.GetComponent<RectTransform>().rect.width;
        float maxScroll = contentWidth - viewportWidth;

        if (maxScroll <= 0) return; 

        Vector3 pawnLocalPos = contentParent.InverseTransformPoint(targetPawn.position);
        float distanceFromLeft = pawnLocalPos.x - contentParent.GetComponent<RectTransform>().rect.xMin;
        float targetLeftEdge = distanceFromLeft - (viewportWidth / 2f);
        
        float targetNormalized = targetLeftEdge / maxScroll;
        targetNormalized = Mathf.Clamp01(targetNormalized);

        _scrollRect.horizontalNormalizedPosition = Mathf.SmoothDamp(
            _scrollRect.horizontalNormalizedPosition, 
            targetNormalized, 
            ref _scrollVelocity, 
            followSmoothTime
        );
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