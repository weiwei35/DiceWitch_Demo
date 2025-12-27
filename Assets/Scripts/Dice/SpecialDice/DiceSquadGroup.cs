using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
        }
    }

    // --- 3. 拖拽中：画箭头 + 队员排队 ---
    public void OnSquadDragUpdate(Vector3 mouseWorldPos)
    {
        if (!_isDragging || _leader == null) return;

        // A. 绘制攻击箭头 (逻辑与 DiceDragger 单体一致)
        // ---------------------------------------------------------
        Vector3 startPos = _leader.transform.position;
        Vector3 endPos = mouseWorldPos; // 默认终点

        // 简单的吸附检测 (为了箭头能吸附到怪身上)
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        int mask = (1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Player"));
        if (Physics.Raycast(ray, out hit, 1000f, mask))
        {
            endPos = hit.transform.position; // 吸附
        }
        
        // 调用箭头单例显示
        TargetingArrow.Instance.Show(startPos, endPos);


        // B. 队员蛇形排队 (修复数组越界 Bug)
        // ---------------------------------------------------------
        // 逻辑：所有队员在 Leader 身后排成一串，像贪吃蛇一样
        float spacing = 0.6f; // 排队间距
        float moveSpeed = 10f; // 归队速度

        // 链式目标：第一个人跟Leader，第二个人跟第一个人...
        Transform targetToFollow = _leader.transform; 

        foreach (var member in memberDice)
        {
            // 队长自己不用动，他是锚点
            if (member == _leader) continue;

            // 计算目标位置：目标(前一个人)的位置
            Vector3 targetPos = targetToFollow.position;

            // 保持距离逻辑：如果离得太近就不动，离远了就靠过去
            // 这里做一个简单的“背后吸附”：往目标位置移动，但保留 spacing 距离
            Vector3 direction = (member.transform.position - targetPos).normalized;
            // 防止重叠导致的向量为0
            if (direction == Vector3.zero) direction = Random.onUnitSphere; 

            // 我们希望队员排在目标 "后面" (这里简化为直接 Lerp 过去)
            // 如果你想做得很精致，可以计算箭头的反方向，让它们排在屁股后面
            // 这里使用最稳健的“链式逼近”：
            float dist = Vector3.Distance(member.transform.position, targetPos);
            
            if (dist > spacing)
            {
                // 目标位置是：前一个人位置 + 指向我的方向 * 间距
                // 这样就像是被绳子牵着走
                Vector3 desiredPos = targetPos + (member.transform.position - targetPos).normalized * spacing;
                member.transform.position = Vector3.Lerp(member.transform.position, desiredPos, Time.deltaTime * moveSpeed);
            }

            // 更新链条：下一个人跟着我
            targetToFollow = member.transform;
        }
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
            else if (target.team == TargetTeam.Enemy)
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
                
                // 启动抛物线攻击 (复用你现有的逻辑)
                // 注意：这里我们不需要等待 StartCoroutine 返回，因为我们希望稍微重叠一点节奏
                // 但如果你想要严格的一个接一个，就加 yield return
                yield return attacker.StartCoroutine(attacker.FlyAndHit(target, data));
                
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
            d.ReturnToTray();
            d.SetKinematic(false);
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