using System;
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

    private bool resultTriggered;
    private bool subscribedPlayer01;
    private bool subscribedPlayer02;
    private bool subscribedVictory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForBattleScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, BattleSceneName, StringComparison.Ordinal))
        {
            return;
        }

        if (FindObjectOfType<BattleSceneResultRouter>() != null)
        {
            return;
        }

        GameObject routerObject = new GameObject(nameof(BattleSceneResultRouter));
        SceneManager.MoveGameObjectToScene(routerObject, activeScene);
        routerObject.AddComponent<BattleSceneResultRouter>();
    }

    private void Awake()
    {
        Debug.Log($"[RestartTrace] BattleSceneResultRouter Awake scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)} resultTriggered={resultTriggered}", this);
        ResetForNewBattle();
    }

    private void OnEnable()
    {
        Debug.Log($"[RestartTrace] BattleSceneResultRouter OnEnable scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)} resultTriggered={resultTriggered}", this);
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
        Debug.Log($"[RestartTrace] BattleSceneResultRouter OnDisable scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)}", this);
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        Debug.Log($"[RestartTrace] BattleSceneResultRouter OnDestroy scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)}", this);
        UnsubscribeEvents();
    }

    private void ResetForNewBattle()
    {
        resultTriggered = false;
        UnsubscribeEvents();
        player01Health = null;
        player02Health = null;
        difficultyDirector = null;
        Debug.Log($"[RestartTrace] Router reset for new battle scene={gameObject.scene.name} router={GetObjectDebugLabel(gameObject)}", this);
        RefreshBindings(forceRebind: true);
        SubscribeEvents();
    }

    private void RefreshBindings(bool forceRebind)
    {
        CombatHealth resolvedPlayer01 = FindPlayerHealth("Player01", typeof(Player01SkillController), gameObject.scene);
        CombatHealth resolvedPlayer02 = FindPlayerHealth("Player02", typeof(Player2PrototypeController), gameObject.scene);
        EnemyDifficultyDirector resolvedDirector = FindSceneDifficultyDirector(gameObject.scene);

        if (forceRebind || player01Health != resolvedPlayer01 || player02Health != resolvedPlayer02 || difficultyDirector != resolvedDirector)
        {
            UnsubscribeEvents();
            player01Health = resolvedPlayer01;
            player02Health = resolvedPlayer02;
            difficultyDirector = resolvedDirector;
            Debug.Log(
                $"[RestartTrace] Router bound scene={gameObject.scene.name} player01={GetObjectDebugLabel(player01Health)} player02={GetObjectDebugLabel(player02Health)} difficultyDirector={GetObjectDebugLabel(difficultyDirector)}",
                this);
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
            Debug.Log($"[RestartTrace] Router unbound old player01={GetObjectDebugLabel(player01Health)}", this);
        }

        if (player02Health != null && subscribedPlayer02)
        {
            player02Health.Died -= HandlePlayerDeath;
            Debug.Log($"[RestartTrace] Router unbound old player02={GetObjectDebugLabel(player02Health)}", this);
        }

        if (difficultyDirector != null && subscribedVictory)
        {
            difficultyDirector.OnVictory -= HandleVictory;
            Debug.Log($"[RestartTrace] Router unbound old difficultyDirector={GetObjectDebugLabel(difficultyDirector)}", this);
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

        Debug.Log($"[PlayerDeathTrace] Death callback received from {ResolveDeadPlayerLabel()}", this);

        if (difficultyDirector != null && difficultyDirector.CurrentPhase == DifficultyPhase.Victory)
        {
            TriggerGameWin();
            return;
        }

        if (IsAnyPlayerDead())
        {
            Debug.Log("[PlayerDeathTrace] GameOver requested", this);
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
        Debug.Log("[PlayerDeathTrace] GameOver entered", this);
        Debug.Log("[BattleSceneResultRouter] GameOver triggered because a player died.", this);
        SceneManager.LoadScene(GameOverSceneName);
    }

    private void TriggerGameWin()
    {
        if (resultTriggered)
        {
            return;
        }

        resultTriggered = true;
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
        CombatHealth[] allHealth = Resources.FindObjectsOfTypeAll<CombatHealth>();
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

    private static EnemyDifficultyDirector FindSceneDifficultyDirector(Scene targetScene)
    {
        EnemyDifficultyDirector[] directors = Resources.FindObjectsOfTypeAll<EnemyDifficultyDirector>();
        for (int i = 0; i < directors.Length; i++)
        {
            EnemyDifficultyDirector candidate = directors[i];
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            Scene candidateScene = candidate.gameObject.scene;
            if (!candidateScene.IsValid() || !candidateScene.isLoaded || candidateScene != targetScene)
            {
                continue;
            }

            return candidate;
        }

        return null;
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
}
