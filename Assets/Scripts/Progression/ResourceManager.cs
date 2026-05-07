using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;

    public int manaDust = 0;

    public event System.Action OnResourceChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddManaDust(int amount)
    {
        manaDust += amount;
        OnResourceChanged?.Invoke();
        Debug.Log($"获得资源: {amount}, 当前: {manaDust}");
    }

    public bool TrySpendManaDust(int amount)
    {
        if (manaDust >= amount)
        {
            manaDust -= amount;
            OnResourceChanged?.Invoke();
            return true;
        }
        return false;
    }
}
