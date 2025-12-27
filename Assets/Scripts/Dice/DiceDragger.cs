using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PhysicsDice))]
public class DiceDragger : MonoBehaviour
{
    private PhysicsDice physicsDice;
    public bool isDragging = false;
    public bool IsDragging => isDragging;
    
    public DiceSquadGroup squadGroup; // 我属于哪个小队？
    private Vector3 originalPos; 
    private Quaternion originalRot;
    
    private Rigidbody rb;
    
    // 鼠标在屏幕上的深度（Z轴距离）
    private float mZCoord;

    void Awake()
    {
        physicsDice = GetComponent<PhysicsDice>();
        rb = GetComponent<Rigidbody>();
        originalPos = transform.position;
        originalRot = transform.rotation;
    }
    // 提供给外部控制物理的接口
    public void SetKinematic(bool state)
    {
        rb.isKinematic = state;
    }
    void OnMouseDown()
    {
        if (physicsDice.isRolling) return;
        
        // 记录当前位置为“归位点”，这样每次拖拽失败都会回到拿起的地方，而不是出生点
        originalPos = transform.position;
        originalRot = transform.rotation;
        if (squadGroup != null)
        {
            // 如果有小队，通知小队“我被抓了，我是队长”
            squadGroup.OnSquadDragStart(this);
        }
        else
        {
            isDragging = true;
            
            // 计算摄像机到骰子的距离，用于后续鼠标坐标转换
            mZCoord = Camera.main.WorldToScreenPoint(transform.position).z;
            rb.isKinematic = true;
        }
        
        // TODO:播放个音效？
    }

    void OnMouseDrag()
    {
        if (squadGroup != null)
        {
            // 让小队去计算位置，我自己不跑了
            squadGroup.OnSquadDragUpdate(GetMouseAsWorldPoint());
        }
        else
        {
            if (!isDragging) return;

            // 1. 默认终点：鼠标位置
            Vector3 endPos = GetMouseAsWorldPoint();

            // 2. 射线检测：看看鼠标是不是正指着某个敌人
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int playerLayer = LayerMask.NameToLayer("Player");
            int finalMask = (1 << enemyLayer) | (1 << playerLayer);

            // 如果指着敌人，强行把终点改成敌人的中心位置！
            if (Physics.Raycast(ray, out hit, 1000f, finalMask))
            {
                // 稍微往相机方向拉一点 (-Vector3.forward)，防止线条穿插进怪物模型里
                endPos = hit.transform.position; 
        
                // 可选：在这里让 TargetingArrow 变色 (比如变成高亮红)
            }

            // 3. 起点修正 (同上一步)
            Vector3 startPos = transform.position;

            // 4. 绘制
            TargetingArrow.Instance.Show(startPos, endPos);
        }
    }

    void OnMouseUp()
    {
        if (squadGroup != null)
        {
            // 检测鼠标下的目标
            BattleTarget target = GetTargetUnderMouse();
            squadGroup.OnSquadDragEnd(target);
        }
        else
        {
            if (!isDragging) return;
            isDragging = false;

            // 隐藏箭头
            TargetingArrow.Instance.Hide();

            // 检测是否命中
            CheckTarget();
        }
    }
    private BattleTarget GetTargetUnderMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        int mask = (1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Player"));
        
        if (Physics.Raycast(ray, out hit, 1000f, mask))
        {
            return hit.collider.GetComponent<BattleTarget>();
        }
        return null;
    }
    // 获取鼠标的世界坐标（保持和骰子同一个深度平面，或者根据射线检测地面）
    private Vector3 GetMouseAsWorldPoint()
    {
        // 射线打到哪里算哪里 (更精确，适合箭头指哪打哪)
        // 我们创建一个虚拟平面在骰子的高度
        Plane boardPlane = new Plane(Vector3.up, transform.position);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float enter;
        if (boardPlane.Raycast(ray, out enter))
        {
            return ray.GetPoint(enter);
        }
        return transform.position; // 兜底
    }

    void CheckTarget()
    {
        BattleTarget target = GetTargetUnderMouse();
        if (target != null)
        {
            Debug.Log("命中目标！");
            
            DiceFaceData data = physicsDice.GetCurrentData();
            
            // 视觉效果：让骰子飞过去撞击
            StartCoroutine(FlyAndHit(target, data));
            return;
        }

        // 如果没打中，什么都不用做，箭头消失了，骰子还在原地// 没打中，归位
        ReturnToTray();
    }
    public IEnumerator FlyAndHit(BattleTarget target, DiceFaceData damageData)
    {
        // 1. 准备阶段
        isDragging = false;
        rb.isKinematic = true;
        GetComponentInChildren<Collider>().enabled = false; // 关闭碰撞，防止半路撞飞

        float duration = 0.4f; // 飞行时间可以稍微加长一点点，让弧线更明显
        float timer = 0f;

        Vector3 startPos = transform.position;
        // 目标位置：建议稍微抬高一点，打在怪物胸口而不是脚底
        Vector3 endPos = target.transform.position + Vector3.up * 0.5f; 

        // --- 核心：计算贝塞尔曲线的控制点 ---
        // 1. 取起点和终点的中点
        Vector3 midPoint = (startPos + endPos) / 2;
        // 2. 往上抬高一定高度 (这里设为距离的 0.5 倍，扔得越远弧度越高)
        float distance = Vector3.Distance(startPos, endPos);
        float arcHeight = distance * 0.5f; 
        // 3. 得到控制点
        Vector3 controlPoint = midPoint + Vector3.up * arcHeight;

        while (timer < duration)
        {
            if (target == null) 
            {
                Destroy(gameObject);
                yield break;
            }

            timer += Time.deltaTime;
            float t = timer / duration; // t 从 0 到 1

            // --- 贝塞尔曲线计算公式 ---
            // 简单的理解：从 p0 到 p1 的插值，和 p1 到 p2 的插值，再取插值
            // B(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
            
            // 这里用两次 Lerp 来模拟贝塞尔 (性能开销极小，更易读)
            Vector3 m1 = Vector3.Lerp(startPos, controlPoint, t);
            Vector3 m2 = Vector3.Lerp(controlPoint, endPos, t);
            transform.position = Vector3.Lerp(m1, m2, t);

            // 旋转效果：让骰子根据飞行进度疯狂旋转
            transform.Rotate(new Vector3(360, 180, 90) * Time.deltaTime * 3f);

            yield return null;
        }

        // 撞击结算
        if (target != null)
        {
            // 获取骰子身上的所有能力
            var abilities = physicsDice.GetAbilities();
            int finalDamage = damageData.value;

            // ---> 触发钩子：OnCalculateDamage <---
            // 让每一个能力都有机会修改伤害（比如暴击、易伤）
            foreach (var ability in abilities)
            {
                finalDamage = ability.OnCalculateDamage(finalDamage, target);
            }

            // 造成最终伤害
            DiceFaceData finalData = damageData; 
            finalData.value = finalDamage; 
            if(target.team == TargetTeam.Enemy)
                target.TakeDamage(finalData);
            else
                target.GainArmor(finalData.value);

            // ---> 触发钩子：OnPostHit <---
            // 造成伤害后，触发吸血、燃烧等效果
            foreach (var ability in abilities)
            {
                ability.OnPostHit(target, finalDamage);
            }
        }
        if (target != null)
        {
            // 1. 造成伤害 (触发 OnHit -> 触发 Ability.OnPostHit)
            target.OnHit(damageData); 

            // 2. 【关键修改】检查是否是幽灵骰，并生成幽灵
            var abilities = physicsDice.GetAbilities();
            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    // 判断当前能力是不是幽灵能力
                    if (ability is Ability_Ghost ghostAbility)
                    {
                        // 获取场景里的骰子管理器
                        DiceThrower thrower = FindObjectOfType<DiceThrower>();
                    
                        // 调用生成方法
                        // 参数1: originalPos (DiceDragger 知道骰子是从哪拿起来的)
                        // 参数2: thrower (刚刚获取的引用)
                        ghostAbility.SpawnGhost(originalPos, thrower); 
                    }
                }
            }
        }
        Destroy(gameObject);
    }

    public void ReturnToTray()
    {
        // 只有当物体还存在时才执行
        if (this == null || gameObject == null) return;

        transform.position = originalPos;
        transform.rotation = originalRot;
        
        if (rb != null)
        {
            rb.isKinematic = false; // 恢复物理让它自然掉落
            rb.velocity = Vector3.zero; // 清空残留速度
        }
        
        isDragging = false;
    }
}