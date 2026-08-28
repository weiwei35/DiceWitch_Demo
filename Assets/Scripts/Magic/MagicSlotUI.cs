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

    private MagicCircleSlot _targetSlot;
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
    }

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
                // 白板骰子：使用统一默认图标
                abilityIcon.gameObject.SetActive(true);
                abilityIcon.sprite = MagicCircleManager.Instance != null ? MagicCircleManager.Instance.defaultDiceIcon : null;
                abilityIcon.color = Color.white;
            }

        }
        else
        {
            // 锁定状态
            lockIcon.SetActive(true);
            abilityIcon.gameObject.SetActive(false);
            slotBorder.color = Color.gray;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_targetSlot.isUnlocked) return;

        MagicCircleDisplay.Instance?.MoveHandToSlot(GetSquareCenterInContainer());

        // A. 显示 Tips (复用你的 TooltipSystem)
        string title = _targetSlot.currentDice?.diceName ?? "空槽位";
        string desc = BuildTooltipDescription();
        TooltipSystem.Instance.Show(desc, title);

        // B. 3D 骰子高亮联动
        if (_targetSlot.currentDice != null)
        {
            // 通知 DiceThrower 高亮这颗骰子
            DiceThrower.Instance.HighlightDice(_targetSlot.currentDice);
        }
    }

    private string BuildTooltipDescription()
    {
        if (_targetSlot.currentDice == null) return "空槽位";

        string desc = "";
        PlayerDice dice = _targetSlot.currentDice;

        if (dice.boundAbility != null)
        {
            desc += $"<color=yellow>★ {dice.boundAbility.abilityName}</color>";
            if (!string.IsNullOrEmpty(dice.boundAbility.description))
                desc += $"\n{dice.boundAbility.description}";
        }

        if (dice.forgeSlots != null)
        {
            bool hasForged = false;
            foreach (var slot in dice.forgeSlots)
            {
                if (slot != null && slot.isForged && slot.affix != null)
                {
                    if (!hasForged)
                    {
                        if (!string.IsNullOrEmpty(desc)) desc += "\n\n";
                        desc += "<color=#FF8800>◆ 已刻印词条</color>";
                        hasForged = true;
                    }

                    desc += $"\nT{slot.tier}: {slot.affix.affixName}";
                    if (!string.IsNullOrEmpty(slot.affix.description))
                        desc += $"\n{slot.affix.description}";
                }
            }
        }

        return string.IsNullOrEmpty(desc) ? "暂无属性" : desc;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        MagicCircleDisplay.Instance?.ReturnHandToDefault();

        // 隐藏 Tips
        TooltipSystem.Instance.Hide();
        
        // 取消高亮
        DiceThrower.Instance.StopHighlight();
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

    public void SetRadialLayout(Vector2 targetPosition)
    {
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        // 节点底图已经合并到魔法阵背景中，运行时节点只负责显示内容和接收交互。
        if (slotBorder != null)
            slotBorder.enabled = false;

        SetChildCenter(abilityIcon != null ? abilityIcon.rectTransform : null, Vector2.zero);
        SetChildCenter(lockIcon != null ? lockIcon.transform as RectTransform : null, Vector2.zero);
        _rectTransform.anchoredPosition = targetPosition;
    }

    public Vector2 GetSquareCenterInContainer()
    {
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        return _rectTransform.anchoredPosition;
    }

    private static void SetChildCenter(RectTransform child, Vector2 position)
    {
        if (child == null) return;

        child.anchorMin = new Vector2(0.5f, 0.5f);
        child.anchorMax = new Vector2(0.5f, 0.5f);
        child.anchoredPosition = position;
    }

    private void OnDisable()
    {
        // 1. 强制干掉幽灵提示框
        if (TooltipSystem.Instance != null)
        {
            TooltipSystem.Instance.Hide();
        }

        // 2. 强制干掉幽灵 3D 高亮
        DiceThrower thrower = DiceThrower.Instance;
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
