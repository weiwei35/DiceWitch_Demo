using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 骰子竖列横扫转场。
/// 生成一列占满屏幕高度的骰子，从左向右依次旋转划过，并在遮罩覆盖时执行外部切换逻辑。
/// </summary>
public class DiceWipeTransition : MonoBehaviour
{
    [Header("References")]
    public RectTransform transitionRoot;
    public RectTransform diceContainer;
    public Image veilImage;
    public CanvasGroup canvasGroup;
    public List<Sprite> diceSprites = new List<Sprite>();

    [Header("Column")]
    [Min(1)] public int diceCount = 7;
    [Tooltip("骰子高度相对单格高度的倍率，略大可以形成仪式感的重叠幕布。")]
    public float diceHeightMultiplier = 1.18f;
    public float horizontalPadding = 180f;
    public float rowDelay = 0.055f;

    [Header("Motion")]
    public float sweepDuration = 0.92f;
    public float coverHoldDuration = 0.18f;
    public float exitFadeDuration = 0.22f;
    public float rotationTurns = 1.15f;
    public Ease sweepEase = Ease.InOutCubic;
    public Ease veilEase = Ease.InOutSine;

    [Header("Magic Veil")]
    public Color veilColor = new Color(0.06f, 0.02f, 0.12f, 0.82f);
    public float veilFadeInDuration = 0.34f;

    private readonly List<Image> _diceImages = new List<Image>();
    private Sequence _sequence;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        ResolveReferences();
        if (!_isPlaying)
            ResetVisualState();
    }

    private void OnDisable()
    {
        KillTransition();
    }

    /// <summary>
    /// 播放骰子横扫转场。
    /// </summary>
    /// <param name="onCovered">遮罩覆盖并短暂停顿时执行的切换逻辑。</param>
    /// <param name="onComplete">转场完全结束后的回调。</param>
    public bool Play(Action onCovered, Action onComplete = null)
    {
        ResolveReferences();
        if (transitionRoot == null || diceContainer == null || veilImage == null || canvasGroup == null)
        {
            Debug.LogError("DiceWipeTransition 引用未配置完整。必须配置 transitionRoot、diceContainer、veilImage、canvasGroup。", this);
            return false;
        }

        KillTransition();
        _isPlaying = true;
        transitionRoot.gameObject.SetActive(true);
        transitionRoot.SetAsLastSibling();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        RefreshVeil(0f);
        veilImage.raycastTarget = true;
        BuildDiceColumn();

        float coverTime = Mathf.Max(veilFadeInDuration, sweepDuration * 0.48f);
        float totalSweepTime = sweepDuration + rowDelay * Mathf.Max(0, diceCount - 1);
        float outTime = totalSweepTime + coverHoldDuration;

        _sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        _sequence.Insert(0f, veilImage.DOFade(veilColor.a, Mathf.Max(0.01f, veilFadeInDuration)).SetEase(veilEase));

        for (int i = 0; i < _diceImages.Count; i++)
        {
            Image diceImage = _diceImages[i];
            if (diceImage == null) continue;

            RectTransform rect = diceImage.rectTransform;
            float delay = i * Mathf.Max(0f, rowDelay);
            float rotation = 360f * rotationTurns * (i % 2 == 0 ? 1f : -1f);

            _sequence.Insert(delay, rect.DOAnchorPosX(GetEndX(rect), Mathf.Max(0.01f, sweepDuration)).SetEase(sweepEase));
            _sequence.Insert(delay, rect.DOLocalRotate(new Vector3(0f, 0f, rotation), Mathf.Max(0.01f, sweepDuration), RotateMode.FastBeyond360).SetEase(sweepEase));
        }

        _sequence.InsertCallback(coverTime, () => onCovered?.Invoke());
        _sequence.Insert(outTime, veilImage.DOFade(0f, Mathf.Max(0.01f, exitFadeDuration)).SetEase(veilEase));
        _sequence.Insert(outTime, canvasGroup.DOFade(0f, Mathf.Max(0.01f, exitFadeDuration)).SetEase(veilEase));
        _sequence.OnComplete(() =>
        {
            ResetVisualState();
            _isPlaying = false;
            onComplete?.Invoke();
        });

        return true;
    }

    [ContextMenu("Hide Immediate")]
    public void HideImmediate()
    {
        ResolveReferences();
        KillTransition();
        ResetVisualState();
        _isPlaying = false;
    }

    private void ResetVisualState()
    {
        if (veilImage != null) RefreshVeil(0f);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        if (veilImage != null)
            veilImage.raycastTarget = false;
        if (transitionRoot != null) transitionRoot.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (transitionRoot == null)
            transitionRoot = transform as RectTransform;
        if (diceContainer == null && transitionRoot != null)
            diceContainer = transitionRoot;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void BuildDiceColumn()
    {
        EnsureDiceImages();
        Rect area = transitionRoot.rect;
        float areaHeight = Mathf.Max(1f, area.height);
        float cellHeight = areaHeight / Mathf.Max(1, diceCount);
        float diceHeight = cellHeight * Mathf.Max(0.1f, diceHeightMultiplier);

        for (int i = 0; i < _diceImages.Count; i++)
        {
            Image diceImage = _diceImages[i];
            RectTransform rect = diceImage.rectTransform;
            Sprite sprite = PickDiceSprite(i);

            diceImage.sprite = sprite;
            diceImage.raycastTarget = false;
            diceImage.color = Color.white;
            diceImage.gameObject.SetActive(true);

            float aspect = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            rect.sizeDelta = new Vector2(diceHeight * aspect, diceHeight);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            float y = areaHeight * 0.5f - cellHeight * (i + 0.5f);
            rect.anchoredPosition = new Vector2(GetStartX(rect), y);
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-16f, 16f));
            rect.localScale = Vector3.one;
        }
    }

    private void EnsureDiceImages()
    {
        while (_diceImages.Count < diceCount)
        {
            GameObject diceObject = new GameObject("TransitionDice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = diceObject.GetComponent<RectTransform>();
            rect.SetParent(diceContainer, false);

            Image image = diceObject.GetComponent<Image>();
            image.raycastTarget = false;
            _diceImages.Add(image);
        }

        for (int i = 0; i < _diceImages.Count; i++)
        {
            if (_diceImages[i] == null) continue;
            _diceImages[i].gameObject.SetActive(i < diceCount);
        }
    }

    private Sprite PickDiceSprite(int index)
    {
        if (diceSprites == null || diceSprites.Count == 0) return null;

        for (int i = 0; i < diceSprites.Count; i++)
        {
            Sprite sprite = diceSprites[(index + i) % diceSprites.Count];
            if (sprite != null) return sprite;
        }

        return null;
    }

    private void RefreshVeil(float alpha)
    {
        Color color = veilColor;
        color.a = alpha;
        veilImage.color = color;
    }

    private float GetStartX(RectTransform diceRect)
    {
        return -transitionRoot.rect.width * 0.5f - diceRect.rect.width * 0.5f - horizontalPadding;
    }

    private float GetEndX(RectTransform diceRect)
    {
        return transitionRoot.rect.width * 0.5f + diceRect.rect.width * 0.5f + horizontalPadding;
    }

    private void KillTransition()
    {
        DOTween.Kill(this);
        if (_sequence != null)
        {
            _sequence.Kill();
            _sequence = null;
        }
    }
}
