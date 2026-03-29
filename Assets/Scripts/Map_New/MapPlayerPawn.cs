using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class MapPlayerPawn : MonoBehaviour
{[Header("Animation Settings")]
    public float jumpDuration = 0.4f; // 每次跳跃耗时
    public float jumpHeight = 50f;    // 跳跃的弧度高度 (UI像素)

    // 核心跳跃协程
    public IEnumerator MoveAlongPath(List<Vector3> pathWorldPositions, System.Action onComplete)
    {
        // 遍历要跳的每一个格子
        foreach (Vector3 targetPos in pathWorldPositions)
        {
            // 播放弹跳动画，等待跳完再进行下一次循环
            // 参数: 目标位置, 弧度高度, 跳跃次数, 持续时间
            yield return transform.DOLocalJump(targetPos, jumpHeight, 1, jumpDuration).WaitForCompletion();
            
            // 可以加个小小的挤压动画增强 Q弹 的感觉
            transform.DOPunchScale(new Vector3(0.2f, -0.2f, 0), 0.15f);
            
            // 停顿一小下
            yield return new WaitForSeconds(0.1f);
        }

        // 全都跳完了，通知系统
        onComplete?.Invoke();
    }

    // 瞬间移动 (初始化用)
    public void TeleportTo(Vector3 position)
    {
        transform.position = position;
    }
}