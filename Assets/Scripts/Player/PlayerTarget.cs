using UnityEngine;

public class PlayerTarget : BattleTarget
{
    void Awake()
    {
        team = TargetTeam.Player; // 我是玩家队
    }

    public override void TakeDamage(DiceFaceData damageData)
    {
        // 玩家一般来说不会被自己的攻击骰子打中，
        // 但如果有“自残骰子”，这里就调用 PlayerManager.Instance.TakeDamage(amount);
        Debug.Log("玩家受到伤害: " + damageData.value);
        PlayerManager.Instance.TakeDamage(damageData.value);
    }

    public override void ApplyStatus(StatusEffectSO status, int amount)
    {
        
    }

    public override void GainArmor(int amount)
    {
        // 调用我们之前写好的 PlayerManager
        PlayerManager.Instance.AddArmor(amount);
        Debug.Log($"玩家获得护甲: {amount}");
        
        // 播放个特效?
    }

    public override void ApplyDirectValue(int value)
    {
        // 对于分裂骰，如果是防御骰分裂，就是加甲
        // 如果是攻击骰分裂打到自己（反弹？），就是扣血
        // 这里为了简化，假设分裂到自己身上的都是好东西（加甲/回血）
        GainArmor(value);
    }
}