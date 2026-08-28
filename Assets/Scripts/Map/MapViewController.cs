using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapViewController : MonoBehaviour
{
    public static MapViewController Instance;

[Header("References")]
    public Transform contentParent; 
    public Dictionary<int, RectTransform> nodeUIRects = new Dictionary<int, RectTransform>();
    public Dictionary<int, MapNodeAnchor> nodeAnchors = new Dictionary<int, MapNodeAnchor>();

    [Header("Passed Grid Reveal")]
    [Min(0f)] public float passedGridRevealRadius = 180f;
    [Min(0f)] public float passedGridRevealFeather = 18f;

    [Header("Passed Route Reveal")]
    [Min(0f)] public float passedRouteRevealFeather = 8f;

    [Header("Camera Follow")]
    public Transform targetPawn;           
    public float followSmoothTime = 0.2f;  
    
    // ==========================================
    // 【新增】自动跟随的状态开关
    // ==========================================
    public bool isAutoFollowing = true;    

    private float _scrollVelocity = 0f;
    private ScrollRect _scrollRect;
    private readonly List<MapGridRevealLayer> _passedGridRevealLayers = new List<MapGridRevealLayer>();
    private readonly List<MapGridRevealLayer> _passedRouteRevealLayers = new List<MapGridRevealLayer>();
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
        _passedGridRevealLayers.Clear();
        _passedRouteRevealLayers.Clear();

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
                    _passedGridRevealLayers.Add(bgLayout.passedGridRevealLayer);
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

            if (layout.passedRouteRevealLayer != null)
            {
                layout.passedRouteRevealLayer.Initialize();
                _passedRouteRevealLayers.Add(layout.passedRouteRevealLayer);
            }

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
                    Sprite passedBackgroundSprite = MapManager.Instance.boardConfig != null
                        ? MapManager.Instance.boardConfig.GetNodePassedBackground(anchor.passedBackgroundSpriteName)
                        : null;
                    anchor.SetPresentationContext(
                        nodeData.roomDataRef,
                        MapManager.Instance.MapPresentationCatalog,
                        passedBackgroundSprite);
                    nodeUIRects[nodeIndex] = anchorRect;
                    nodeAnchors[nodeIndex] = anchor;

                    globalNodeIndex++;
                }
            }
        }

        RectTransform contentRect = contentParent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(totalWidth, maxHeight);

        Canvas.ForceUpdateCanvases();

        // 背景分身在下、包含手绘路线贴图的节点分身在上。
        foreach (var bg in bgLayers) bg.SetAsLastSibling();
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
        UpdatePassedRouteReveal();

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

    public void UpdateNodeStates(int currentIndex)
    {
        MapManager.Instance.MarkNodeVisited(currentIndex);

        foreach (var kvp in nodeAnchors)
        {
            int nodeIndex = kvp.Key;
            MapNodeAnchor anchor = kvp.Value;
            BoardNode dataNode = MapManager.Instance.boardNodes[nodeIndex];

            if (dataNode.isInvalidated) anchor.SetState(MapNodeAnchor.NodeState.Disabled);
            else if (nodeIndex == currentIndex) anchor.SetState(MapNodeAnchor.NodeState.Current);
            else if (MapManager.Instance.IsNodeVisited(nodeIndex)) anchor.SetState(MapNodeAnchor.NodeState.Passed);
            else anchor.SetState(MapNodeAnchor.NodeState.Future);
        }

        UpdatePassedGridReveal();
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

    private void UpdatePassedGridReveal()
    {
        _revealedNodeIndices.Clear();

        foreach (int nodeIndex in MapManager.Instance.VisitedNodeIndices)
        {
            if (nodeIndex < 0 || nodeIndex >= MapManager.Instance.boardNodes.Count) continue;
            BoardNode node = MapManager.Instance.boardNodes[nodeIndex];
            if (!node.isInvalidated)
                _revealedNodeIndices.Add(nodeIndex);
        }

        float feather = Mathf.Min(passedGridRevealFeather, passedGridRevealRadius);
        foreach (MapGridRevealLayer revealLayer in _passedGridRevealLayers)
        {
            if (revealLayer != null)
                revealLayer.ApplyReveal(nodeUIRects, _revealedNodeIndices, passedGridRevealRadius, feather);
        }
    }

    private void UpdatePassedRouteReveal()
    {
        Transform revealBoundary = targetPawn;
        if (revealBoundary == null
            && MapManager.Instance != null
            && nodeUIRects.TryGetValue(MapManager.Instance.currentPlayerNodeIndex, out RectTransform currentNodeRect))
        {
            revealBoundary = currentNodeRect;
        }

        if (revealBoundary == null) return;

        foreach (MapGridRevealLayer revealLayer in _passedRouteRevealLayers)
        {
            if (revealLayer != null)
                revealLayer.ApplyRevealLeftOf(revealBoundary, passedRouteRevealFeather);
        }
    }
}
