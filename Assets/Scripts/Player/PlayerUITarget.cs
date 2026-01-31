using UnityEngine;

// 继承 BattleTarget，这样骰子就能识别它
public class PlayerUITarget : BattleTarget
{
    void Awake()
    {
        team = Enum.TargetTeam.Player; // 标记为玩家阵营
    }
    public override void OnHit(DiceFaceData data)
    {
        // 1. 如果是攻击骰子 -> 拖给自己算作“格挡/加甲”
        if (data.type == Enum.DiceActionType.Attack)
        {
            GainArmor(data.value);
        }
        // 2. 如果是防御骰子 -> 加甲
        else if (data.type == Enum.DiceActionType.Defend)
        {
            GainArmor(data.value);
        }
        // 3. 只有特殊的“诅咒/陷阱”骰子才真的扣血
        // (假设你以后加了 DiceActionType.Curse)
        // else if (data.type == DiceActionType.Curse)
        // {
        //     TakeDamage(data);
        // }
    }

    public override void TakeDamage(DiceFaceData damageData)
    {
        Debug.Log("UI玩家受到伤害: " + damageData.value);
        PlayerManager.Instance.TakeDamage(damageData.value);
        
        // 可选：播放 UI 震动动画
        // transform.DOShakeAnchorPos(...)
    }

    public override void GainArmor(int amount)
    {
        Debug.Log($"UI玩家获得护甲: {amount}");
        PlayerManager.Instance.AddArmor(amount);
        
        // 可选：播放护甲特效
    }

    public override void ApplyDirectValue(int value,bool isChainReaction = false)
    {
        // 处理分身骰等直接数值
        GainArmor(value);
    }

    // 状态相关的如果不做可以留空，或者实现 UI 状态栏
    public override void ApplyStatus(StatusEffectSO status, int amount) { }
}