using UnityEngine;

// 这是一个抽象基类，所有具体房间都继承它
public abstract class RoomDataSO : ScriptableObject
{
    public string roomName;
    public Enum.RoomType roomType;
    public Sprite roomIcon; // 地图上显示的图标
    [TextArea] public string description; // 鼠标悬停时的描述
}