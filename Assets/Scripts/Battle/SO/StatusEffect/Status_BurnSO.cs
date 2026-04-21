using UnityEngine;

[CreateAssetMenu(menuName = "DiceWitch/Status/Burn")]
public class Status_BurnSO : StatusEffectSO
{
    public override void OnTurnStart(BattleTarget target, int stacks)
    {
        Debug.Log($"<color=orange>燃烧生效！造成 {stacks} 点伤害</color>");
        
        // 【重构】打包一个异常状态伤害，推入管线。这样算作正规伤害，但不会触发反伤。
        DamageInfo dotInfo = new DamageInfo(target, target, stacks, DamageType.StatusDOT);
        BattleManager.Instance.ProcessDamage(dotInfo);

        // 减少层数
        if (target is EnemyTarget enemy)
        {
            enemy.ApplyStatus(this, -1);
        }
    }
}