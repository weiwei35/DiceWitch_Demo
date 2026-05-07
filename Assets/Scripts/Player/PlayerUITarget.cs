using UnityEngine;
using System.Collections.Generic;

// 继承 BattleTarget，这样骰子就能识别它
public class PlayerUITarget : BattleTarget
{
    // =========================================================
    // 状态管理 (和 EnemyTarget 保持一致，方便玩家未来吃 Buff)
    // =========================================================
    private Dictionary<StatusEffectSO, int> currentStatuses = new Dictionary<StatusEffectSO, int>();
    private List<StatusEffectSO> _statusCache = new List<StatusEffectSO>();

    public static PlayerUITarget Instance;

    void Awake()
    {
        Instance = this;
        team = GameEnums.TargetTeam.Player; // 标记为玩家阵营
        targetName = "Player";         // 方便管线 Debug 时打印名字
    }

    // =========================================================
    // 特殊机制：玩家被骰子直接砸中时的逻辑 (保留了你的独特设计)
    // =========================================================
    public override void OnHit(DiceFaceData data, BattleTarget attacker = null)
    {
        // 1. 如果是攻击骰子 -> 拖给自己算作“格挡/加甲” (绕过伤害管线，直接转为护甲)
        if (data.type == GameEnums.DiceActionType.Attack)
        {
            GainArmor(data.value);
        }
        // 2. 如果是防御骰子 -> 加甲
        else if (data.type == GameEnums.DiceActionType.Defend)
        {
            GainArmor(data.value);
        }
        // 3. 如果未来有“诅咒/陷阱”骰子，就可以打包丢进管线
        // else if (data.type == GameEnums.DiceActionType.Curse)
        // {
        //     DamageInfo info = new DamageInfo(attacker, this, data.value, DamageType.Magic);
        //     BattleManager.Instance.ProcessDamage(info);
        // }
    }

    // =========================================================
    // 基础能力
    // =========================================================
    public override void GainArmor(int amount)
    {
        Debug.Log($"UI玩家获得护甲: {amount}");
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.AddArmor(amount);
            
            // 通知全局：玩家获得了护甲 (触发互斥等特殊机制)
            var keys = new List<StatusEffectSO>(currentStatuses.Keys);
            foreach (var status in keys)
            {
                status.OnPlayerGainArmor(this, amount, currentStatuses[status]);
            }
        }
        // 可选：播放护甲特效
    }

    public override void ApplyStatus(StatusEffectSO status, int amount)
    {
        if (status == null) return;

        if (currentStatuses.ContainsKey(status))
            currentStatuses[status] += amount;
        else
            currentStatuses.Add(status, amount);

        if (currentStatuses[status] <= 0)
        {
            currentStatuses.Remove(status);
            // TODO: 通知 UI 移除玩家头像上的 Buff 图标
        }
        else
        {
            // TODO: 通知 UI 更新玩家头像上的 Buff 图标
        }
    }

    // =========================================================
    // 【核心】管线钩子实现 (让玩家身上的 Buff 也能拦截伤害！)
    // =========================================================
    public override void TriggerBeforeAttack(DamageInfo info)
    {
        _statusCache.Clear();
        _statusCache.AddRange(currentStatuses.Keys); 
        for (int i = _statusCache.Count - 1; i >= 0; i--)
        {
            if (currentStatuses.TryGetValue(_statusCache[i], out int stacks))
                _statusCache[i].OnBeforeAttack(this, info, stacks);
        }
    }

    public override void TriggerBeforeDefend(DamageInfo info)
    {
        _statusCache.Clear();
        _statusCache.AddRange(currentStatuses.Keys);
        for (int i = _statusCache.Count - 1; i >= 0; i--)
        {
            if (currentStatuses.TryGetValue(_statusCache[i], out int stacks))
                _statusCache[i].OnBeforeDefend(this, info, stacks);
        }
    }

    public override void TriggerAfterTakeDamage(DamageInfo info)
    {
        _statusCache.Clear();
        _statusCache.AddRange(currentStatuses.Keys);
        for (int i = _statusCache.Count - 1; i >= 0; i--)
        {
            if (currentStatuses.TryGetValue(_statusCache[i], out int stacks))
                _statusCache[i].OnAfterTakeDamage(this, info, stacks);
        }
    }

    // =========================================================
    // 【核心】管线终点：最终执行伤害
    // =========================================================
    public override void ExecuteDamage(int actualDamage)
    {
        Debug.Log($"<color=red>玩家即将受到最终伤害: {actualDamage}</color>");
        
        if (PlayerManager.Instance != null)
        {
            // 呼叫你的 PlayerManager 进行实际的扣护甲/扣血逻辑
            PlayerManager.Instance.TakeDamage(actualDamage);
        }

        // 可选：在玩家头像的位置弹出伤害数字
        if (actualDamage > 0 && DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowDamage(transform.position, actualDamage, false);
        }

        // 可选：播放 UI 震动动画
        // transform.DOShakeAnchorPos(...)
    }
}