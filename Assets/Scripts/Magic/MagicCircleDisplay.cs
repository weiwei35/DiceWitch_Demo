using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class MagicCircleDisplay : MonoBehaviour
{
    public static MagicCircleDisplay Instance;

    public GameObject slotPrefab;   // 拖入 MagicSlotUI 的预制体
    public Transform container;     // 生成在哪个父物体下

    [Header("Node Layout")]
    [Min(0f)]
    public float radius = 145f;     // 所有节点围绕原点显示的半径

    [Tooltip("整体平移 7 个节点，不会移动魔法阵背景。")]
    public Vector2 centerOffset;

    [Tooltip("X/Y 半径倍率。保持 (1,1) 为正圆，可用于适配略扁或略高的美术底图。")]
    public Vector2 radiusScale = Vector2.one;

    [Tooltip("整体旋转角度。0 度时 Element 0 位于正上方。")]
    public float angleOffset;

    [Tooltip("单节点微调：Element 0 从顶部开始，之后按逆时针排列。只需修改未对齐的节点。")]
    public Vector2[] nodeOffsets = new Vector2[7];

    [Header("Hover Hand")]
    public RectTransform hoverHand;
    public Vector2 handHoverOffset = new Vector2(185f, -190f);
    [Min(0f)] public float handMoveDuration = 0.18f;

    private List<MagicSlotUI> _spawnedSlots = new List<MagicSlotUI>();
    private Vector2 _handDefaultPosition;

    void Awake()
    {
        Instance = this;
        if (hoverHand != null)
            _handDefaultPosition = hoverHand.anchoredPosition;
    }

    void Start()
    {
        GenerateCircle();
    }

    void GenerateCircle()
    {
        // 1. 获取数据
        var dataSlots = MagicCircleManager.Instance.magicSlots;
        int count = dataSlots.Count;

        // 2. 环形生成
        for (int i = 0; i < count; i++)
        {
            Vector2 targetPosition = CalculateNodePosition(i, count);

            // 实例化
            GameObject slotObj = Instantiate(slotPrefab, container);

            // 初始化 UI
            MagicSlotUI uiScript = slotObj.GetComponent<MagicSlotUI>();
            uiScript.Setup(dataSlots[i]);
            uiScript.SetRadialLayout(targetPosition);

            _spawnedSlots.Add(uiScript);
        }

        BringHandToFront();
    }

    private Vector2 CalculateNodePosition(int index, int count)
    {
        float angle = index * (360f / count) + 90f + angleOffset;
        float radian = angle * Mathf.Deg2Rad;
        Vector2 radialPosition = new Vector2(
            Mathf.Cos(radian) * radius * radiusScale.x,
            Mathf.Sin(radian) * radius * radiusScale.y);
        Vector2 nodeOffset = nodeOffsets != null && index < nodeOffsets.Length
            ? nodeOffsets[index]
            : Vector2.zero;
        return centerOffset + radialPosition + nodeOffset;
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0f, radius);

        if (!Application.isPlaying || _spawnedSlots == null || _spawnedSlots.Count == 0)
            return;

        for (int i = 0; i < _spawnedSlots.Count; i++)
        {
            if (_spawnedSlots[i] != null)
                _spawnedSlots[i].SetRadialLayout(CalculateNodePosition(i, _spawnedSlots.Count));
        }
    }

    // 刷新显示 (比如刚注入了属性后调用)
    public void RefreshAll()
    {
        var dataSlots = MagicCircleManager.Instance.magicSlots;
        for (int i = 0; i < _spawnedSlots.Count; i++)
        {
            _spawnedSlots[i].Setup(dataSlots[i]);
        }
    }

    public void SetSelectionMode(bool isActive)
    {
        if (_spawnedSlots == null) return;

        foreach (var ui in _spawnedSlots)
        {
            // 让每个 UI 自己去处理动画
            ui.SetSelectionState(isActive);
        }
    }

    public void MoveHandToSlot(Vector2 slotCenter)
    {
        if (hoverHand == null) return;

        BringHandToFront();
        hoverHand.DOKill();
        hoverHand.DOAnchorPos(slotCenter + handHoverOffset, handMoveDuration)
            .SetEase(Ease.OutQuad);
    }

    private void BringHandToFront()
    {
        if (hoverHand != null)
            hoverHand.SetAsLastSibling();
    }

    public void ReturnHandToDefault()
    {
        if (hoverHand == null) return;

        hoverHand.DOKill();
        hoverHand.DOAnchorPos(_handDefaultPosition, handMoveDuration)
            .SetEase(Ease.OutQuad);
    }

    private void OnDisable()
    {
        if (hoverHand == null) return;

        hoverHand.DOKill();
        hoverHand.anchoredPosition = _handDefaultPosition;
    }
}
