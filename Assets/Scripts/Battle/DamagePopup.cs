using UnityEngine;
using TMPro;
using DG.Tweening;

public class DamagePopup : MonoBehaviour
{
    public TextMeshPro textMesh;
    
    // 颜色配置
    public Color normalColor = Color.white;
    public Color chainColor = Color.yellow; // 链枷/AOE伤害的颜色

    public void Setup(int damageAmount, bool isChainReaction)
    {
        if (damageAmount <= 0)
        {
            textMesh.text = "无效";
            textMesh.color = Color.gray; // 灰色代表没打动
        }
        else
        {
            textMesh.text = damageAmount.ToString();
                    
             // 1. 设置颜色
             if (isChainReaction)
             {
                 textMesh.color = chainColor;
                 textMesh.fontSize *= 0.8f; // 连锁伤害稍微小一点
             }
             else
             {
                 textMesh.color = normalColor;
             }
        }
        

        // 2. 动画效果 (Juice!)
        
        // 初始状态：稍微缩小
        transform.localScale = Vector3.one * 0.5f;
        
        // 序列动画
        Sequence seq = DOTween.Sequence();

        // A. 瞬间变大 (弹跳感)
        seq.Append(transform.DOScale(1.2f, 0.1f).SetEase(Ease.OutBack));
        // B. 恢复正常大小
        seq.Append(transform.DOScale(1.0f, 0.1f));
        // C. 慢慢飘起 (同时)
        seq.Insert(0f, transform.DOMoveY(transform.position.y + 2f, 0.8f).SetEase(Ease.OutQuad));
        // D. 最后淡出
        seq.Insert(0.4f, textMesh.DOFade(0, 0.4f));

        // 3. 销毁
        seq.OnComplete(() => Destroy(gameObject));
    }

    void Update()
    {
        // 始终朝向摄像机 (Billboard)
        if (Camera.main != null)
        {
            // transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}