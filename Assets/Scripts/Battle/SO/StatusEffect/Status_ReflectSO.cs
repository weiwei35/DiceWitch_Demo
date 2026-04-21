using UnityEngine;

[CreateAssetMenu(menuName = "DiceWitch/Status/Reflect")]
public class Status_ReflectSO : StatusEffectSO
{
    public GameObject reflectVFX; 

    // 在伤害真正造成之前拦截它。
    public override void OnBeforeDefend(BattleTarget target, DamageInfo info, int stacks)
    {
        // 1. 防死循环：如果这伤害本身就是别人反弹过来的，或者是毒伤，绝不触发反伤！
        if (info.DmgType == DamageType.Reflect || info.DmgType == DamageType.StatusDOT || info.FinalDamage <= 0) 
            return;

        // 2. 尝试获取攻击者。如果骰子没传 attacker，我们就默认反弹给玩家
        BattleTarget realAttacker = info.Attacker;
        if (realAttacker == null)
        {
            realAttacker = FindObjectOfType<PlayerUITarget>();
        }

        // 3. 获取 100% 的面值伤害准备反弹
        int reflectDamage = info.FinalDamage;

        Debug.Log($"<color=orange>【反伤触发】免疫了 {reflectDamage} 点伤害，并向 {realAttacker?.name} 反弹 100% 伤害！</color>");
        
        // 4. 打包反弹伤害，塞入管线发给攻击者
        if (realAttacker != null)
        {
            DamageInfo reflectInfo = new DamageInfo(target, realAttacker, reflectDamage, DamageType.Reflect);
            BattleManager.Instance.ProcessDamage(reflectInfo);
        }

        if (reflectVFX != null) 
            Instantiate(reflectVFX, target.transform.position, Quaternion.identity);

        // 5. 【关键修改2】实现“攻击无效”：将本次要打在自己身上的伤害强制清零！
        info.FinalDamage = 0;

        // 6. 消耗 1 层反伤Buff
        if (target is EnemyTarget enemy) 
        {
            enemy.ApplyStatus(this, -1);
        }
    }
}