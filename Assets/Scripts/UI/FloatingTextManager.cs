using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance;

    [Header("预制体")]
    public GameObject floatingTextPrefab; // 拖入做好的跳字预制体

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 在指定的世界坐标生成飘字
    /// </summary>
    public void ShowText(Vector3 position, string text, Color color)
    {
        if (floatingTextPrefab == null) return;
        
        // 生成在传入的位置 (通常是棋子的位置)，并设为 Canvas_Global 的子物体
        GameObject txtObj = Instantiate(floatingTextPrefab, position, Quaternion.identity, transform);
        
        FloatingTextUI ft = txtObj.GetComponent<FloatingTextUI>();
        if (ft != null)
        {
            ft.Play(text, color);
        }
    }
}