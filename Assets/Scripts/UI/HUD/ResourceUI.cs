using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
    public TextMeshProUGUI amountText;

    void Start()
    {
        UpdateUI();
        ResourceManager.Instance.OnResourceChanged += UpdateUI;
    }

    void OnDestroy()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= UpdateUI;
    }

    void UpdateUI()
    {
        amountText.text = ResourceManager.Instance.manaDust.ToString();
    }
}