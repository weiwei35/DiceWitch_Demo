using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冥想界面的启迪节点显示模块。
/// 负责启迪按钮生成、位置分配、碰撞推开、出现动画、待机漂浮和已刻印连线回调。
/// </summary>
public class ForgeInspirationPanel : MonoBehaviour
{
    [Header("Options")]
    public Transform optionsContainer;
    public GameObject optionButtonPrefab;
    public RectTransform optionPlacementCenter;
    public bool positionOptionsAroundSpellIcon = true;
    public List<Vector2> optionOffsets = new List<Vector2>
    {
        new Vector2(-260f, -40f),
        new Vector2(0f, 190f),
        new Vector2(260f, -40f)
    };
    public bool showCommittedOptionLines = true;

    [Header("Layout")]
    public float generatedOptionRadiusStep = 42f;
    public float optionCollisionRadius = 96f;
    public int optionCollisionIterations = 8;
    public float optionCollisionPushStrength = 0.65f;
    public float optionLayoutMaxRadius = 380f;

    [SerializeField, HideInInspector] private float optionAppearDuration = 0.35f;
    [SerializeField, HideInInspector] private float optionFloatDistance = 8f;
    [SerializeField, HideInInspector] private float optionFloatDuration = 1.6f;

    private readonly Dictionary<ForgeInspiration, Vector2> _resolvedPositions = new Dictionary<ForgeInspiration, Vector2>();
    private readonly Dictionary<ForgeInspiration, Vector2> _previousPositions = new Dictionary<ForgeInspiration, Vector2>();

    public Transform OptionsContainer => optionsContainer;
    public RectTransform OptionPlacementCenter => optionPlacementCenter;

    /// <summary>
    /// 刷新指定骰子的所有启迪节点。
    /// </summary>
    /// <param name="dice">当前正在冥想的骰子。</param>
    /// <param name="lastCreatedInspiration">本次新生成的启迪；只有它播放出现动画。</param>
    /// <param name="isCurrentSessionInspiration">判断启迪是否属于当前可刻印会话。</param>
    /// <param name="drawCommittedLine">绘制已刻印启迪连线的回调。</param>
    /// <param name="centerOverride">启迪位置中心；为空时使用 optionPlacementCenter。</param>
    public void Refresh(
        PlayerDice dice,
        ForgeInspiration lastCreatedInspiration,
        Func<ForgeInspiration, bool> isCurrentSessionInspiration,
        Action<int, ForgeAffixSO, RectTransform> drawCommittedLine,
        RectTransform centerOverride)
    {
        CaptureCurrentPositions();
        ClearOptions();

        try
        {
            if (optionsContainer == null || optionButtonPrefab == null || dice == null) return;

            RebuildLayout(dice, centerOverride);
            RenderNodes(dice, lastCreatedInspiration, isCurrentSessionInspiration, drawCommittedLine, centerOverride);
        }
        finally
        {
            _previousPositions.Clear();
        }
    }

    /// <summary>
    /// 清空启迪按钮对象。
    /// </summary>
    public void ClearOptions()
    {
        _resolvedPositions.Clear();

        if (optionsContainer == null) return;
        foreach (Transform child in optionsContainer)
        {
            ForgeUIEffects.StopOptionTweens(child);
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// 根据启迪记录查找当前界面中的按钮对象。
    /// </summary>
    /// <param name="inspiration">要查找的启迪记录。</param>
    /// <returns>匹配的启迪按钮对象；未找到时返回 null。</returns>
    public GameObject FindOptionObject(ForgeInspiration inspiration)
    {
        if (inspiration == null || optionsContainer == null) return null;

        foreach (Transform child in optionsContainer)
        {
            ForgeOptionButton optionButton = child.GetComponent<ForgeOptionButton>();
            if (optionButton != null && optionButton.Inspiration == inspiration)
                return child.gameObject;
        }

        return null;
    }

    /// <summary>
    /// 查找指定骰子周围第一个未被启迪占用的位置索引。
    /// </summary>
    /// <param name="dice">要分配启迪位置的玩家骰子。</param>
    /// <returns>可用的启迪位置索引。</returns>
    public int FindFreeOptionIndex(PlayerDice dice)
    {
        if (dice?.forgeInspirations == null || dice.forgeInspirations.Count == 0) return 0;

        HashSet<int> occupied = new HashSet<int>();
        foreach (ForgeInspiration inspiration in dice.forgeInspirations)
        {
            if (inspiration != null && inspiration.optionIndex >= 0)
                occupied.Add(inspiration.optionIndex);
        }

        for (int i = 0; i <= occupied.Count + 8; i++)
        {
            if (!occupied.Contains(i))
                return i;
        }

        return occupied.Count;
    }

    /// <summary>
    /// 将中心法术图标的位置转换到启迪容器局部坐标。
    /// </summary>
    /// <param name="parentRect">启迪容器 RectTransform。</param>
    /// <param name="centerOverride">外部指定的中心图标。</param>
    /// <returns>中心点在启迪容器内的局部坐标。</returns>
    public Vector2 GetOptionCenterInContainer(RectTransform parentRect, RectTransform centerOverride = null)
    {
        RectTransform centerRect = centerOverride != null ? centerOverride : optionPlacementCenter;
        if (parentRect == null || centerRect == null) return Vector2.zero;

        Canvas canvas = parentRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, centerRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, camera, out Vector2 localPoint);
        return localPoint;
    }

    private void RenderNodes(
        PlayerDice dice,
        ForgeInspiration lastCreatedInspiration,
        Func<ForgeInspiration, bool> isCurrentSessionInspiration,
        Action<int, ForgeAffixSO, RectTransform> drawCommittedLine,
        RectTransform centerOverride)
    {
        if (dice.forgeInspirations == null) return;

        foreach (ForgeInspiration inspiration in dice.forgeInspirations)
        {
            if (inspiration == null || inspiration.affix == null) continue;

            GameObject buttonObject = Instantiate(optionButtonPrefab, optionsContainer);
            ForgeOptionButton optionButton = buttonObject.GetComponent<ForgeOptionButton>();
            bool isCurrentPending = isCurrentSessionInspiration != null && isCurrentSessionInspiration(inspiration);
            if (optionButton != null)
            {
                optionButton.Setup(inspiration, showAttach: isCurrentPending);
                optionButton.SetDimmed(!inspiration.isCommitted && !isCurrentPending);
                optionButton.SetCommitInteractable(isCurrentPending);
            }

            bool playAppearAnimation = inspiration == lastCreatedInspiration;
            Vector2? targetPosition = _resolvedPositions.TryGetValue(inspiration, out Vector2 resolvedPosition)
                ? resolvedPosition
                : null;
            Vector2? shiftStartPosition = !playAppearAnimation
                && _previousPositions.TryGetValue(inspiration, out Vector2 previousPosition)
                && targetPosition.HasValue
                && (previousPosition - targetPosition.Value).sqrMagnitude > 1f
                    ? previousPosition
                    : null;

            PositionOptionButton(buttonObject, inspiration.optionIndex, playAppearAnimation, targetPosition, shiftStartPosition, centerOverride);
            if (showCommittedOptionLines && inspiration.isCommitted)
                drawCommittedLine?.Invoke(inspiration.optionIndex, inspiration.affix, buttonObject.GetComponent<RectTransform>());
        }
    }

    private void CaptureCurrentPositions()
    {
        _previousPositions.Clear();
        if (optionsContainer == null) return;

        foreach (Transform child in optionsContainer)
        {
            ForgeOptionButton optionButton = child.GetComponent<ForgeOptionButton>();
            RectTransform rect = child as RectTransform;
            if (optionButton?.Inspiration == null || rect == null) continue;

            _previousPositions[optionButton.Inspiration] = rect.anchoredPosition;
        }
    }

    private void RebuildLayout(PlayerDice dice, RectTransform centerOverride)
    {
        _resolvedPositions.Clear();
        if (dice?.forgeInspirations == null || optionsContainer == null) return;

        RectTransform parentRect = optionsContainer as RectTransform;
        if (parentRect == null) return;

        Vector2 center = GetOptionCenterInContainer(parentRect, centerOverride);
        List<ForgeInspiration> inspirations = new List<ForgeInspiration>();
        List<Vector2> positions = new List<Vector2>();

        foreach (ForgeInspiration inspiration in dice.forgeInspirations)
        {
            if (inspiration == null || inspiration.affix == null) continue;

            inspirations.Add(inspiration);
            positions.Add(center + GetOptionOffset(inspiration.optionIndex));
        }

        ResolveCollisions(positions, center);

        for (int i = 0; i < inspirations.Count; i++)
            _resolvedPositions[inspirations[i]] = positions[i];
    }

    private void ResolveCollisions(List<Vector2> positions, Vector2 center)
    {
        if (positions == null || positions.Count <= 1) return;

        float minDistance = Mathf.Max(12f, optionCollisionRadius);
        float maxRadius = Mathf.Max(GetBaseOptionRadius(), optionLayoutMaxRadius);
        float pushStrength = Mathf.Clamp01(optionCollisionPushStrength);
        int iterations = Mathf.Max(1, optionCollisionIterations);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = i + 1; j < positions.Count; j++)
                {
                    Vector2 delta = positions[j] - positions[i];
                    float distance = delta.magnitude;
                    if (distance >= minDistance) continue;

                    Vector2 direction = distance > 0.01f
                        ? delta / distance
                        : GetFallbackCollisionDirection(i, j);
                    Vector2 push = direction * ((minDistance - distance) * 0.5f * pushStrength);

                    positions[i] = ClampToRadius(positions[i] - push, center, maxRadius);
                    positions[j] = ClampToRadius(positions[j] + push, center, maxRadius);
                }
            }
        }
    }

    private void PositionOptionButton(
        GameObject optionObject,
        int index,
        bool playAppearAnimation,
        Vector2? targetOverride,
        Vector2? shiftStartPosition,
        RectTransform centerOverride)
    {
        if (!positionOptionsAroundSpellIcon || optionObject == null || optionsContainer == null) return;

        RectTransform optionRect = optionObject.GetComponent<RectTransform>();
        RectTransform parentRect = optionsContainer as RectTransform;
        if (optionRect == null || parentRect == null) return;

        LayoutGroup layoutGroup = optionsContainer.GetComponent<LayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;

        Vector2 center = GetOptionCenterInContainer(parentRect, centerOverride);
        Vector2 targetPosition = targetOverride ?? center + GetOptionOffset(index);

        optionRect.anchorMin = new Vector2(0.5f, 0.5f);
        optionRect.anchorMax = new Vector2(0.5f, 0.5f);
        optionRect.pivot = new Vector2(0.5f, 0.5f);
        optionRect.anchoredPosition = targetPosition;

        if (playAppearAnimation)
            ForgeUIEffects.PlayOptionAppearAndIdle(
                optionRect,
                center,
                targetPosition,
                index,
                optionAppearDuration,
                optionFloatDistance,
                optionFloatDuration);
        else if (shiftStartPosition.HasValue)
            ForgeUIEffects.PlayOptionShiftAndIdle(
                optionRect,
                shiftStartPosition.Value,
                targetPosition,
                index,
                optionAppearDuration,
                optionFloatDistance,
                optionFloatDuration);
        else
            ForgeUIEffects.StartOptionIdleFloat(optionRect, targetPosition, index, optionFloatDistance, optionFloatDuration);
    }

    private Vector2 GetOptionOffset(int index)
    {
        if (index < 0) index = 0;
        if (optionOffsets != null && index < optionOffsets.Count)
            return optionOffsets[index];

        int configuredCount = optionOffsets != null ? optionOffsets.Count : 0;
        int generatedIndex = Mathf.Max(0, index - configuredCount);
        int pointsPerRing = 8;
        int ring = generatedIndex / pointsPerRing;
        int slot = generatedIndex % pointsPerRing;
        float radius = GetBaseOptionRadius() + Mathf.Max(0f, generatedOptionRadiusStep) * ring;
        float angle = -90f + slot * (360f / pointsPerRing);
        float radians = angle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }

    private float GetBaseOptionRadius()
    {
        float radius = 230f;
        if (optionOffsets == null) return radius;

        foreach (Vector2 offset in optionOffsets)
            radius = Mathf.Max(radius, offset.magnitude);

        return radius;
    }

    private static Vector2 GetFallbackCollisionDirection(int firstIndex, int secondIndex)
    {
        float angle = (firstIndex * 97f + secondIndex * 53f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }

    private static Vector2 ClampToRadius(Vector2 position, Vector2 center, float maxRadius)
    {
        Vector2 offset = position - center;
        if (offset.magnitude <= maxRadius) return position;
        return center + offset.normalized * maxRadius;
    }
}
