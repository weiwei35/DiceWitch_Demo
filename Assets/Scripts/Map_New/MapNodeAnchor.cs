using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // 【新增】引入事件系统

// 【新增】继承 IPointerEnterHandler 和 IPointerExitHandler
public class MapNodeAnchor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("节点配置")]
    public Enum.BoardNodeType nodeType = Enum.BoardNodeType.Empty;
    public int effectValue = 0; 
    public Sprite baseIconSprite;

    [Header("UI 表现 (子节点引用)")]
    public Image backgroundImage;     
    public Image baseIconImage;       
    public Image effectIconImage;     
    public TextMeshProUGUI valueText;

    [Header("状态图标图集配置")]
    public Sprite hpHealIcon;              
    public Sprite hpDamageIcon;            
    public Sprite resourceIcon;            
    public Sprite roomEventIcon;           
    
    public Sprite nextBattleArmorIcon;     
    public Sprite nextBattleFixedDiceIcon; 
    public Sprite blockNextDamageIcon;     
    public Sprite nextBattleDamageUpIcon;  
    public Sprite relicIcon;               

    [Header("状态颜色配置 (进度)")]
    public Color passedColor = new Color(0.5f, 0.5f, 0.5f, 1f); 
    public Color currentColor = new Color(0.5f, 1f, 1f, 1f);    
    public Color futureColor = Color.white; 
    public Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f); 

    public enum NodeState { Future, Current, Passed, Disabled }[Header("当前运行状态")]
    public NodeState currentState = NodeState.Future;

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            UpdateVisuals();
        };
#endif
    }

    public void UpdateVisuals()
    {
        // 0. 更新主建筑图标
        if (baseIconImage != null)
        {
            if (baseIconSprite != null)
            {
                baseIconImage.sprite = baseIconSprite;
                baseIconImage.gameObject.SetActive(true);
            }
            else
            {
                baseIconImage.gameObject.SetActive(false);
            }
        }

        bool isNodeActive = (currentState != NodeState.Disabled);

        // 1. 更新数值文本
        if (valueText != null)
        {
            if (isNodeActive && effectValue != 0)
            {
                valueText.text = effectValue > 0 ? $"+{effectValue}" : effectValue.ToString();
                valueText.gameObject.SetActive(true);
            }
            else
            {
                valueText.text = "";
                valueText.gameObject.SetActive(false);
            }
        }

        // 2. 更新底部的【加成状态小图标】
        if (effectIconImage != null)
        {
            if (!isNodeActive || nodeType == Enum.BoardNodeType.Empty)
            {
                effectIconImage.sprite = null;
                effectIconImage.gameObject.SetActive(false);
            }
            else
            {
                effectIconImage.gameObject.SetActive(true);
                switch (nodeType)
                {
                    case Enum.BoardNodeType.HpChange: 
                        effectIconImage.sprite = effectValue > 0 ? hpHealIcon : hpDamageIcon; 
                        break;
                    case Enum.BoardNodeType.ResourceChange: effectIconImage.sprite = resourceIcon; break;
                    case Enum.BoardNodeType.RoomEvent: effectIconImage.sprite = roomEventIcon; break;
                    case Enum.BoardNodeType.NextBattleArmor: effectIconImage.sprite = nextBattleArmorIcon; break;
                    case Enum.BoardNodeType.NextBattleFixedDice: effectIconImage.sprite = nextBattleFixedDiceIcon; break;
                    case Enum.BoardNodeType.BlockNextDamage: effectIconImage.sprite = blockNextDamageIcon; break;
                    case Enum.BoardNodeType.NextBattleDamageUp: effectIconImage.sprite = nextBattleDamageUpIcon; break;
                    case Enum.BoardNodeType.Relic: effectIconImage.sprite = relicIcon; break;
                    default: effectIconImage.gameObject.SetActive(false); break;
                }
            }
        }

        // 3. 更新底图颜色（进度状态）
        if (backgroundImage != null)
        {
            switch (currentState)
            {
                case NodeState.Passed: backgroundImage.color = passedColor; break;
                case NodeState.Current: backgroundImage.color = currentColor; break;
                case NodeState.Future: backgroundImage.color = futureColor; break;
                case NodeState.Disabled: backgroundImage.color = disabledColor; break; 
            }
        }
    }

    public void SetState(NodeState newState)
    {
        currentState = newState;
        UpdateVisuals();
    }

    public void OnNodeClicked()
    {
        Debug.Log($"<color=#00FF00>点击了地图节点</color> 类型: {nodeType}, 数值: {effectValue}");
    }

    // =========================================================
    // 【新增】提取 Tooltip 标题和内容的方法
    // =========================================================
    private void GetTooltipInfo(out string header, out string content)
    {
        if (currentState == NodeState.Disabled)
        {
            header = "已失效";
            content = "<color=#888888>该路线已废弃，无法触发任何效果。</color>";
            return;
        }

        switch (nodeType)
        {
            case Enum.BoardNodeType.HpChange:
                if (effectValue > 0)
                {
                    header = "恢复泉水";
                    content = $"<color=#008800>恢复 {effectValue} 点生命值</color>";
                }
                else
                {
                    header = "危险陷阱";
                    content = $"<color=#FF0000>失去 {Mathf.Abs(effectValue)} 点生命值</color>";
                }
                break;
            
            case Enum.BoardNodeType.ResourceChange:
                if (effectValue > 0)
                {
                    header = "宝藏";
                    content = $"<color=#0000FF>获得 {effectValue} 点粉尘</color>";
                }
                else
                {
                    header = "强盗营地";
                    content = $"<color=#FF0000>失去 {Mathf.Abs(effectValue)} 点粉尘</color>";
                }
                break;
            
            case Enum.BoardNodeType.NextBattleArmor:
                header = "坚固防线";
                content = $"<color=#3333FF>下场战斗开局获得 {effectValue} 点护甲</color>";
                break;
            
            case Enum.BoardNodeType.NextBattleFixedDice:
                header = "命运干预";
                content = $"<color=#AA00AA>下场战斗第一回合必定有一枚骰子掷出 {effectValue} 点</color>";
                break;
            
            case Enum.BoardNodeType.BlockNextDamage:
                header = "神圣护盾";
                content = "<color=#0088FF>抵消下一次受到的任何伤害\n(地图陷阱或战斗通用)</color>";
                break;
            
            case Enum.BoardNodeType.NextBattleDamageUp:
                header = "磨刀石";
                content = $"<color=#FF8800>下场战斗期间，所有伤害增加 {effectValue} 点</color>";
                break;
            
            case Enum.BoardNodeType.Relic:
                header = "远古遗物";
                content = "<color=#FFD700>获得一件随机遗物</color>";
                break;
                
            case Enum.BoardNodeType.RoomEvent:
                header = "未知的挑战";
                content = "触发该房间的主事件或战斗";
                break;
                
            case Enum.BoardNodeType.Empty:
            default:
                header = "安全空地";
                content = "这里很安全，无事发生";
                break;
        }
    }

    // =========================================================
    // 【新增】实现鼠标悬停触发
    // =========================================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipSystem.Instance != null)
        {
            // 获取数据
            GetTooltipInfo(out string header, out string content);
            
            // 调用你原有的 Show 方法
            TooltipSystem.Instance.Show(content, header);
            
            // 节点图标微小放大反馈
            transform.localScale = Vector3.one * 1.1f;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipSystem.Instance != null)
        {
            TooltipSystem.Instance.Hide();
            transform.localScale = Vector3.one;
        }
    }
}