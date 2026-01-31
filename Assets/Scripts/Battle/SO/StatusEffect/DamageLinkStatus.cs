using UnityEngine;
using System.Collections.Generic;

public class DamageLinkStatus : MonoBehaviour
{
    private EnemyTarget _owner;
    private float _transferRatio = 0.5f;
    private GameObject _vfxPrefab;

    // 初始化
    public void Setup(EnemyTarget owner, float ratio, GameObject vfx)
    {
        _owner = owner;
        _transferRatio = ratio;
        _vfxPrefab = vfx;

        // 订阅受伤事件
        _owner.OnDamageTaken += HandleDamageDistribute;
        
        // 订阅回合结束事件 (需要在 BattleManager 里加这个事件，或者手动轮询)
        // 这里假设 BattleManager 有一个 OnPlayerTurnEnd 的事件，如果没有，下面教你加
        BattleManager.Instance.OnPlayerTurnEnd += RemoveStatus;
        
        Debug.Log($"{owner.name} 被挂上了链枷！");
    }

    void OnDestroy()
    {
        // 记得取消订阅，防止报错
        if (_owner != null) _owner.OnDamageTaken -= HandleDamageDistribute;
        if (BattleManager.Instance != null) BattleManager.Instance.OnPlayerTurnEnd -= RemoveStatus;
    }

    // 核心逻辑：分摊伤害
    private void HandleDamageDistribute(int incomingDamage)
    {
        // 1. 计算传递的伤害
        int spreadDamage = Mathf.FloorToInt(incomingDamage * _transferRatio);
        if (spreadDamage < 1) return;

        // 2. 获取其他敌人
        List<EnemyTarget> enemies = BattleManager.Instance.enemies;
        
        foreach (var enemy in enemies)
        {
            // 跳过自己，跳过死人
            if (enemy == _owner || enemy == null || enemy.currentHp <= 0) continue;

            // 3. 施加伤害 (关键：标记为 isChainReaction = true)
            // 这样被波及的敌人如果也有链枷，就不会再次触发，防止无限循环
            enemy.ApplyDirectValue(spreadDamage, true);
            
            // 播放特效
            if (_vfxPrefab != null) 
                Instantiate(_vfxPrefab, enemy.transform.position, Quaternion.identity);
        }
        
        Debug.Log($"链枷生效！{_owner.name} 传递了 {spreadDamage} 点伤害给队友。");
    }

    // 回合结束销毁自己
    private void RemoveStatus()
    {
        Destroy(this);
    }
}