using UnityEngine;[CreateAssetMenu(menuName = "Status/Status_RepulsionSO (互斥)")]
public class Status_RepulsionSO : StatusEffectSO
{
    public override void OnPlayerGainArmor(EnemyTarget target, int armorAmount, int stacks)
    {
        Debug.Log($"<color=green>【互斥】感知到玩家获得了 {armorAmount} 点护甲，触发互斥反应！所有剩余骰子点数 -1！</color>");

        // 查找场上所有存活的物理骰子
        var allDice = FindObjectsOfType<PhysicsDice>();
        
        foreach (var dice in allDice)
        {
            if (dice != null)
            {
                // 调用现成接口，扣掉 1 点加成！
                dice.ApplyTemporaryBonus(-1);
                
                // 可选特效：dice.transform.DOPunchScale(Vector3.one * -0.2f, 0.3f); 
            }
        }
    }
}