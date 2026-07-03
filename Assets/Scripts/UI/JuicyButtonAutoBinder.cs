using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public class JuicyButtonAutoBinder : MonoBehaviour
{
    public static JuicyButtonAutoBinder Instance;

    [Header("Auto Bind")]
    public bool bindInactiveButtons = true;
    public float rescanInterval = 0.5f;

    [Header("Defaults")]
    public float hoverScale = 1.08f;
    public float hoverDuration = 0.16f;
    public float hoverRotation = 3.5f;
    public float pressScale = 0.92f;
    public float pressDuration = 0.08f;
    public float clickPunchScale = 0.14f;
    public float clickPunchDuration = 0.18f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject binderObject = new GameObject("JuicyButtonAutoBinder");
        DontDestroyOnLoad(binderObject);
        Instance = binderObject.AddComponent<JuicyButtonAutoBinder>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindAllButtons();
        StartCoroutine(RescanRoutine());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAllCoroutines();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindAllButtons();
    }

    private IEnumerator RescanRoutine()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(rescanInterval);
            BindAllButtons();
        }
    }

    public void BindAllButtons()
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();

        foreach (Button button in buttons)
        {
            if (button == null) continue;
            if (!button.gameObject.scene.IsValid()) continue;
            if (!bindInactiveButtons && !button.gameObject.activeInHierarchy) continue;
            if (button.GetComponent<JuicyButtonIgnore>() != null) continue;
            if (button.GetComponent<JuicyButtonEffect>() != null) continue;

            JuicyButtonEffect effect = button.gameObject.AddComponent<JuicyButtonEffect>();
            ApplyDefaults(effect);
        }
    }

    private void ApplyDefaults(JuicyButtonEffect effect)
    {
        effect.hoverScale = hoverScale;
        effect.hoverDuration = hoverDuration;
        effect.hoverRotation = hoverRotation;
        effect.pressScale = pressScale;
        effect.pressDuration = pressDuration;
        effect.clickPunchScale = clickPunchScale;
        effect.clickPunchDuration = clickPunchDuration;
    }
}
