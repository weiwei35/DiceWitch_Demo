using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDiceData", menuName = "Game/Dice Data")]
public class DiceDataSO : ScriptableObject
{
    public string diceName = "基础攻击骰";
    public Color bodyColor = Color.white; // 甚至可以定义骰子本体颜色
    
    // 关键：定义6个面的数据（必须是6个）
    // 这里的 DiceFaceData 就是我们之前定义的那个类
    public DiceFaceData[] faces = new DiceFaceData[6]; 
    
    // ---> 这个骰子附带的特殊能力列表 <---
    public List<DiceAbilitySO> abilities = new List<DiceAbilitySO>();
}