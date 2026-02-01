using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance;

    [Header("Assets")]
    public GameObject popupPrefab; // 拖入刚才做的 DamagePopup Prefab

    void Awake()
    {
        Instance = this;
    }

    public void ShowDamage(Vector3 position, int amount, bool isChainReaction = false)
    {
        if (popupPrefab == null) return;

        // 生成位置稍微随机一点，防止叠在一起
        Vector3 spawnPos = position + Vector3.up * 1.5f;
        spawnPos.x += Random.Range(-0.5f, 0.5f);

        GameObject popup = Instantiate(popupPrefab, spawnPos, Quaternion.identity);
        
        DamagePopup script = popup.GetComponent<DamagePopup>();
        if (script != null)
        {
            script.Setup(amount, isChainReaction);
        }
    }
}