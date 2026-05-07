using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bootstrap: ensures the split progression managers are attached to the persistent GameObject.
/// Keeps Inspector references for ability/attribute libraries and forwards them to MagicCircleManager.
/// All progression logic has been moved to ResourceManager and MagicCircleManager.
/// </summary>
public class PlayerProgressionManager : MonoBehaviour
{
    public static PlayerProgressionManager Instance;

    [Header("Library References (forwarded to MagicCircleManager)")]
    public List<SlotAttributeSO> allAttributesLibrary_Slot = new List<SlotAttributeSO>();
    public List<DiceAbilitySO> allAttributesLibrary_Dice = new List<DiceAbilitySO>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (GetComponent<ResourceManager>() == null)
                gameObject.AddComponent<ResourceManager>();

            MagicCircleManager mcm = GetComponent<MagicCircleManager>();
            if (mcm == null)
                mcm = gameObject.AddComponent<MagicCircleManager>();

            mcm.allAttributesLibrary_Slot = allAttributesLibrary_Slot;
            mcm.allAttributesLibrary_Dice = allAttributesLibrary_Dice;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
