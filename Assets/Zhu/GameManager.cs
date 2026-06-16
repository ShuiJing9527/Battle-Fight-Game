using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GameObject settingsPanel;
    public SettingsData settings = new SettingsData();

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

    void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void ToggleSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

[System.Serializable]
public class SettingsData
{
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool fullscreen = false;
}