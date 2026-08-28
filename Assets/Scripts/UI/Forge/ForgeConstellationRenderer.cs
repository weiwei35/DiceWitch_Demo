using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冥想界面的星座连线渲染模块。
/// 负责 UI 备用线、世界空间发光线、节点、粒子、长按线进度和连线跟随。
/// </summary>
public class ForgeConstellationRenderer : MonoBehaviour
{
    [Header("Line")]
    public Color lineColor = new Color(1f, 1f, 1f, 1f);
    public float lineWidth = 0.014f;
    public float bendOffset = 36f;
    public float bendExaggeration = 1.8f;
    [Range(0.2f, 0.8f)] public float bendMinT = 0.35f;
    [Range(0.2f, 0.8f)] public float bendMaxT = 0.65f;
    public float particleSize = 0.018f;
    public float particleSpread = 3f;
    public float idleBendAmplitude = 4f;
    public float idleBendSpeed = 2.2f;
    public float hdrIntensity = 2.2f;
    public Sprite nodeSprite;
    public float nodeSize = 18f;

    [Header("World Rendering")]
    public bool useWorldEffect = true;
    public Camera effectCamera;
    public Transform effectRoot;
    public float effectDepth = 2.85f;

    private readonly List<ForgeConstellationEffect> _activeWorldEffects = new List<ForgeConstellationEffect>();
    private readonly List<ForgeConstellationLine> _activeUiLines = new List<ForgeConstellationLine>();
    private readonly List<WorldBinding> _worldBindings = new List<WorldBinding>();
    private bool _visible = true;

    /// <summary>
    /// 星座线实例句柄。
    /// 用于长按流程更新进度、抖动和清理，不暴露具体渲染实现。
    /// </summary>
    public class LineHandle
    {
        public ForgeConstellationLine UiLine;
        public ForgeConstellationEffect WorldLine;
        public Vector2 UiBasePosition;
    }

    /// <summary>
    /// 清理所有世界空间连线。
    /// </summary>
    public void ClearAll()
    {
        for (int i = _activeWorldEffects.Count - 1; i >= 0; i--)
        {
            ForgeConstellationEffect effect = _activeWorldEffects[i];
            if (effect != null) Destroy(effect.gameObject);
        }

        _activeWorldEffects.Clear();
        _activeUiLines.Clear();
        _worldBindings.Clear();
    }

    /// <summary>
    /// 设置所有星座连线和节点是否可见。
    /// 用于背包等前景弹窗打开时避免连线穿透到弹窗上层。
    /// </summary>
    /// <param name="visible">为 true 时显示连线；为 false 时临时隐藏。</param>
    public void SetVisible(bool visible)
    {
        _visible = visible;

        for (int i = _activeWorldEffects.Count - 1; i >= 0; i--)
        {
            ForgeConstellationEffect effect = _activeWorldEffects[i];
            if (effect == null)
            {
                _activeWorldEffects.RemoveAt(i);
                continue;
            }

            effect.gameObject.SetActive(visible);
        }

        for (int i = _activeUiLines.Count - 1; i >= 0; i--)
        {
            ForgeConstellationLine line = _activeUiLines[i];
            if (line == null)
            {
                _activeUiLines.RemoveAt(i);
                continue;
            }

            line.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 每帧刷新世界空间连线，让连线跟随 UI 图标位置。
    /// </summary>
    public void RefreshLiveSettings()
    {
        for (int i = _worldBindings.Count - 1; i >= 0; i--)
        {
            WorldBinding binding = _worldBindings[i];
            ForgeConstellationEffect effect = binding?.Effect;
            if (effect == null)
            {
                _worldBindings.RemoveAt(i);
                continue;
            }

            if (TryGetLiveScreenPoints(binding, out Vector2 startScreen, out Vector2 bendScreen, out Vector2 endScreen))
                effect.SetScreenPoints(startScreen, bendScreen, endScreen);

            effect.SetNodeSizePixels(nodeSize);
            effect.SetParticleSpread(particleSpread);
            effect.SetIdleMotion(idleBendAmplitude, idleBendSpeed, (binding.Seed & 1023) * 0.017f);
        }

        for (int i = _activeWorldEffects.Count - 1; i >= 0; i--)
        {
            if (_activeWorldEffects[i] == null)
                _activeWorldEffects.RemoveAt(i);
        }
    }

    /// <summary>
    /// 绘制已经刻印成功的启迪连线。
    /// </summary>
    /// <param name="parent">连线所在 UI 容器。</param>
    /// <param name="centerRect">法术中心图标。</param>
    /// <param name="optionRect">启迪图标。</param>
    /// <param name="affix">启迪词条。</param>
    /// <param name="optionIndex">启迪位置索引。</param>
    /// <param name="dice">当前骰子，用于生成稳定随机线形。</param>
    public void DrawCommittedLine(Transform parent, RectTransform centerRect, RectTransform optionRect, ForgeAffixSO affix, int optionIndex, PlayerDice dice)
    {
        if (!TryGetLocalEdges(parent, centerRect, optionRect, out RectTransform parentRect, out Vector2 start, out Vector2 end))
            return;

        int seed = GetSeed(affix, optionIndex, dice);
        if (TryCreateWorldLine(parentRect, centerRect, optionRect, start, end, seed, 1f, "CommittedAffixWorldLine", out _))
            return;

        CreateUiLine(parent, start, end, seed, 1f, "CommittedAffixLine");
    }

    /// <summary>
    /// 创建长按刻印过程中的临时连线。
    /// </summary>
    /// <param name="parent">连线所在 UI 容器。</param>
    /// <param name="centerRect">法术中心图标。</param>
    /// <param name="optionRect">启迪图标。</param>
    /// <param name="affix">启迪词条。</param>
    /// <param name="optionIndex">启迪位置索引。</param>
    /// <param name="dice">当前骰子，用于生成稳定随机线形。</param>
    /// <returns>创建出的连线句柄；失败时返回 null。</returns>
    public LineHandle CreateHoldLine(Transform parent, RectTransform centerRect, RectTransform optionRect, ForgeAffixSO affix, int optionIndex, PlayerDice dice)
    {
        if (!TryGetLocalEdges(parent, centerRect, optionRect, out RectTransform parentRect, out Vector2 start, out Vector2 end))
            return null;

        int seed = GetSeed(affix, Mathf.Max(0, optionIndex), dice);
        LineHandle handle = new LineHandle();
        if (TryCreateWorldLine(parentRect, centerRect, optionRect, start, end, seed, 0f, "HoldConstellationWorldLine", out ForgeConstellationEffect worldLine))
        {
            handle.WorldLine = worldLine;
            return handle;
        }

        handle.UiLine = CreateUiLine(parent, start, end, seed, 0f, "HoldConstellationLine");
        handle.UiBasePosition = handle.UiLine != null ? handle.UiLine.RectTransform.anchoredPosition : Vector2.zero;
        return handle.UiLine != null ? handle : null;
    }

    /// <summary>
    /// 设置长按线段显现进度。
    /// </summary>
    /// <param name="handle">线段句柄。</param>
    /// <param name="progress">0 到 1 的显现进度。</param>
    public void SetProgress(LineHandle handle, float progress)
    {
        if (handle == null) return;
        if (handle.WorldLine != null) handle.WorldLine.SetProgress(progress);
        if (handle.UiLine != null) handle.UiLine.SetProgress(progress);
    }

    /// <summary>
    /// 设置长按线段抖动偏移。
    /// </summary>
    /// <param name="handle">线段句柄。</param>
    /// <param name="shake">抖动偏移。</param>
    public void SetShake(LineHandle handle, Vector2 shake)
    {
        if (handle == null) return;
        if (handle.WorldLine != null) handle.WorldLine.SetScreenShake(shake);
        if (handle.UiLine != null) handle.UiLine.RectTransform.anchoredPosition = handle.UiBasePosition + shake;
    }

    /// <summary>
    /// 获取 UI 备用线的 RectTransform，用于成功反馈动画。
    /// 世界空间线没有 UI RectTransform，会返回 null。
    /// </summary>
    /// <param name="handle">线段句柄。</param>
    /// <returns>UI 线段 RectTransform 或 null。</returns>
    public RectTransform GetUiRect(LineHandle handle)
    {
        return handle?.UiLine != null ? handle.UiLine.RectTransform : null;
    }

    /// <summary>
    /// 销毁指定连线句柄。
    /// </summary>
    /// <param name="handle">要销毁的连线句柄。</param>
    public void DestroyLine(LineHandle handle)
    {
        if (handle == null) return;

        if (handle.WorldLine != null)
        {
            _activeWorldEffects.Remove(handle.WorldLine);
            RemoveBinding(handle.WorldLine);
            Destroy(handle.WorldLine.gameObject);
        }

        if (handle.UiLine != null)
        {
            _activeUiLines.Remove(handle.UiLine);
            Destroy(handle.UiLine.gameObject);
        }

        handle.WorldLine = null;
        handle.UiLine = null;
    }

    private bool TryGetLocalEdges(
        Transform parent,
        RectTransform centerRect,
        RectTransform optionRect,
        out RectTransform parentRect,
        out Vector2 centerEdge,
        out Vector2 optionEdge)
    {
        parentRect = parent as RectTransform;
        centerEdge = Vector2.zero;
        optionEdge = Vector2.zero;
        if (parentRect == null || centerRect == null || optionRect == null) return false;
        if (!TryGetRectCenterInContainer(centerRect, parentRect, out Vector2 center)) return false;
        if (!TryGetRectCenterInContainer(optionRect, parentRect, out Vector2 option)) return false;

        Vector2 centerSize = centerRect.rect.size;
        if (centerSize.x < 20f && centerSize.y < 20f) centerSize = new Vector2(64f, 64f);
        Vector2 optionSize = optionRect.rect.size;
        if (optionSize.x < 20f && optionSize.y < 20f) optionSize = new Vector2(64f, 64f);

        centerEdge = GetIconEdgePoint(center, centerSize, option);
        optionEdge = GetIconEdgePoint(option, optionSize, center);
        return (optionEdge - centerEdge).sqrMagnitude > 0.01f;
    }

    private ForgeConstellationLine CreateUiLine(Transform parent, Vector2 start, Vector2 end, int seed, float progress, string objectName)
    {
        ForgeConstellationLine line = ForgeConstellationLine.Create(
            parent,
            start,
            end,
            seed,
            lineColor,
            GetFallbackLineThickness(),
            nodeSize,
            bendOffset * bendExaggeration,
            bendMinT,
            bendMaxT,
            objectName);
        if (line != null)
        {
            line.SetProgress(progress);
            line.gameObject.SetActive(_visible);
            _activeUiLines.Add(line);
        }
        return line;
    }

    private bool TryCreateWorldLine(
        RectTransform parentRect,
        RectTransform centerRect,
        RectTransform optionRect,
        Vector2 start,
        Vector2 end,
        int seed,
        float progress,
        string objectName,
        out ForgeConstellationEffect effect)
    {
        effect = null;
        if (!useWorldEffect || parentRect == null) return false;

        Camera camera = GetEffectCamera();
        if (camera == null) return false;
        Vector2 bend = GetBendPoint(start, end, seed);
        if (!TryLocalPointToScreenPoint(parentRect, start, out Vector2 startScreen)) return false;
        if (!TryLocalPointToScreenPoint(parentRect, bend, out Vector2 bendScreen)) return false;
        if (!TryLocalPointToScreenPoint(parentRect, end, out Vector2 endScreen)) return false;

        effect = ForgeConstellationEffect.Create(
            camera,
            effectRoot,
            startScreen,
            bendScreen,
            endScreen,
            seed,
            lineColor,
            lineWidth,
            particleSize,
            particleSpread,
            hdrIntensity,
            nodeSprite,
            nodeSize,
            effectDepth,
            objectName);
        if (effect == null) return false;

        effect.SetProgress(progress);
        effect.SetIdleMotion(idleBendAmplitude, idleBendSpeed, (seed & 1023) * 0.017f);
        effect.gameObject.SetActive(_visible);
        _activeWorldEffects.Add(effect);
        _worldBindings.Add(new WorldBinding
        {
            Effect = effect,
            ParentRect = parentRect,
            CenterRect = centerRect,
            OptionRect = optionRect,
            Seed = seed
        });
        return true;
    }

    private bool TryGetLiveScreenPoints(WorldBinding binding, out Vector2 startScreen, out Vector2 bendScreen, out Vector2 endScreen)
    {
        startScreen = Vector2.zero;
        bendScreen = Vector2.zero;
        endScreen = Vector2.zero;
        if (binding == null || binding.ParentRect == null || binding.CenterRect == null || binding.OptionRect == null)
            return false;

        if (!TryGetLocalEdges(
                binding.ParentRect,
                binding.CenterRect,
                binding.OptionRect,
                out _,
                out Vector2 start,
                out Vector2 end))
        {
            return false;
        }

        Vector2 bend = GetBendPoint(start, end, binding.Seed);
        return TryLocalPointToScreenPoint(binding.ParentRect, start, out startScreen)
            && TryLocalPointToScreenPoint(binding.ParentRect, bend, out bendScreen)
            && TryLocalPointToScreenPoint(binding.ParentRect, end, out endScreen);
    }

    private float GetFallbackLineThickness()
    {
        return Mathf.Max(2f, lineWidth * 220f);
    }

    private Vector2 GetBendPoint(Vector2 start, Vector2 end, int seed)
    {
        Vector2 delta = end - start;
        if (delta.sqrMagnitude <= 0.01f) return start;

        Vector2 dir = delta.normalized;
        Vector2 perpendicular = new Vector2(-dir.y, dir.x);

        float minT = Mathf.Clamp01(Mathf.Min(bendMinT, bendMaxT));
        float maxT = Mathf.Clamp01(Mathf.Max(bendMinT, bendMaxT));
        if (Mathf.Approximately(minT, maxT)) maxT = Mathf.Clamp01(minT + 0.01f);

        System.Random random = new System.Random(seed & int.MaxValue);
        float t = Mathf.Lerp(minT, maxT, (float)random.NextDouble());
        float sign = random.Next(0, 2) == 0 ? -1f : 1f;
        float offsetScale = Mathf.Lerp(0.35f, 1f, (float)random.NextDouble());
        return Vector2.Lerp(start, end, t) + perpendicular * bendOffset * bendExaggeration * offsetScale * sign;
    }

    private Camera GetEffectCamera()
    {
        if (effectCamera != null) return effectCamera;
        return Camera.main;
    }

    private static bool TryLocalPointToScreenPoint(RectTransform parentRect, Vector2 localPoint, out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        if (parentRect == null) return false;

        Canvas canvas = parentRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3 worldPoint = parentRect.TransformPoint(localPoint);
        screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
        return true;
    }

    private static bool TryGetRectCenterInContainer(RectTransform source, RectTransform container, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (source == null || container == null) return false;

        Canvas canvas = container.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, source.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPoint, camera, out localPoint);
    }

    private static Vector2 GetIconEdgePoint(Vector2 center, Vector2 size, Vector2 target)
    {
        Vector2 dir = (target - center).normalized;
        if (dir == Vector2.zero) return center;

        float radius = Mathf.Max(1f, Mathf.Min(size.x, size.y) * 0.5f + 3f);
        return center + dir * radius;
    }

    private static int GetSeed(ForgeAffixSO affix, int index, PlayerDice dice)
    {
        unchecked
        {
            int seed = 17;
            seed = seed * 31 + index;
            seed = seed * 31 + (affix != null ? affix.GetInstanceID() : 0);
            seed = seed * 31 + GetDiceSeed(dice);
            return seed;
        }
    }

    private static int GetDiceSeed(PlayerDice dice)
    {
        if (dice == null) return 0;
        if (!string.IsNullOrEmpty(dice.uid)) return dice.uid.GetHashCode();
        return !string.IsNullOrEmpty(dice.diceName) ? dice.diceName.GetHashCode() : 0;
    }

    private void RemoveBinding(ForgeConstellationEffect effect)
    {
        for (int i = _worldBindings.Count - 1; i >= 0; i--)
        {
            if (_worldBindings[i]?.Effect == effect)
                _worldBindings.RemoveAt(i);
        }
    }

    private class WorldBinding
    {
        public ForgeConstellationEffect Effect;
        public RectTransform ParentRect;
        public RectTransform CenterRect;
        public RectTransform OptionRect;
        public int Seed;
    }
}
