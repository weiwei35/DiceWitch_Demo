using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MagicSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Base")]
    public Image slotBorder;
    public Image abilityIcon;
    public GameObject lockIcon;
    public TextMeshProUGUI slotIndexText;

    [Header("Attribute Info")]
    public GameObject levelBadgeObj; // 【新增】等级角标的父物体
    public TextMeshProUGUI levelText; // 【新增】显示 "Lv.5"

    private MagicCircleSlot _targetSlot;

    public void Setup(MagicCircleSlot slotData)
    {
        _targetSlot = slotData;
        if (slotIndexText) slotIndexText.text = (slotData.slotID + 1).ToString();

        if (slotData.isUnlocked)
        {
            lockIcon.SetActive(false);
            slotBorder.color = Color.white;
            
            // 1. 设置法术图标
            if (slotData.currentDice != null && slotData.currentDice.boundAbility != null)
            {
                abilityIcon.gameObject.SetActive(true);
                abilityIcon.sprite = slotData.currentDice.boundAbility.icon;
                abilityIcon.color = Color.white;
            }
            else
            {
                // 白板状态
                abilityIcon.gameObject.SetActive(true);
                // abilityIcon.sprite = null; // 或者默认图
                // abilityIcon.color = new Color(1, 1, 1, 0.2f);
            }

            // 2. 【新增】设置属性等级角标
            if (slotData.currentAttribute != null && slotData.currentAttribute.data != null)
            {
                levelBadgeObj.SetActive(true);
                // 显示绿色或者显眼的颜色
                levelText.text = $"{slotData.currentAttribute.level}";
            }
            else
            {
                levelBadgeObj.SetActive(false);
            }
        }
        else
        {
            // 锁定状态
            lockIcon.SetActive(true);
            abilityIcon.gameObject.SetActive(false);
            levelBadgeObj.SetActive(false);
            slotBorder.color = Color.gray;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_targetSlot.isUnlocked) return;

        // A. 显示 Tips (复用你的 TooltipSystem)
        string title = _targetSlot.currentDice?.diceName ?? "空槽位";
        string desc = "暂无属性";
        if (_targetSlot.currentAttribute != null && _targetSlot.currentAttribute.data != null)
        {
            var attr = _targetSlot.currentAttribute;
            desc = $"{attr.data.attributeName} Lv.{attr.level}\n效果: +{attr.GetCurrentValue()}";
        }
        TooltipSystem.Instance.Show(desc, title);

        // B. 3D 骰子高亮联动
        if (_targetSlot.currentDice != null)
        {
            // 通知 DiceThrower 高亮这颗骰子
            FindObjectOfType<DiceThrower>().HighlightDice(_targetSlot.currentDice);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 隐藏 Tips
        TooltipSystem.Instance.Hide();
        
        // 取消高亮
        FindObjectOfType<DiceThrower>().StopHighlight();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_targetSlot.isUnlocked)
        {
            // 通知控制器打开 "微型操作栏"，并传入当前按钮的位置（方便定位菜单）
            GameFlowController.Instance.OnSlotClicked(_targetSlot, transform.position);
        }
    }

    public void SetSelectionState(bool isSelecting)
    {
        // 1. 安全检查：只有解锁的槽位才需要闪烁
        if (!_targetSlot.isUnlocked) return;

        // 2. 杀掉旧动画 (防止多次调用叠加)
        slotBorder.DOKill(); 

        if (isSelecting)
        {
            // >> 开启闪烁 <<
            // 颜色从白色变到青色（或者黄色），循环往复
            // SetLoops(-1, LoopType.Yoyo) 代表无限循环、悠悠球式往返
            slotBorder.DOColor(Color.cyan, 0.5f).SetLoops(-1, LoopType.Yoyo);
            
            // 可选：加个缩放呼吸效果
            // transform.DOScale(1.1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            // >> 停止闪烁 <<
            // 恢复颜色为白色 (解锁状态默认色)
            slotBorder.color = Color.white;
            // transform.localScale = Vector3.one;
        }
    }
    private void OnDisable()
    {
        // 1. 强制干掉幽灵提示框
        if (TooltipSystem.Instance != null)
        {
            TooltipSystem.Instance.Hide();
        }

        // 2. 强制干掉幽灵 3D 高亮
        DiceThrower thrower = FindObjectOfType<DiceThrower>();
        if (thrower != null)
        {
            thrower.StopHighlight();
        }

        // 3. 杀掉可能正在播放的呼吸/闪烁动画，防止切回界面时动画错乱报错
        if (slotBorder != null)
        {
            slotBorder.DOKill();
            slotBorder.color = Color.white; // 恢复默认颜色
        }
    }
}