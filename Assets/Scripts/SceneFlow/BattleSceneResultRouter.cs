using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class BattleSceneResultRouter : MonoBehaviour
{
    public const string BattleSceneName = "\u8349\u539F";
    public const string TitleSceneName = "GameScene";
    public const string GameWinSceneName = "gamewin";
    public const string GameOverSceneName = "gameover";

    [SerializeField] private CombatHealth player01Health;
    [SerializeField] private CombatHealth player02Health;
    [SerializeField] private EnemyDifficultyDirector difficultyDirector;
    [SerializeField] private EnemySpawner enemySpawner;
    [Header("Debug")]
    [SerializeField] private bool debugRestartTrace;
    [SerializeField, Min(0.1f)] private float bindingRetryInterval = 0.33f;

    private bool resultTriggered;
    private bool subscribedPlayer01;
    private bool subscribedPlayer02;
    private bool subscribedVictory;
    private float nextBindingRetryTime;
    private static bool sceneLoadedHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        sceneLoadedHookRegistered = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHook()
    {
        if (sceneLoadedHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        sceneLoadedHookRegistered = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForActiveBattleScene()
    {
        EnsureRouterForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRouterForScene(scene);
    }

    private static void EnsureRouterForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !string.Equals(scene.name, BattleSceneName, StringComparison.Ordinal))
        {
            return;
        }

        if (FindSceneComponent<BattleSceneResultRouter>(scene) != null)
        {
            return;
        }

        GameObject routerObject = new GameObject(nameof(BattleSceneResultRouter));
        SceneManager.MoveGameObjectToScene(routerObject, scene);
        routerObject.AddComponent<BattleSceneResultRouter>();
    }

    private void Awake()
    {
        LogRestartTrace($"BattleSceneResultRouter Awake scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)} resultTriggered={resultTriggered}");
        ResetForNewBattle();
    }

    private void OnEnable()
    {
        LogRestartTrace($"BattleSceneResultRouter OnEnable scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)} resultTriggered={resultTriggered}");
        RefreshBindings(forceRebind: false);
        SubscribeEvents();
    }

    private void Update()
    {
        if (resultTriggered)
        {
            return;
        }

        RefreshBindings(forceRebind: false);
        SubscribeEvents();

        if (difficultyDirector != null && difficultyDirector.CurrentPhase == DifficultyPhase.Victory)
        {
            TriggerGameWin();
            return;
        }

        if (IsAnyPlayerDead())
        {
            TriggerGameOver();
        }
    }

    private void OnDisable()
    {
        LogRestartTrace($"BattleSceneResultRouter OnDisable scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)}");
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        LogRestartTrace($"BattleSceneResultRouter OnDestroy scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)}");
        UnsubscribeEvents();
    }

    private void ResetForNewBattle()
    {
        resultTriggered = false;
        UnsubscribeEvents();
        player01Health = null;
        player02Health = null;
        difficultyDirector = null;
        enemySpawner = null;
        nextBindingRetryTime = 0f;
        LogRestartTrace($"Router reset for new battle scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)}");
        RefreshBindings(forceRebind: true);
        SubscribeEvents();
    }

    private void RefreshBindings(bool forceRebind)
    {
        bool missingBinding =
            player01Health == null ||
            player02Health == null ||
            difficultyDirector == null ||
            enemySpawner == null;

        if (!forceRebind && !missingBinding)
        {
            return;
        }

        if (!forceRebind && Time.unscaledTime < nextBindingRetryTime)
        {
            return;
        }

        nextBindingRetryTime = Time.unscaledTime + Mathf.Max(0.1f, bindingRetryInterval);

        CombatHealth resolvedPlayer01 = FindPlayerHealth("Player01", typeof(Player01SkillController), gameObject.scene);
        CombatHealth resolvedPlayer02 = FindPlayerHealth("Player02", typeof(Player2PrototypeController), gameObject.scene);
        EnemyDifficultyDirector resolvedDirector = FindSceneComponent<EnemyDifficultyDirector>(gameObject.scene);
        EnemySpawner resolvedSpawner = FindSceneComponent<EnemySpawner>(gameObject.scene);

        if (forceRebind || player01Health != resolvedPlayer01 || player02Health != resolvedPlayer02 || difficultyDirector != resolvedDirector || enemySpawner != resolvedSpawner)
        {
            UnsubscribeEvents();
            player01Health = resolvedPlayer01;
            player02Health = resolvedPlayer02;
            difficultyDirector = resolvedDirector;
            enemySpawner = resolvedSpawner;
            LogRestartTrace(
                $"Router bound scene={gameObject.scene.name} player01={GetObjectDebugLabel(player01Health)} player02={GetObjectDebugLabel(player02Health)} difficultyDirector={GetObjectDebugLabel(difficultyDirector)} enemySpawner={GetObjectDebugLabel(enemySpawner)}");
        }
    }

    private void SubscribeEvents()
    {
        if (player01Health != null && !subscribedPlayer01)
        {
            player01Health.Died += HandlePlayerDeath;
            subscribedPlayer01 = true;
        }

        if (player02Health != null && !subscribedPlayer02)
        {
            player02Health.Died += HandlePlayerDeath;
            subscribedPlayer02 = true;
        }

        if (difficultyDirector != null && !subscribedVictory)
        {
            difficultyDirector.OnVictory += HandleVictory;
            subscribedVictory = true;
        }
    }

    private void UnsubscribeEvents()
    {
        if (player01Health != null && subscribedPlayer01)
        {
            player01Health.Died -= HandlePlayerDeath;
            LogRestartTrace($"Router unbound old player01={GetObjectDebugLabel(player01Health)}");
        }

        if (player02Health != null && subscribedPlayer02)
        {
            player02Health.Died -= HandlePlayerDeath;
            LogRestartTrace($"Router unbound old player02={GetObjectDebugLabel(player02Health)}");
        }

        if (difficultyDirector != null && subscribedVictory)
        {
            difficultyDirector.OnVictory -= HandleVictory;
            LogRestartTrace($"Router unbound old difficultyDirector={GetObjectDebugLabel(difficultyDirector)}");
        }

        subscribedPlayer01 = false;
        subscribedPlayer02 = false;
        subscribedVictory = false;
    }

    private void HandlePlayerDeath(GameObject killer)
    {
        if (resultTriggered)
        {
            return;
        }

        LogPlayerDeathTrace($"Death callback received from {ResolveDeadPlayerLabel()}");

        if (difficultyDirector != null && difficultyDirector.CurrentPhase == DifficultyPhase.Victory)
        {
            TriggerGameWin();
            return;
        }

        if (IsAnyPlayerDead())
        {
            LogPlayerDeathTrace("GameOver requested");
            TriggerGameOver();
        }
    }

    private void HandleVictory()
    {
        TriggerGameWin();
    }

    private void TriggerGameOver()
    {
        if (resultTriggered)
        {
            return;
        }

        resultTriggered = true;
        FreezeBattleBeforeResultScene();
        AudioManager.Instance?.PlayGameOverBgm();
        LogPlayerDeathTrace("GameOver entered");
        SceneManager.LoadScene(GameOverSceneName);
    }

    private void TriggerGameWin()
    {
        if (resultTriggered)
        {
            return;
        }

        resultTriggered = true;
        FreezeBattleBeforeResultScene();
        AudioManager.Instance?.PlayVictoryBgm();
        SceneManager.LoadScene(GameWinSceneName);
    }

    private static bool IsPlayerDead(CombatHealth health)
    {
        return health != null && health.IsDead;
    }

    private bool IsAnyPlayerDead()
    {
        return IsPlayerDead(player01Health) || IsPlayerDead(player02Health);
    }

    private static CombatHealth FindPlayerHealth(string objectName, Type controllerType, Scene targetScene)
    {
        CombatHealth[] allHealth = FindSceneComponents<CombatHealth>(targetScene);
        for (int i = 0; i < allHealth.Length; i++)
        {
            CombatHealth candidate = allHealth[i];
            if (!IsCandidateInScene(candidate, targetScene))
            {
                continue;
            }

            if (string.Equals(candidate.gameObject.name, objectName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        for (int i = 0; i < allHealth.Length; i++)
        {
            CombatHealth candidate = allHealth[i];
            if (!IsCandidateInScene(candidate, targetScene))
            {
                continue;
            }

            if (controllerType != null && candidate.GetComponent(controllerType) != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void FreezeBattleBeforeResultScene()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (enemySpawner == null)
        {
            enemySpawner = FindSceneComponent<EnemySpawner>(gameObject.scene);
        }

        if (enemySpawner != null)
        {
            enemySpawner.PauseSpawningForExternalTest();
            enemySpawner.enabled = false;
        }

        if (difficultyDirector != null)
        {
            difficultyDirector.enabled = false;
        }
    }

    private static bool IsCandidateInScene(CombatHealth candidate, Scene targetScene)
    {
        if (candidate == null || candidate.gameObject == null)
        {
            return false;
        }

        Scene candidateScene = candidate.gameObject.scene;
        return candidateScene.IsValid() && candidateScene.isLoaded && candidateScene == targetScene;
    }

    private static T FindSceneComponent<T>(Scene targetScene) where T : Component
    {
        T[] all = FindSceneComponents<T>(targetScene);
        return all.Length > 0 ? all[0] : null;
    }

    private static T[] FindSceneComponents<T>(Scene targetScene) where T : Component
    {
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            return Array.Empty<T>();
        }

        GameObject[] roots = targetScene.GetRootGameObjects();
        List<T> results = new List<T>();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            T[] components = root.GetComponentsInChildren<T>(true);
            for (int j = 0; j < components.Length; j++)
            {
                if (components[j] != null)
                {
                    results.Add(components[j]);
                }
            }
        }

        return results.ToArray();
    }

    private string ResolveDeadPlayerLabel()
    {
        if (IsPlayerDead(player01Health))
        {
            return GetObjectDebugLabel(player01Health);
        }

        if (IsPlayerDead(player02Health))
        {
            return GetObjectDebugLabel(player02Health);
        }

        return "unknown";
    }

    private static string GetObjectDebugLabel(UnityEngine.Object target)
    {
        if (target == null)
        {
            return "null";
        }

        if (target is Component component)
        {
            return $"{component.name}#{component.GetInstanceID()}";
        }

        if (target is GameObject gameObject)
        {
            return $"{gameObject.name}#{gameObject.GetInstanceID()}";
        }

        return $"{target.name}#{target.GetInstanceID()}";
    }

    private void LogRestartTrace(string message)
    {
        if (!debugRestartTrace)
        {
            return;
        }

        Debug.Log("[RestartTrace] " + message, this);
    }

    private void LogPlayerDeathTrace(string message)
    {
        if (!debugRestartTrace)
        {
            return;
        }

        Debug.Log("[PlayerDeathTrace] " + message, this);
    }
}
