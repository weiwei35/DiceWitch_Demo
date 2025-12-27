using UnityEngine;
using System.Collections;

public class SplittingProjectile : MonoBehaviour
{
    private BattleTarget _target;
    private int _damage;
    private Ability_Split _abilitySource; // 引用源能力，用于递归

    // 初始化方法
    public void Setup(Vector3 startPos, BattleTarget target, int damage, Ability_Split source)
    {
        transform.position = startPos;
        _target = target;
        _damage = damage;
        _abilitySource = source;

        // 开始飞行
        StartCoroutine(FlyRoutine());
    }

    IEnumerator FlyRoutine()
    {
        float duration = 0.3f; // 飞行时间
        float time = 0;
        Vector3 startPos = transform.position;

        // 贝塞尔曲线控制点（稍微抬高一点，形成抛物线）
        Vector3 midPoint = (startPos + _target.transform.position) / 2;
        Vector3 controlPoint = midPoint + Vector3.up * 2.0f;

        while (time < duration)
        {
            if (_target == null) 
            {
                Destroy(gameObject);
                yield break;
            }

            time += Time.deltaTime;
            float t = time / duration;

            // 贝塞尔曲线位移
            Vector3 m1 = Vector3.Lerp(startPos, controlPoint, t);
            Vector3 m2 = Vector3.Lerp(controlPoint, _target.transform.position, t);
            transform.position = Vector3.Lerp(m1, m2, t);

            // 疯狂旋转
            transform.Rotate(Vector3.one * 360 * Time.deltaTime * 5);

            yield return null;
        }

        // --- 撞击结算 ---
        if (_target != null)
        {
            // 1. 造成伤害 (直接应用，因为这是分裂出来的额外伤害)
            _target.ApplyDirectValue(_damage);
            
            // 播放特效...
            
            // 2. 【关键】尝试再次分裂 (递归)
            // 调用 Ability 里的逻辑来判断是否继续生孩子
            _abilitySource.TrySpawnNextSplit(transform.position, _target, _damage);
        }

        Destroy(gameObject);
    }
}