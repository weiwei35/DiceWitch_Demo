using UnityEngine;

[CreateAssetMenu(menuName = "Status/Status_DislocationSO (错位)")]
public class Status_DislocationSO : StatusEffectSO
{
    public override int OnGlobalCalculateDamage(BattleTarget target, int incomingDamage, int usedOrder, int remainingAtThrow)
    {
        // 如果这颗骰子是用来回血等非攻击操作，避开干预
        if (incomingDamage <= 0) return incomingDamage;

        // 特性 1：首发翻倍
        if (usedOrder == 1)
        {
            Debug.Log("<color=magenta>【错位】状态生效：首颗骰子伤害暴击 x2！</color>");
            return incomingDamage * 2;
        }
        
        // 特性 2：终局归零 
        if (remainingAtThrow == 1)
        {
            Debug.Log("<color=magenta>【错位】状态生效：最后一颗骰子化为迷雾，伤害归零！</color>");
            return 0;
        }

        return incomingDamage;
    }
}