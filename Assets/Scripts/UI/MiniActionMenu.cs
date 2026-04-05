using UnityEngine;
using TMPro;
using System.Text;

public class MiniActionMenu : MonoBehaviour
{
    [Header("UI References")]
    // 【注意】请在 Unity 面板中，把原来的按钮删掉，新建一个 TextMeshPro 文本框并拖拽到这里
    public TextMeshProUGUI tooltipText; 
    public float verticalOffset = 100f; 

    // --- 改为纯粹的展示方法 ---
    public void ShowTooltip(MagicCircleSlot slot, Vector3 anchorPos)
    {
        if (slot == null) return;

        // 1. 设置位置与偏移
        transform.position = anchorPos;
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 currentAnchored = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(currentAnchored.x, currentAnchored.y + verticalOffset);
        }
        transform.SetAsLastSibling();

        // 2. 组装你要展示的 Tips 内容
        StringBuilder sb = new StringBuilder();
        
        // 检查是否有骰子
        if (slot.currentDice != null)
        {
            sb.AppendLine($"<b>{slot.currentDice.diceName}</b>");
            // 检查骰子是否有附魔法术
            if (slot.currentDice.boundAbility != null)
            {
                sb.AppendLine($"<color=yellow>★ 附魔: {slot.currentDice.boundAbility.abilityName}</color>");
            }
        }
        else
        {
            sb.AppendLine("<b><color=#AAAAAA>空槽位</color></b>");
        }

        // 检查魔法阵是否有属性升级
        if (slot.currentAttribute != null && slot.currentAttribute.data != null)
        {
            sb.AppendLine($"<color=#00FF00>阵法加持: Lv.{slot.currentAttribute.level} {slot.currentAttribute.data.attributeName}</color>");
        }

        // 赋值给文本框
        if (tooltipText != null)
        {
            tooltipText.text = sb.ToString();
        }

        gameObject.SetActive(true);
    }

    // --- 鼠标移出时隐藏 ---
    public void HideTooltip()
    {
        gameObject.SetActive(false);
    }
}