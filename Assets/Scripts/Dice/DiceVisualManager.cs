using UnityEngine;
using TMPro; // 如果你用的是TextMeshPro
// using UnityEngine.UI; // 如果你用的是图片

public class DiceVisualManager : MonoBehaviour
{
    // 按顺序拖入那6个子物体：Up, Down, Forward, Back, Right, Left
    // 顺序必须和 PhysicsDice.cs 里的 faceDirections 数组顺序一致！
    public Transform[] faceTransforms; 
    
    // 存储每一面实际代表的数据（不仅仅是显示的数字）
    public DiceFaceData[] faceDatas = new DiceFaceData[6];

    public void InitDice(DiceFaceData[] initialData)
    {
        for (int i = 0; i < 6; i++)
        {
            UpdateFaceVisual(i, initialData[i]);
        }
    }

    // 核心功能：升级/修改某一面的内容
    public void UpdateFaceVisual(int faceIndex, DiceFaceData data)
    {
        faceDatas[faceIndex] = data;
        Transform faceObj = faceTransforms[faceIndex];

        // 假设你的FacePrefab里有一个 TextMeshPro 组件
        TextMeshPro text = faceObj.GetComponentInChildren<TextMeshPro>();
        if(text != null) text.text = data.value.ToString();

        // 假设你还有一个 SpriteRenderer 显示图标（比如剑、盾）
        SpriteRenderer icon = faceObj.GetComponentInChildren<SpriteRenderer>();
        if(icon != null) icon.sprite = data.icon;
        
        // 甚至可以改颜色
        if(text != null) text.color = data.color;
    }

    // 获取当前朝上那一面的数据
    public DiceFaceData GetResultData(int faceIndex)
    {
        return faceDatas[faceIndex];
    }
}

// 定义每一面的数据结构（不仅仅是个数字）
[System.Serializable]
public class DiceFaceData
{
    public int value;        // 数值：1, 2, 6...
    public DiceActionType type; // 类型：物理、魔法、治疗
    public Sprite icon;      // 图标
    public Color color;      // 颜色：红、蓝...
    public string effectDescription; // "造成流血"
}

public enum DiceActionType { Attack, Defend, Magic, Empty }