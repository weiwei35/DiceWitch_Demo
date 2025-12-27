using UnityEngine;
using DG.Tweening; // 用来做材质渐变动画

[RequireComponent(typeof(PhysicsDice))]
[RequireComponent(typeof(DiceDragger))]
public class GhostDiceBehavior : MonoBehaviour
{
    private PhysicsDice _physicsDice;
    private DiceDragger _dragger;
    private Collider _collider;
    private Rigidbody _rb;
    
    // 原始材质颜色，用于恢复
    private Color _originalColor;

    void Awake()
    {
        _physicsDice = GetComponent<PhysicsDice>();
        _dragger = GetComponent<DiceDragger>();
        _collider = GetComponentInChildren<Collider>();
        _rb = GetComponent<Rigidbody>();
    }

    // 初始化：进入“幽灵模式”
    public void EnterGhostMode(Material ghostMat)
    {
        // 1. 禁用交互
        _dragger.enabled = false; // 禁止拖动
        _physicsDice.isRolling = true; // 伪装成正在滚动，防止被误操作
        _collider.enabled = false; // 关闭碰撞，防止被物理干扰
        if (_rb != null)
        {
            _rb.isKinematic = true; 
            // _rb.velocity = Vector3.zero; // 清空残留速度
            // _rb.angularVelocity = Vector3.zero;
        }
        // 2. 视觉变化：变半透明
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            _originalColor = rend.material.color;
            // 替换成幽灵材质，或者直接改 Alpha
            if (ghostMat != null)
            {
                rend.material = ghostMat;
            }
            else
            {
                // 简单的 Alpha 变半透明
                Color ghostColor = _originalColor;
                ghostColor.a = 0.3f;
                rend.material.color = ghostColor;
                
                // 注意：Unity标准材质需要设为 Transparent 模式才能改 Alpha
                // 如果是 Opaque 模式，改 Alpha 没用。建议直接用 ghostMat 替换。
            }
        }

        // 3. 订阅死亡事件
        BattleManager.Instance.OnEnemyKilledEvent += OnEnemyDied;
    }

    void OnDestroy()
    {
        // 重要：销毁时必须取消订阅，否则会报错
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnEnemyKilledEvent -= OnEnemyDied;
        }
    }

    // 当听到敌人死亡时
    void OnEnemyDied()
    {
        Debug.Log("幽灵骰复活！");
        Revive();
    }

    void Revive()
    {
        // 1. 取消订阅 (防止一次复活多次)
        BattleManager.Instance.OnEnemyKilledEvent -= OnEnemyDied;

        // 2. 恢复交互
        _dragger.enabled = true;
        _physicsDice.isRolling = false; // 解除锁定
        _collider.enabled = true;

        // 3. 恢复视觉
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            // 变回原来的颜色/材质
            // 简单点：直接重置颜色，或者恢复原本的材质
            rend.material.color = _originalColor; 
            rend.material.DOFade(1.0f, 0.5f); // 渐变回实体
        }

        // 4. 执行重投逻辑！
        // 给一个向上的力，跳起来重投// 【核心修复】复活时恢复物理
        if (_collider != null) _collider.enabled = true; // 先开腿
        if (_rb != null) _rb.isKinematic = false;        // 再开重力
        
        Vector3 force = Vector3.up * 4f + Random.insideUnitSphere * 1f;
        Vector3 torque = Random.insideUnitSphere * 10f;
        _physicsDice.Roll(force, torque);

        // 5. 销毁这个 Ghost 脚本，因为它已经变回真骰子了
        Destroy(this);
    }
}