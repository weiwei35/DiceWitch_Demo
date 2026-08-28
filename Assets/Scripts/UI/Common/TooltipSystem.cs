using UnityEngine;
using UnityEngine.UI; // 引用 LayoutRebuilder
using TMPro;

public class TooltipSystem : MonoBehaviour
{
    public static TooltipSystem Instance;

    [Header("UI References")]
    public GameObject tooltipPanel;       // 拖入你的 Panel
    public TextMeshProUGUI tooltipText;   // 拖入 Panel 下的 Text
    public RectTransform panelRect;       // 拖入 Panel 的 RectTransform

    void Awake()
    {
        Instance = this;
        Hide();
    }

    void Update()
    {
        // 只有显示的时候才计算位置
        if (tooltipPanel.activeSelf)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        Vector2 mousePos = Input.mousePosition;
        RectTransform parentRect = panelRect.parent as RectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, mousePos, Camera.main, out Vector2 localPoint))
            return;

        Vector2 size = panelRect.rect.size;
        Vector2 pivot = panelRect.pivot;
        float halfW = parentRect.rect.width * 0.5f;
        float halfH = parentRect.rect.height * 0.5f;
        float gap = 20f;
        float pad = 10f;

        // 默认位置：鼠标右下
        float px = localPoint.x + gap;
        float py = localPoint.y - gap;

        // 计算 tooltip 四条边在父坐标系中的位置
        float tipRight = px + (1f - pivot.x) * size.x;
        float tipBottom = py - pivot.y * size.y;
        float tipLeft = px - pivot.x * size.x;
        float tipTop = py + (1f - pivot.y) * size.y;

        // 超出右边界 → 翻转到鼠标左侧
        if (tipRight > halfW - pad)
            px = localPoint.x - gap - size.x;

        // 超出下边界 → 翻转到鼠标上方
        if (tipBottom < -halfH + pad)
            py = localPoint.y + gap + size.y;

        // 兜底钳制，确保不超出任意边界
        tipRight = px + (1f - pivot.x) * size.x;
        tipBottom = py - pivot.y * size.y;
        tipLeft = px - pivot.x * size.x;
        tipTop = py + (1f - pivot.y) * size.y;

        if (tipLeft < -halfW + pad) px += (-halfW + pad) - tipLeft;
        else if (tipRight > halfW - pad) px -= tipRight - (halfW - pad);
        if (tipBottom < -halfH + pad) py += (-halfH + pad) - tipBottom;
        else if (tipTop > halfH - pad) py -= tipTop - (halfH - pad);

        panelRect.localPosition = new Vector2(px, py);
    }

    public void Show(string content, string header = "")
    {
        // 1. 设置文字
        if (string.IsNullOrEmpty(header))
        {
            tooltipText.text = content;
        }
        else
        {
            tooltipText.text = $"<size=110%><b>{header}</b></size>\n<color=#000000>{content}</color>";
        }

        // 2. 激活物体
        tooltipPanel.SetActive(true);

        // 3. 【关键】强制刷新布局！
        // 这一步解决了“背景框不随文字变大”的问题
        // 有时候需要刷新两层：先刷新文字大小，再刷新背景大小
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    public void Hide()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }
}