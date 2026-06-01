using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

public class DiceSquadGroup : MonoBehaviour
{
    public List<DiceDragger> memberDice = new List<DiceDragger>();
    
    private DiceDragger _leader;
    private bool _isDragging = false;

    // --- 1. 初始化 ---
    public void Initialize(List<DiceDragger> diceList)
    {
        memberDice = diceList;
        foreach (var d in memberDice)
        {
            d.squadGroup = this; 
        }
    }

    public void ArrangeAt(Vector3 centerPos, float duration)
    {
        memberDice.RemoveAll(d => d == null);
        int count = memberDice.Count;
        if (count == 0) return;

        float clusterRadius = 0.09f;
        Camera cam = Camera.main;

        for (int i = 0; i < count; i++)
        {
            DiceDragger dragger = memberDice[i];
            PhysicsDice physicsDice = dragger.GetComponent<PhysicsDice>();
            Vector3 offset = Vector3.zero;
            if (count > 1)
            {
                float angle = i * Mathf.PI * 2f / count;
                float radius = i == 0 ? 0f : clusterRadius;
                offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            Vector3 targetPos = centerPos + offset;

            dragger.SetKinematic(true);
            dragger.transform.DOJump(targetPos, 0.04f, 1, duration).SetEase(Ease.OutQuad);

            Vector3 euler = physicsDice != null && DiceThrower.Instance != null
                ? DiceThrower.Instance.GetOrganizedEuler(physicsDice, cam, physicsDice.CurrentResultIndex)
                : dragger.transform.eulerAngles;
            dragger.transform.DORotate(euler, duration).SetEase(Ease.OutQuad);
        }

        DOVirtual.DelayedCall(duration, CaptureMemberTrayPoses);
    }

    private void CaptureMemberTrayPoses()
    {
        foreach (var d in memberDice)
        {
            if (d != null) d.CaptureTrayPose();
        }
    }

    // --- 2. 拖拽开始 ---
    public void OnSquadDragStart(DiceDragger leader)
    {
        _leader = leader;
        _isDragging = true;

        // 让所有队员标记为拖拽状态，关闭物理，防止乱滚
        foreach (var d in memberDice)
        {
            d.isDragging = true; 
            d.SetKinematic(true);
            d.CaptureTrayPose();
        }
    }

    // --- 3. 拖拽中：画箭头 + 队员排队 ---
    public void OnSquadDragUpdate(Vector3 mouseWorldPos)
    {
        if (!_isDragging || _leader == null) return;

        // 1. 队长不动，只画箭头 (逻辑同 DiceDragger)
        // 我们可以直接调用 leader 的方法来画线，或者在这里重写一遍
        // 为了方便，我们在 DiceDragger 里把 UpdateTargetingArrow 设为 public 或者 internal
        _leader.UpdateTargetingArrow(); // 需要你去 Dragger 里把那个方法改成 public

        // 2. 队员动作
        // 既然队长不动了，队员们也可以不动，或者围着队长转圈/跳动（增加动感）
        // 之前的蛇形跟随是基于队长位移的，如果队长不动，队员也不会动。
        // 你可以加一点 Perlin Noise 让它们原地抖动，显得很急切想攻击。
        
        // 队员保持当前阵型，只由队长负责指示目标。
    }

    // --- 4. 拖拽结束 ---
    public void OnSquadDragEnd(BattleTarget target)
    {
        _isDragging = false;
        TargetingArrow.Instance.Hide(); // 隐藏箭头

        if (target != null)
        {
            // 攻击！
            StartCoroutine(SequenceAttack(target));
        }
        else
        {
            // 没打中，全部归位
            Disperse();
        }
    }

    // --- 5. 序列攻击逻辑 ---
    IEnumerator SequenceAttack(BattleTarget target)
    {
        // 创建一个队列来执行攻击
        Queue<DiceDragger> attackQueue = new Queue<DiceDragger>();
        
        // 1. 队长先上！
        attackQueue.Enqueue(_leader);
        
        // 2. 队员跟上！
        foreach (var d in memberDice)
        {
            if (d != _leader) attackQueue.Enqueue(d);
        }

        // 清空列表，交接控制权给攻击队列
        memberDice.Clear(); 

        while (attackQueue.Count > 0)
        {
            // --- 关键检查：目标死了没？ ---
            bool isTargetDead = false;
            if (target == null) isTargetDead = true;
            else if (target.team == GameEnums.TargetTeam.Enemy)
            {
                EnemyTarget enemy = (EnemyTarget)target;
                if(enemy.currentHp <= 0)
                    isTargetDead = true;
            }
            // 如果是玩家目标，一般不会死，或者满血了也可以停止

            if (isTargetDead)
            {
                Debug.Log("目标已清除，剩余分身返回！");
                break; // 跳出循环，处理剩下的骰子
            }

            // 取出一个攻击
            DiceDragger attacker = attackQueue.Dequeue();
            
            if (attacker != null)
            {
                // 获取数据 (通常是1点)
                var data = attacker.GetComponent<PhysicsDice>().GetCurrentData();

                if (BattleManager.Instance != null)
                {
                    BattleManager.Instance.TriggerPlayerUseDice();
                }

                // 启动抛物线攻击 (复用你现有的逻辑)
                // 注意：这里我们不需要等待 StartCoroutine 返回，因为我们希望稍微重叠一点节奏
                // 但如果你想要严格的一个接一个，就加 yield return
                int usedOrder = BattleManager.Instance != null ? BattleManager.Instance.diceUsedThisTurn : 1;
                int remaining = DiceThrower.Instance.GetValidDiceCount();
                yield return attacker.StartCoroutine(attacker.FlyAndHit(target, data, usedOrder, remaining));
                
                // 节奏间隔：哒..哒..哒..
                yield return new WaitForSeconds(0.15f);
            }
        }

        // --- 6. 处理剩下的骰子 (返还) ---
        foreach (var remaining in attackQueue)
        {
            if (remaining != null)
            {
                remaining.ReturnToTray();
                remaining.squadGroup = null; // 解除编队，恢复自由身
                remaining.SetKinematic(false);
            }
        }

        if (attackQueue.Count > 1)
        {
            RegroupSurvivors(attackQueue.ToList());
        }

        // 任务完成，销毁小队控制器
        Destroy(gameObject);
    }

    void Disperse()
    {
        foreach (var d in memberDice)
        {
            d.ReturnToTray(true);
            d.isDragging = false;
        }
    }
    void RegroupSurvivors(List<DiceDragger> survivors)
    {
        // 创建一个新的空物体作为控制器
        GameObject newGroupObj = new GameObject($"Squad_Regrouped_{Time.frameCount}");
        DiceSquadGroup newSquad = newGroupObj.AddComponent<DiceSquadGroup>();

        // 初始化新小队
        // 注意：Initialize 方法里已经写了 d.squadGroup = this; 
        // 所以这里会自动把幸存者的归属权转交给新小队
        newSquad.Initialize(survivors);

        Debug.Log($"重组完成！{survivors.Count} 个分身已归队。");
    }
}
