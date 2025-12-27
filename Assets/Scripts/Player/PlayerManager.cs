using System.Collections;
using DG.Tweening;
using UnityEngine;
using TMPro;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance; // 单例方便调用

    public int maxHp = 50;
    public int currentHp;
    public int currentArmor;

    public TextMeshPro hpText; 
    public TextMeshPro armorText;

    void Awake()
    {
        Instance = this;
        currentHp = maxHp;
    }

    void Start()
    {
        UpdateUI();
    }

    // 受伤逻辑：先扣护甲，再扣血
    public void TakeDamage(int dmg)
    {
        int damageToHp = dmg;
        
        if (currentArmor > 0)
        {
            if (currentArmor >= dmg)
            {
                currentArmor -= dmg;
                damageToHp = 0;
            }
            else
            {
                damageToHp = dmg - currentArmor;
                currentArmor = 0;
            }
        }

        currentHp -= damageToHp;
        Debug.Log($"玩家受到 {dmg} 点伤害 (护甲抵挡后实际扣血: {damageToHp})");
        
        // 简单的屏幕震动反馈（可选）
        transform.DOShakePosition(0.5f, 0.5f);

        UpdateUI();
    }

    public IEnumerator AttackAnim()
    {
        // 播放攻击动画（这里用简单的位移模拟）
        Vector3 originalPos = transform.position;
        Vector3 targetPos = transform.position + Vector3.forward * 1.1f; // 往前冲一点

        // 冲出去
        float t = 0;
        while(t < 0.1f) { transform.position = Vector3.Lerp(originalPos, targetPos, t/0.1f); t+=Time.deltaTime; yield return null; }

        // 回来
        t = 0;
        while(t < 0.2f) { transform.position = Vector3.Lerp(targetPos, originalPos, t/0.2f); t+=Time.deltaTime; yield return null; }
    }

    public void AddArmor(int amount)
    {
        currentArmor += amount;
        UpdateUI();
    }

    public void ResetArmor()
    {
        currentArmor = 0;
        UpdateUI();
    }

    void UpdateUI()
    {
        if(hpText) hpText.text = $"HP: {currentHp}/{maxHp}";
        if(armorText) armorText.text = $"Armor: {currentArmor}";
    }

    public void Heal(int healAmount)
    {
        currentHp += healAmount;
        if(currentHp > maxHp) currentHp = maxHp;
        UpdateUI();
    }
}