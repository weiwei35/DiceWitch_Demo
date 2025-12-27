using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWave", menuName = "Game/Wave Data")]
public class WaveDataSO : ScriptableObject
{
    // 这一波包含的怪物预制体列表
    public List<GameObject> enemyPrefabs;
}