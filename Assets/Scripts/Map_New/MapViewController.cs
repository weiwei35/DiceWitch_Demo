using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems; // 【新增】事件系统命名空间

public class MapViewController : MonoBehaviour
{
    public static MapViewController Instance;

[Header("References")]
    public Transform contentParent; 
    public Dictionary<int, RectTransform> nodeUIRects = new Dictionary<int, RectTransform>();
    public Dictionary<int, MapNodeAnchor> nodeAnchors = new Dictionary<int, MapNodeAnchor>();

    [Header("Route Paths")]
    public bool showRouteLines = true;
    public Sprite routeDotSprite;
    public Vector2 routeDotSize = new Vector2(18f, 18f);
    public float routeDotSpacing = 28f;
    public float routeCurveOffset = 90f;
    public Color routeDotColor = Color.white;

    [Header("Passed Grid Reveal")]
    [Min(0f)] public float passedGridRevealRadius = 180f;
    [Min(0f)] public float passedGridRevealFeather = 18f;

    [Header("Camera Follow")]
    public Transform targetPawn;           
    public float followSmoothTime = 0.2f;  
    
    // ==========================================
    // 【新增】自动跟随的状态开关
    // ==========================================
    public bool isAutoFollowing = true;    

    private float _scrollVelocity = 0f;
    private ScrollRect _scrollRect;
    private readonly List<MapGridRevealLayer> _gridRevealLayers = new List<MapGridRevealLayer>();
    private readonly HashSet<int> _revealedNodeIndices = new HashSet<int>();

    void Awake() { Instance = this; }

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
        _gridRevealLayers.Clear();

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
            
            MapRegionLayout bgLayout = bgObj.GetComponent<MapRegionLayout>();
            if (bgLayout != null)
            {
                KeepOnlyBackgroundLayers(bgLayout);
                if (bgLayout.passedGridRevealLayer != null)
                {
                    bgLayout.passedGridRevealLayer.Initialize();
                    _gridRevealLayers.Add(bgLayout.passedGridRevealLayer);
                }
                Destroy(bgLayout);
            }

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

            RemoveBackgroundLayers(layout);

            for (int i = 0; i < layout.orderedRooms.Count; i++)
            {
                var room = layout.orderedRooms[i];
                if (room == null) continue;

                for (int j = 0; j < room.roomNodes.Count; j++)
                {
                    if (globalNodeIndex >= nodes.Count) break;

                    MapNodeAnchor anchor = room.roomNodes[j];
                    RectTransform anchorRect = anchor.GetComponent<RectTransform>();

                    BoardNode nodeData = nodes[globalNodeIndex];
                    int nodeIndex = nodeData.index;
                    anchor.SetPresentationContext(nodeData.roomDataRef, MapManager.Instance.MapPresentationCatalog);
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
        if (showRouteLines)
        {
            linesContainer = new GameObject("RoutePathsContainer");
            linesContainer.transform.SetParent(contentParent, false);

            DrawRouteLines(linesContainer.transform);
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

        MapInteractionManager interactionMgr = MapInteractionManager.Instance;
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

    private void DrawRouteLines(Transform parent)
    {
        if (MapManager.Instance == null || MapManager.Instance.boardRooms == null) return;

        foreach (BoardRoom room in MapManager.Instance.boardRooms)
        {
            if (room == null) continue;
            if (room.nextRoomIds == null || room.nextRoomIds.Count == 0) continue;

            for (int i = 0; i < room.nextRoomIds.Count; i++)
            {
                BoardRoom nextRoom = MapManager.Instance.GetRoom(room.nextRoomIds[i]);
                if (nextRoom == null) continue;

                if (!TryGetRoomBoundsLocal(room, parent, out Rect fromBounds)) continue;
                if (!TryGetRoomBoundsLocal(nextRoom, parent, out Rect toBounds)) continue;

                float edgePadding = Mathf.Max(routeDotSize.x, routeDotSize.y) * 0.5f;
                Vector2 from = GetBoundsEdgePoint(fromBounds, toBounds.center, edgePadding);
                Vector2 to = GetBoundsEdgePoint(toBounds, fromBounds.center, edgePadding);

                MapRoutePathRenderer.DrawDottedCurve(
                    from,
                    to,
                    parent,
                    routeDotSprite,
                    routeDotSize,
                    routeDotSpacing,
                    routeCurveOffset,
                    routeDotColor,
                    i,
                    room.nextRoomIds.Count);
            }
        }
    }

    private bool TryGetRoomBoundsLocal(BoardRoom room, Transform parent, out Rect bounds)
    {
        bounds = default;
        bool hasNode = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        for (int nodeIndex = room.startNodeIndex; nodeIndex <= room.endNodeIndex; nodeIndex++)
        {
            if (!nodeUIRects.TryGetValue(nodeIndex, out RectTransform rect)) continue;

            Vector2 localPosition = parent.InverseTransformPoint(rect.position);
            Vector2 halfSize = rect.rect.size * 0.5f;
            Vector2 nodeMin = localPosition - halfSize;
            Vector2 nodeMax = localPosition + halfSize;

            if (!hasNode)
            {
                min = nodeMin;
                max = nodeMax;
                hasNode = true;
            }
            else
            {
                min = Vector2.Min(min, nodeMin);
                max = Vector2.Max(max, nodeMax);
            }
        }

        if (!hasNode) return false;

        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static Vector2 GetBoundsEdgePoint(Rect bounds, Vector2 target, float padding)
    {
        Vector2 center = bounds.center;
        Vector2 direction = target - center;
        if (direction.sqrMagnitude <= 0.0001f) return center;

        direction.Normalize();
        Vector2 halfSize = bounds.size * 0.5f + Vector2.one * Mathf.Max(0f, padding);
        float scaleX = Mathf.Abs(direction.x) > 0.0001f ? halfSize.x / Mathf.Abs(direction.x) : float.MaxValue;
        float scaleY = Mathf.Abs(direction.y) > 0.0001f ? halfSize.y / Mathf.Abs(direction.y) : float.MaxValue;
        return center + direction * Mathf.Min(scaleX, scaleY);
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

        UpdatePassedGridReveal(currentIndex);
    }

    private void KeepOnlyBackgroundLayers(MapRegionLayout layout)
    {
        Transform baseGrid = layout.baseGridRoot != null ? layout.baseGridRoot.transform : null;
        Transform revealGrid = layout.passedGridRevealLayer != null ? layout.passedGridRevealLayer.transform : null;

        foreach (Transform child in layout.transform)
        {
            if (child != baseGrid && child != revealGrid)
                Destroy(child.gameObject);
        }
    }

    private void RemoveBackgroundLayers(MapRegionLayout layout)
    {
        if (layout.baseGridRoot != null)
            Destroy(layout.baseGridRoot);
        if (layout.passedGridRevealLayer != null)
            Destroy(layout.passedGridRevealLayer.gameObject);
    }

    private void UpdatePassedGridReveal(int currentIndex)
    {
        _revealedNodeIndices.Clear();

        for (int nodeIndex = 0; nodeIndex < MapManager.Instance.boardNodes.Count; nodeIndex++)
        {
            BoardNode node = MapManager.Instance.boardNodes[nodeIndex];
            if (nodeIndex < currentIndex || node.isInvalidated)
                _revealedNodeIndices.Add(nodeIndex);
        }

        float feather = Mathf.Min(passedGridRevealFeather, passedGridRevealRadius);
        foreach (MapGridRevealLayer revealLayer in _gridRevealLayers)
        {
            if (revealLayer != null)
                revealLayer.ApplyReveal(nodeUIRects, _revealedNodeIndices, passedGridRevealRadius, feather);
        }
    }
}
