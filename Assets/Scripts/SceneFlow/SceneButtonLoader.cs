using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonLoader : MonoBehaviour
{
    [Header("Optional Demo Entry")]
    [SerializeField] private bool loadDemoBattleOnStart;
    [SerializeField] private string demoBattleSceneName = BattleSceneResultRouter.BattleSceneName;
    [SerializeField, Min(1f)] private float demoDifficultyLevelInterval = 15f;
    [SerializeField, Min(0f)] private float demoFinalRushStartTime = 90f;
    [SerializeField, Min(0f)] private float demoFinalRushDuration = 30f;
    [SerializeField, Min(0f)] private float demoPlayerDamageMultiplier = 1.25f;
    [Header("Debug")]
    [SerializeField] private bool debugRestartTrace;

    private bool waitingForDemoBattle;

    private void Start()
    {
        if (!loadDemoBattleOnStart)
        {
            return;
        }

        waitingForDemoBattle = true;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleDemoBattleLoaded;
        GameLocalization.MarkFormalGameStart();
        SceneManager.LoadScene(demoBattleSceneName);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleDemoBattleLoaded;
    }

    private void HandleDemoBattleLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!waitingForDemoBattle || scene.name != demoBattleSceneName)
        {
            return;
        }

        waitingForDemoBattle = false;
        EnemyDifficultyDirector director = FindObjectOfType<EnemyDifficultyDirector>();
        if (director != null)
        {
            director.ConfigureTimeline(
                demoDifficultyLevelInterval,
                demoFinalRushStartTime,
                demoFinalRushDuration);
        }
        else
        {
            Debug.LogError("[DemoScene] EnemyDifficultyDirector was not found in the battle scene.", this);
        }

        ApplyDemoPlayerDamageMultiplier(scene);

        SceneManager.sceneLoaded -= HandleDemoBattleLoaded;
        Destroy(gameObject);
    }

    private void ApplyDemoPlayerDamageMultiplier(Scene battleScene)
    {
        CombatStats[] allStats = Resources.FindObjectsOfTypeAll<CombatStats>();
        for (int i = 0; i < allStats.Length; i++)
        {
            CombatStats stats = allStats[i];
            if (stats == null || stats.gameObject.scene != battleScene || !BattleTargetUtility.IsPlayer(stats.gameObject))
            {
                continue;
            }

            stats.outgoingDamageMultiplier = Mathf.Max(0f, demoPlayerDamageMultiplier);
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneButtonLoader] sceneName is empty.", this);
            return;
        }

        bool loadingTitleScene = sceneName == BattleSceneResultRouter.TitleSceneName;
        LogRestartTrace($"Restart requested targetScene={sceneName} loader={name}#{GetInstanceID()} timeScaleBefore={Time.timeScale:F2}");
        Time.timeScale = 1f;
        AudioListener.pause = false;
        LogRestartTrace($"Time.timeScale restored to {Time.timeScale:F2} before loading {sceneName}");
        if (loadingTitleScene)
        {
            GameManager.RequestMainMenuResetOnNextLoad();
        }
        else
        {
            GameLocalization.MarkFormalGameStart();
        }
        LogRestartTrace($"Loading scene={sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    private void LogRestartTrace(string message)
    {
        if (!debugRestartTrace)
        {
            return;
        }

        Debug.Log("[RestartTrace] " + message, this);
    }
}
