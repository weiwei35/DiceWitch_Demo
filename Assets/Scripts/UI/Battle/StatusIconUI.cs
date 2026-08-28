using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 必须引用这个来实现鼠标接口
using TMPro;

public class StatusIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Refs")]
    public Image iconImage;
    public TextMeshProUGUI stackText;

    private StatusEffectSO _data;
    private int _currentStacks;

    // 初始化方法 (替代之前的直接 GetComponent 修改)
    public void Setup(StatusEffectSO data, int stacks)
    {
        _data = data;
        _currentStacks = stacks;

        // 设置显示
        if (iconImage)
        {
            iconImage.sprite = data.icon;
            iconImage.color = data.color;
        }

        if (stackText)
        {
            stackText.text = stacks.ToString();
        }
    }

    // --- 鼠标悬停逻辑 ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_data == null) return;

        // 获取动态描述
        string content = _data.GetDescription(_currentStacks);
        
        // 显示 Tooltip (传入 标题 和 内容)
        TooltipSystem.Instance.Show(content, _data.statusName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance.Hide();
    }
}