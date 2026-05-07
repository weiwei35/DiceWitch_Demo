using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MapNodeAnchor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("节点配置")]
    public GameEnums.BoardNodeType nodeType = GameEnums.BoardNodeType.Empty;
    public int effectValue = 0;
    public GameEnums.BoardNodeType forgeBonusType = GameEnums.BoardNodeType.Empty; // 仅当 nodeType=Forge 时生效
    public Sprite baseIconSprite;

    [Header("UI 表现 (子节点引用)")]
    public Image backgroundImage;
    public Image baseIconImage;
    public Image effectIconImage;
    public TextMeshProUGUI valueText;

    [Header("共享配置")]
    public MapIconConfigSO iconConfig; // 所有节点共用这一份

    // 锻造行 UI 元素（自动查找子物体 ForgeRow/Icon、ForgeRow/Text）
    private Image _forgeIconImage;
    private TextMeshProUGUI _forgeText;
    private bool _forgeRowSearched;

    private void EnsureForgeRow()
    {
        if (_forgeRowSearched) return;
        _forgeRowSearched = true;
        var forgeRow = transform.Find("ForgeRow");
        if (forgeRow != null)
        {
            var icon = forgeRow.Find("Icon");
            if (icon != null) _forgeIconImage = icon.GetComponent<Image>();
            var txt = forgeRow.Find("Text");
            if (txt != null) _forgeText = txt.GetComponent<TextMeshProUGUI>();
        }
    }

    public enum NodeState { Future, Current, Passed, Disabled }
    [Header("当前运行状态")]
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
        EnsureForgeRow();

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

        // 1. 加成行：效果图标 + 数值
        //    锻造节点的加成效果由 forgeBonusType 决定
        GameEnums.BoardNodeType effectiveType = nodeType;
        if (nodeType == GameEnums.BoardNodeType.Forge && forgeBonusType != GameEnums.BoardNodeType.Empty)
            effectiveType = forgeBonusType;

        bool showEffectRow = isNodeActive && effectiveType != GameEnums.BoardNodeType.Empty && effectiveType != GameEnums.BoardNodeType.Forge;
        bool hasValue = effectValue != 0;

        if (effectIconImage != null)
        {
            if (showEffectRow && iconConfig != null)
            {
                effectIconImage.sprite = GetIconForType(effectiveType);
                effectIconImage.gameObject.SetActive(true);
            }
            else
            {
                effectIconImage.sprite = null;
                effectIconImage.gameObject.SetActive(false);
            }
        }

        if (valueText != null)
        {
            if (showEffectRow && hasValue)
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

        // 2. 锻造行 (仅 Forge 节点显示)
        bool showForgeRow = isNodeActive && nodeType == GameEnums.BoardNodeType.Forge;
        if (_forgeIconImage != null)
        {
            if (showForgeRow && iconConfig != null)
            {
                _forgeIconImage.sprite = iconConfig.forgeIcon;
                _forgeIconImage.gameObject.SetActive(true);
            }
            else
            {
                _forgeIconImage.sprite = null;
                _forgeIconImage.gameObject.SetActive(false);
            }
        }
        if (_forgeText != null)
            _forgeText.gameObject.SetActive(showForgeRow);

        // 3. 更新底图颜色（进度状态）
        if (backgroundImage != null && iconConfig != null)
        {
            switch (currentState)
            {
                case NodeState.Passed: backgroundImage.color = iconConfig.passedColor; break;
                case NodeState.Current: backgroundImage.color = iconConfig.currentColor; break;
                case NodeState.Future: backgroundImage.color = iconConfig.futureColor; break;
                case NodeState.Disabled: backgroundImage.color = iconConfig.disabledColor; break;
            }
        }
    }

    private Sprite GetIconForType(GameEnums.BoardNodeType type)
    {
        switch (type)
        {
            case GameEnums.BoardNodeType.HpChange: return effectValue > 0 ? iconConfig.hpHealIcon : iconConfig.hpDamageIcon;
            case GameEnums.BoardNodeType.ResourceChange: return iconConfig.resourceIcon;
            case GameEnums.BoardNodeType.RoomEvent: return iconConfig.roomEventIcon;
            case GameEnums.BoardNodeType.NextBattleArmor: return iconConfig.nextBattleArmorIcon;
            case GameEnums.BoardNodeType.NextBattleFixedDice: return iconConfig.nextBattleFixedDiceIcon;
            case GameEnums.BoardNodeType.BlockNextDamage: return iconConfig.blockNextDamageIcon;
            case GameEnums.BoardNodeType.NextBattleDamageUp: return iconConfig.nextBattleDamageUpIcon;
            case GameEnums.BoardNodeType.Relic: return iconConfig.relicIcon;
            case GameEnums.BoardNodeType.Forge: return iconConfig.forgeIcon;
            default: return null;
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
    // Tooltip
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
            case GameEnums.BoardNodeType.HpChange:
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

            case GameEnums.BoardNodeType.ResourceChange:
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

            case GameEnums.BoardNodeType.NextBattleArmor:
                header = "坚固防线";
                content = $"<color=#3333FF>下场战斗开局获得 {effectValue} 点护甲</color>";
                break;

            case GameEnums.BoardNodeType.NextBattleFixedDice:
                header = "命运干预";
                content = $"<color=#AA00AA>下场战斗第一回合必定有一枚骰子掷出 {effectValue} 点</color>";
                break;

            case GameEnums.BoardNodeType.BlockNextDamage:
                header = "神圣护盾";
                content = "<color=#0088FF>抵消下一次受到的任何伤害\n(地图陷阱或战斗通用)</color>";
                break;

            case GameEnums.BoardNodeType.NextBattleDamageUp:
                header = "磨刀石";
                content = $"<color=#FF8800>下场战斗期间，所有伤害增加 {effectValue} 点</color>";
                break;

            case GameEnums.BoardNodeType.Relic:
                header = "远古遗物";
                content = "<color=#FFD700>获得一件随机遗物</color>";
                break;

            case GameEnums.BoardNodeType.RoomEvent:
                header = "未知的挑战";
                content = "触发该房间的主事件或战斗";
                break;

            case GameEnums.BoardNodeType.Forge:
                header = "锻造熔炉";
                content = "<color=#FF8800>可以为骰子刻印词条</color>";
                if (forgeBonusType != GameEnums.BoardNodeType.Empty && effectValue != 0)
                    content += GetBonusEffectText();
                break;

            case GameEnums.BoardNodeType.Empty:
            default:
                header = "安全空地";
                content = "这里很安全，无事发生";
                break;
        }
    }

    private string GetBonusEffectText()
    {
        switch (forgeBonusType)
        {
            case GameEnums.BoardNodeType.HpChange:
                return effectValue > 0 ? $"\n<color=#008800>额外: 恢复 {effectValue} 点生命</color>" : $"\n<color=#FF0000>额外: 失去 {Mathf.Abs(effectValue)} 点生命</color>";
            case GameEnums.BoardNodeType.ResourceChange:
                return effectValue > 0 ? $"\n<color=#0000FF>额外: 获得 {effectValue} 点粉尘</color>" : $"\n<color=#FF0000>额外: 失去 {Mathf.Abs(effectValue)} 点粉尘</color>";
            case GameEnums.BoardNodeType.NextBattleArmor:
                return $"\n<color=#3333FF>额外: 下场战斗获得 {effectValue} 点护甲</color>";
            case GameEnums.BoardNodeType.NextBattleFixedDice:
                return $"\n<color=#AA00AA>额外: 下场战斗必有 {effectValue} 点骰子</color>";
            case GameEnums.BoardNodeType.BlockNextDamage:
                return "\n<color=#0088FF>额外: 获得圣盾</color>";
            case GameEnums.BoardNodeType.NextBattleDamageUp:
                return $"\n<color=#FF8800>额外: 下场战斗伤害 +{effectValue}</color>";
            default:
                return "";
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipSystem.Instance != null)
        {
            GetTooltipInfo(out string header, out string content);
            TooltipSystem.Instance.Show(content, header);
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
