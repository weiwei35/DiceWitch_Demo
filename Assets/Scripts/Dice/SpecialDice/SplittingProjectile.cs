// SplittingProjectile.cs (优化版)
using UnityEngine;
using System.Collections;

public class SplittingProjectile : MonoBehaviour
{
    private BattleTarget _target;
    private int _damage;
    private Ability_Split _abilitySource;

    // 添加一个拖尾特效引用，防止销毁时特效断掉
    [SerializeField] private TrailRenderer _trail; 

    public void Setup(Vector3 startPos, BattleTarget target, int damage, Ability_Split source)
    {
        // 【关键】强制设置层级，确保在主相机中可见，而不是在骰子盘的层级
        SetLayerRecursively(gameObject, LayerMask.NameToLayer("Default")); // 或者你主场景的 Layer

        transform.position = startPos;
        _target = target;
        _damage = damage;
        _abilitySource = source;

        StopAllCoroutines();
        StartCoroutine(FlyRoutine());
    }

    IEnumerator FlyRoutine()
    {
        float duration = 0.25f; // 飞行速度快一点，更有打击感
        float time = 0;
        Vector3 startPos = transform.position;
        
        // 计算目标位置（加一点高度偏移，攻击胸口）
        Vector3 endPos = _target.transform.position + Vector3.up * 0.5f;

        // 贝塞尔曲线控制点：取中点，然后向上抬，形成抛物线
        Vector3 midPoint = (startPos + endPos) / 2;
        Vector3 controlPoint = midPoint + Vector3.up * 2.0f; // 抬高弧度

        while (time < duration)
        {
            if (_target == null) { Destroy(gameObject); yield break; }

            time += Time.deltaTime;
            float t = time / duration;
            
            // 二阶贝塞尔曲线公式
            Vector3 m1 = Vector3.Lerp(startPos, controlPoint, t);
            Vector3 m2 = Vector3.Lerp(controlPoint, endPos, t);
            transform.position = Vector3.Lerp(m1, m2, t);

            // 让骰子疯狂旋转
            transform.Rotate(Vector3.right * 720 * Time.deltaTime);
            transform.Rotate(Vector3.up * 360 * Time.deltaTime);

            yield return null;
        }

        // --- 撞击 ---
        if (_target != null)
        {
            // 造成伤害
            _target.ApplyDirectValue(_damage);
            
            // 播放撞击特效（可选）
            // Instantiate(HitVFX, transform.position, Quaternion.identity);

            // 【递归】把“自己”传回去，继续飞向下一个
            _abilitySource.TrySpawnNextSplit(transform.position, _target, _damage, this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, newLayer);
    }
}