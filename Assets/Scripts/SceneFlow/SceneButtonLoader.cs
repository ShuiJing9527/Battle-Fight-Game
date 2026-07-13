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
    [SerializeField, Min(0f)] private float demoPlayerDamageMultiplier = 2f;

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

        SceneManager.LoadScene(sceneName);
    }
}
