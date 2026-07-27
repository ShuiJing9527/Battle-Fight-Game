using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private const string MainMenuSceneName = "GameScene";

    public static GameManager Instance;
    public GameObject settingsPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject languagePanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject defaultSelectedObject;
    public SettingsData settings = new SettingsData();

    private static bool resetMainMenuOnNextLoad;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            EnsureLocalizationService();
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EnsureLocalizationService()
    {
        if (GetComponent<GameLocalization>() == null)
            gameObject.AddComponent<GameLocalization>();
    }

    void Start()
    {
        if (IsMainMenuScene(SceneManager.GetActiveScene()))
        {
            ResetToMainMenu();
        }
        else if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    public void ToggleSettings()
    {
        BindMenuSceneReferences(SceneManager.GetActiveScene());

        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public static void RequestMainMenuResetOnNextLoad()
    {
        resetMainMenuOnNextLoad = true;
    }

    public void ResetToMainMenu()
    {
        BindMenuSceneReferences(SceneManager.GetActiveScene());

        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(languagePanel, false);
        SetPanelActive(creditsPanel, false);
        SelectDefaultMenuObject();

        resetMainMenuOnNextLoad = false;
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsMainMenuScene(scene))
        {
            return;
        }

        BindMenuSceneReferences(scene);
        if (resetMainMenuOnNextLoad || settingsPanel != null)
        {
            ResetToMainMenu();
        }
    }

    private void BindMenuSceneReferences(Scene scene)
    {
        if (!IsMainMenuScene(scene))
        {
            return;
        }

        settingsPanel = FindSceneObjectByName(scene, "Settingpanel", "SettingsPanel", "SettingPanel") ?? settingsPanel;
        mainMenuPanel = FindSceneObjectByName(scene, "MainMenuPanel", "Main Menu Panel", "MenuPanel") ?? mainMenuPanel;
        languagePanel = FindSceneObjectByName(scene, "LanguagePanel", "Language Panel") ?? languagePanel;
        creditsPanel = FindSceneObjectByName(scene, "CreditsPanel", "Credits Panel") ?? creditsPanel;

        if (defaultSelectedObject == null || defaultSelectedObject.scene != scene)
        {
            defaultSelectedObject = FindSceneObjectByName(scene, "Start", "StartButton");
        }
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActive(active);

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }
    }

    private void SelectDefaultMenuObject()
    {
        if (EventSystem.current == null || defaultSelectedObject == null || !defaultSelectedObject.activeInHierarchy)
        {
            return;
        }

        Selectable selectable = defaultSelectedObject.GetComponent<Selectable>();
        if (selectable == null || !selectable.IsInteractable())
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(defaultSelectedObject);
    }

    private static bool IsMainMenuScene(Scene scene)
    {
        return scene.IsValid() && scene.name == MainMenuSceneName;
    }

    private static GameObject FindSceneObjectByName(Scene scene, params string[] names)
    {
        if (!scene.IsValid() || !scene.isLoaded || names == null || names.Length == 0)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            if (NameMatches(root.name, names))
            {
                return root;
            }

            Transform child = FindChildByName(root.transform, names);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform parent, string[] names)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (NameMatches(child.name, names))
            {
                return child;
            }

            Transform nested = FindChildByName(child, names);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool NameMatches(string candidate, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (candidate == names[i])
            {
                return true;
            }
        }

        return false;
    }
}

[System.Serializable]
public class SettingsData
{
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool fullscreen = false;
}
