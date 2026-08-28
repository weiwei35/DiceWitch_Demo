using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingTextUI : MonoBehaviour
{
    public TextMeshProUGUI textMesh;

    public void Play(string text, Color color)
    {
        textMesh.text = text;
        textMesh.color = color;

        // 1. 向上漂移 50 个单位 (SetRelative(true) 代表相对于当前位置)
        transform.DOMoveY(1f, 1f).SetRelative(true).SetEase(Ease.OutCubic);
        
        // 2. 0.5秒后开始渐隐，完成后自动销毁这个物体
        textMesh.DOFade(0, 1f).SetDelay(0.5f).OnComplete(() => Destroy(gameObject));
    }
}