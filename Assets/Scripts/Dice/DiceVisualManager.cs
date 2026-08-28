using UnityEngine;
using TMPro;

public class DiceVisualManager : MonoBehaviour
{
    private static readonly int HandDrawSettledId = Shader.PropertyToID("_HandDrawSettled");

    [Header("Face Texture")]
    [Tooltip("支持缩进显示的骰面材质。")]
    public Material faceTextureMaterial;
    [Tooltip("骰面贴图占完整平面的比例；越小，露出的圆角骰子本体越多。")]
    [Range(0.5f, 1f)] public float faceTextureScale = 0.95f;

    // 按顺序拖入那6个子物体：Up, Down, Forward, Back, Right, Left
    // 顺序必须和 PhysicsDice.cs 里的 faceDirections 数组顺序一致！
    public Transform[] faceTransforms; 
    
    // 存储每一面实际代表的数据（不仅仅是显示的数字）
    public DiceFaceData[] faceDatas = new DiceFaceData[6];

    public void InitDice(DiceFaceData[] initialData)
    {
        for (int i = 0; i < 6; i++)
        {
            // 必须创建一个新的对象进行深拷贝！
            DiceFaceData source = initialData[i];
            
            DiceFaceData newData = new DiceFaceData();
            newData.value = source.value;
            newData.icon = source.icon;
            newData.color = source.color;
            newData.effectDescription = source.effectDescription;
            newData.bonusValue = source.bonusValue; // 这里拷贝过来的是当时的初始值

            // 将新对象存入数组
            faceDatas[i] = newData;
            UpdateFaceVisual(i, initialData[i]);
        }
    }

    public void SetFaceTextureMaterial(Material material)
    {
        if (material == null) return;

        faceTextureMaterial = material;
        foreach (Transform face in faceTransforms)
        {
            Renderer faceRenderer = face != null ? face.GetComponent<Renderer>() : null;
            if (faceRenderer != null)
                faceRenderer.sharedMaterial = material;
        }
    }

    public void SetHandDrawSettled(bool settled)
    {
        float value = settled ? 1f : 0f;
        foreach (Transform face in faceTransforms)
        {
            Renderer faceRenderer = face != null ? face.GetComponent<Renderer>() : null;
            if (faceRenderer == null) continue;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            faceRenderer.GetPropertyBlock(properties);
            properties.SetFloat(HandDrawSettledId, value);
            faceRenderer.SetPropertyBlock(properties);
        }
    }

    // 核心功能：升级/修改某一面的内容
    public void UpdateFaceVisual(int faceIndex, DiceFaceData data)
    {
        // faceDatas[faceIndex] = data;
        Transform faceObj = faceTransforms[faceIndex];

        TextMeshPro text = faceObj.GetComponentInChildren<TextMeshPro>(true);
        bool usesFaceTexture = data.icon != null;
        if (text != null)
        {
            text.gameObject.SetActive(!usesFaceTexture);

            // --- 修改显示逻辑 ---
            if (!usesFaceTexture && data.bonusValue != 0)
            {
                string sign = data.bonusValue > 0 ? "+" : "";
                string color = data.bonusValue > 0 ? "#00FF00" : "#FF5555";
                // 方案 A: 显示总数，但用绿色表示有加成
                // text.text = data.TotalValue.ToString();
                // text.color = Color.green;

                // 方案 B: 显示 "基础+加成" (推荐，更直观)
                text.text = $"{data.value}<size=60%><color={color}>{sign}{data.bonusValue}</color></size>";
                
                // 方案 C: 仅显示总数 (如果你觉得骰子上字太多看不清)
                // text.text = data.TotalValue.ToString();
                // 可以在这里改变材质发光或者字体颜色来提示玩家
            }
            else if (!usesFaceTexture)
            {
                text.text = data.value.ToString();
                // text.color = Color.white; // 记得重置颜色
            }
        }

        // 六个面本身已经是贴合骰子的平面，直接替换纹理即可，无需额外创建 Sprite 子物体。
        Renderer faceRenderer = faceObj.GetComponent<Renderer>();
        if (faceRenderer != null)
        {
            faceRenderer.enabled = usesFaceTexture;
            if (usesFaceTexture)
            {
                if (faceTextureMaterial != null)
                    faceRenderer.sharedMaterial = faceTextureMaterial;

                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                faceRenderer.GetPropertyBlock(properties);
                properties.SetTexture("_MainTex", data.icon.texture);
                properties.SetFloat("_FaceScale", faceTextureScale);
                faceRenderer.SetPropertyBlock(properties);
            }
            else
            {
                faceRenderer.SetPropertyBlock(null);
            }
        }
        
        // 甚至可以改颜色
        if(text != null && !usesFaceTexture) text.color = data.color;
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
    public Sprite icon;      // 图标
    public Color color;      // 颜色：红、蓝...
    public string effectDescription; // "造成流血"
    
    public int bonusValue;   // 法术和战斗流程施加的临时点数修正
    
    // 【新增】获取总值的快捷属性
    public int TotalValue => value + bonusValue; 
}
