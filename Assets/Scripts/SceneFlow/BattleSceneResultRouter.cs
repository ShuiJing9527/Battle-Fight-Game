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
        RefreshBindings();
        SubscribeEvents();
    }

    private void OnEnable()
    {
        RefreshBindings();
        SubscribeEvents();
    }

    private void Update()
    {
        if (resultTriggered)
        {
            return;
        }

        RefreshBindings();
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
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void RefreshBindings()
    {
        if (player01Health == null)
        {
            player01Health = FindPlayerHealth("Player01", typeof(Player01SkillController));
        }

        if (player02Health == null)
        {
            player02Health = FindPlayerHealth("Player02", typeof(Player2PrototypeController));
        }

        if (difficultyDirector == null)
        {
            difficultyDirector = FindObjectOfType<EnemyDifficultyDirector>();
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
        }

        if (player02Health != null && subscribedPlayer02)
        {
            player02Health.Died -= HandlePlayerDeath;
        }

        if (difficultyDirector != null && subscribedVictory)
        {
            difficultyDirector.OnVictory -= HandleVictory;
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

    private static CombatHealth FindPlayerHealth(string objectName, Type controllerType)
    {
        CombatHealth[] allHealth = Resources.FindObjectsOfTypeAll<CombatHealth>();
        for (int i = 0; i < allHealth.Length; i++)
        {
            CombatHealth candidate = allHealth[i];
            if (candidate == null || candidate.gameObject == null || !candidate.gameObject.scene.IsValid())
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
            if (candidate == null || candidate.gameObject == null || !candidate.gameObject.scene.IsValid())
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
}
