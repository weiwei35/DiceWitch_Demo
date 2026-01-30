using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
    public TextMeshProUGUI amountText;

    void Start()
    {
        UpdateUI();
        PlayerProgressionManager.Instance.OnResourceChanged += UpdateUI;
    }

    void OnDestroy()
    {
        if (PlayerProgressionManager.Instance != null)
            PlayerProgressionManager.Instance.OnResourceChanged -= UpdateUI;
    }

    void UpdateUI()
    {
        amountText.text = PlayerProgressionManager.Instance.manaDust.ToString();
    }
}