using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冥想/锻造 UI 的通用 DOTween 表现工具。
/// 只负责视觉动画，不保存材料、启迪或骰子的业务状态。
/// </summary>
public static class ForgeUIEffects
{
    /// <summary>
    /// 停止指定 RectTransform 上的 Tween，并可选择重置缩放。
    /// </summary>
    /// <param name="rect">需要停止动画的 UI 变换。</param>
    /// <param name="resetScale">为 true 时将缩放恢复为 Vector3.one。</param>
    public static void StopTransformTween(RectTransform rect, bool resetScale = false)
    {
        if (rect == null) return;
        rect.DOKill();
        if (resetScale) rect.localScale = Vector3.one;
    }

    /// <summary>
    /// 停止启迪按钮及其 CanvasGroup 上的 Tween。
    /// </summary>
    /// <param name="optionTransform">启迪按钮的 Transform。</param>
    public static void StopOptionTweens(Transform optionTransform)
    {
        if (optionTransform == null) return;

        optionTransform.DOKill();
        var rect = optionTransform as RectTransform;
        if (rect != null) rect.DOKill();
        var canvasGroup = optionTransform.GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.DOKill();
    }

    /// <summary>
    /// 播放法术图标的呼吸待机动画。
    /// </summary>
    /// <param name="image">要播放动画的 UI 图片。</param>
    /// <param name="breathScale">呼吸放大的目标缩放。</param>
    /// <param name="breathDuration">单次放大或缩回的持续时间。</param>
    public static void StartBreath(Image image, float breathScale, float breathDuration)
    {
        if (image == null || breathScale <= 1f || breathDuration <= 0f) return;

        RectTransform rect = image.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOScale(breathScale, breathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// 停止图标 Tween，并将缩放恢复为默认值。
    /// </summary>
    /// <param name="image">要停止动画的 UI 图片。</param>
    public static void StopIconTween(Image image)
    {
        if (image == null) return;
        image.rectTransform.DOKill();
        image.rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// 播放切换骰子/法术时的弹入动画。
    /// </summary>
    /// <param name="image">需要播放切换动画的图标。</param>
    /// <param name="duration">动画持续时间。</param>
    /// <param name="onComplete">动画完成后的回调。</param>
    public static void PlayIconSwitch(Image image, float duration, Action onComplete = null)
    {
        if (image == null || duration <= 0f)
        {
            onComplete?.Invoke();
            return;
        }

        RectTransform rect = image.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one * 0.75f;
        rect.DOScale(1f, duration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// 播放启迪从法术中心生成并移动到目标位置的出现动画，完成后进入漂浮待机。
    /// </summary>
    /// <param name="optionRect">启迪按钮的 RectTransform。</param>
    /// <param name="startPosition">启迪生成时的起点位置。</param>
    /// <param name="targetPosition">启迪最终停留的位置。</param>
    /// <param name="index">启迪序号，用于错开动画延迟。</param>
    /// <param name="appearDuration">出现动画持续时间。</param>
    /// <param name="floatDistance">待机漂浮高度。</param>
    /// <param name="floatDuration">待机漂浮半周期时长。</param>
    public static void PlayOptionAppearAndIdle(
        RectTransform optionRect,
        Vector2 startPosition,
        Vector2 targetPosition,
        int index,
        float appearDuration,
        float floatDistance,
        float floatDuration)
    {
        if (optionRect == null) return;

        optionRect.DOKill();
        CanvasGroup canvasGroup = optionRect.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = optionRect.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.DOKill();

        optionRect.anchoredPosition = startPosition;
        optionRect.localScale = Vector3.one * 0.8f;
        canvasGroup.alpha = 0f;

        Sequence appearSequence = DOTween.Sequence().SetTarget(optionRect);
        appearSequence.AppendInterval(index * 0.08f);
        appearSequence.Append(canvasGroup.DOFade(1f, appearDuration * 0.45f));
        appearSequence.Join(optionRect.DOAnchorPos(targetPosition, appearDuration).SetEase(Ease.OutCubic));
        appearSequence.Join(optionRect.DOScale(1f, appearDuration).SetEase(Ease.OutBack));
        appearSequence.AppendCallback(() => StartOptionIdleFloat(optionRect, targetPosition, index, floatDistance, floatDuration));
    }

    /// <summary>
    /// 让启迪按钮在固定基准位置附近上下漂浮。
    /// </summary>
    /// <param name="optionRect">启迪按钮的 RectTransform。</param>
    /// <param name="basePosition">漂浮动画的基准位置。</param>
    /// <param name="index">启迪序号，用于错开不同启迪的相位。</param>
    /// <param name="floatDistance">漂浮高度。</param>
    /// <param name="floatDuration">漂浮半周期时长。</param>
    public static void StartOptionIdleFloat(RectTransform optionRect, Vector2 basePosition, int index, float floatDistance, float floatDuration)
    {
        if (optionRect == null || floatDistance <= 0f || floatDuration <= 0f) return;

        optionRect.DOKill();
        optionRect.anchoredPosition = basePosition;
        optionRect.DOAnchorPosY(basePosition.y + floatDistance, floatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(index * 0.12f);
    }

    /// <summary>
    /// 将已经存在的启迪从旧位置平滑移动到新布局位置，然后进入漂浮待机。
    /// </summary>
    /// <param name="optionRect">启迪按钮的 RectTransform。</param>
    /// <param name="fromPosition">移动起点位置。</param>
    /// <param name="targetPosition">移动终点位置。</param>
    /// <param name="index">启迪序号，用于错开待机漂浮相位。</param>
    /// <param name="moveDuration">位置调整动画时长。</param>
    /// <param name="floatDistance">待机漂浮高度。</param>
    /// <param name="floatDuration">待机漂浮半周期时长。</param>
    public static void PlayOptionShiftAndIdle(
        RectTransform optionRect,
        Vector2 fromPosition,
        Vector2 targetPosition,
        int index,
        float moveDuration,
        float floatDistance,
        float floatDuration)
    {
        if (optionRect == null) return;

        optionRect.DOKill();
        optionRect.anchoredPosition = fromPosition;
        optionRect.DOAnchorPos(targetPosition, Mathf.Max(0.01f, moveDuration))
            .SetEase(Ease.OutCubic)
            .OnComplete(() => StartOptionIdleFloat(optionRect, targetPosition, index, floatDistance, floatDuration));
    }

    /// <summary>
    /// 长按刻印时对启迪或法术图标施加不稳定抖动。
    /// </summary>
    /// <param name="rect">需要抖动的 UI 变换。</param>
    /// <param name="basePos">抖动围绕的基准位置。</param>
    /// <param name="baseScale">抖动围绕的基准缩放。</param>
    /// <param name="positionIntensity">位置抖动强度。</param>
    /// <param name="scaleIntensity">缩放抖动强度。</param>
    public static void ApplyHoldIconShake(RectTransform rect, Vector2 basePos, Vector3 baseScale, float positionIntensity, float scaleIntensity)
    {
        if (rect == null) return;

        rect.anchoredPosition = basePos + new Vector2(
            UnityEngine.Random.Range(-positionIntensity * 0.35f, positionIntensity * 0.35f),
            UnityEngine.Random.Range(-positionIntensity * 0.35f, positionIntensity * 0.35f));

        rect.localScale = baseScale * (1f + UnityEngine.Random.Range(-scaleIntensity, scaleIntensity));
    }

    /// <summary>
    /// 长按刻印成功时播放线段、启迪和法术图标的确认反馈。
    /// </summary>
    /// <param name="lineRect">UI 线段对象，可为空。</param>
    /// <param name="optionRect">被刻印启迪的 RectTransform。</param>
    /// <param name="centerIconRect">中心法术图标的 RectTransform。</param>
    /// <returns>等待反馈动画完成的协程。</returns>
    public static IEnumerator PlayHoldSuccessFeedback(RectTransform lineRect, RectTransform optionRect, RectTransform centerIconRect)
    {
        const float settleDuration = 0.16f;

        if (lineRect != null) lineRect.DOPunchScale(Vector3.one * 0.08f, settleDuration, 1, 0.45f);
        if (optionRect != null) optionRect.DOPunchScale(Vector3.one * 0.16f, settleDuration, 1, 0.45f);
        if (centerIconRect != null) centerIconRect.DOPunchScale(Vector3.one * 0.16f, settleDuration, 1, 0.45f);

        yield return new WaitForSeconds(settleDuration);
    }

    /// <summary>
    /// 播放材料槽被清空时的收缩动画，并在收缩完成后刷新槽位显示。
    /// </summary>
    /// <param name="image">材料槽 Image。</param>
    /// <param name="delay">开始播放前的延迟。</param>
    /// <param name="onRefresh">动画结束后刷新槽位内容的回调。</param>
    public static void AnimateMaterialSlotClear(Image image, float delay, Action onRefresh)
    {
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOScale(0.78f, 0.14f)
            .SetDelay(delay)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                rect.localScale = Vector3.one;
                onRefresh?.Invoke();
            });
    }

    /// <summary>
    /// 播放材料放入或替换时的弹跳反馈。
    /// </summary>
    /// <param name="image">材料槽 Image。</param>
    /// <param name="isReplace">为 true 时播放替换动画，否则播放首次放入动画。</param>
    /// <param name="popDuration">首次放入动画时长。</param>
    /// <param name="replaceDuration">替换动画总时长。</param>
    public static void PlayMaterialSlotChanged(Image image, bool isReplace, float popDuration, float replaceDuration)
    {
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        rect.DOKill();

        if (isReplace)
        {
            Sequence sequence = DOTween.Sequence().SetTarget(rect);
            sequence.Append(rect.DOScale(0.72f, replaceDuration * 0.35f).SetEase(Ease.InBack));
            sequence.Append(rect.DOScale(1.14f, replaceDuration * 0.35f).SetEase(Ease.OutBack));
            sequence.Append(rect.DOScale(1f, replaceDuration * 0.3f).SetEase(Ease.OutCubic));
        }
        else
        {
            rect.localScale = Vector3.one * 0.65f;
            rect.DOScale(1f, popDuration).SetEase(Ease.OutBack);
        }
    }

    /// <summary>
    /// 播放启迪刻印成功时较重的确认动画。
    /// </summary>
    /// <param name="optionObject">被刻印的启迪按钮对象。</param>
    /// <param name="duration">动画持续时间。</param>
    /// <param name="onComplete">动画完成后的回调。</param>
    public static void PlayAffixCommit(GameObject optionObject, float duration, Action onComplete)
    {
        if (optionObject == null)
        {
            onComplete?.Invoke();
            return;
        }

        RectTransform rect = optionObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            onComplete?.Invoke();
            return;
        }

        rect.DOKill();
        CanvasGroup canvasGroup = optionObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = optionObject.AddComponent<CanvasGroup>();
        canvasGroup.DOKill();

        Vector3 startScale = rect.localScale;
        Sequence sequence = DOTween.Sequence().SetTarget(rect);
        sequence.Append(rect.DOScale(startScale * 0.88f, duration * 0.22f).SetEase(Ease.InQuad));
        sequence.Append(rect.DOScale(startScale * 1.18f, duration * 0.3f).SetEase(Ease.OutBack));
        sequence.Join(canvasGroup.DOFade(0.75f, duration * 0.15f).SetLoops(2, LoopType.Yoyo));
        sequence.Append(rect.DOScale(startScale, duration * 0.28f).SetEase(Ease.OutCubic));
        sequence.OnComplete(() => onComplete?.Invoke());
    }
}
