using UnityEngine;
using UnityEngine.InputSystem;

public class Player2Bootstrap : MonoBehaviour
{
    [Header("Party Members")]
    [SerializeField] private GameObject player01;
    [SerializeField] private GameObject player02;
    [SerializeField] private GameObject partyLeader;

    [Header("Fallback Names (Optional)")]
    [SerializeField] private string player01Name = "Player01";
    [SerializeField] private string player02Name = "Player02";
    [SerializeField] private string partyLeaderName = "Player02";

    [Header("UI")]
    [SerializeField] private bool showSwitchHint = true;
    [SerializeField] private bool disablePlayer2AnimatorIfSharedController = true;
    [SerializeField] private bool showHealthBar = true;

    [Header("Player Health")]
    [SerializeField] private float playerStartHealth = 100f;

    public GameObject CurrentPlayer { get; private set; }
    public Transform CurrentPlayerTransform => CurrentPlayer != null ? CurrentPlayer.transform : null;
    public GameObject PartyLeader => partyLeader;

    private PlayerCameraRig cameraRig;
    private Animator player1Animator;
    private Animator player2Animator;
    private bool initialized;

    private Texture2D healthBarBackgroundTexture;
    private Texture2D healthBarFillTexture;
    private GUIStyle switchHintStyle;
    private GUIStyle healthBarLabelStyle;

    private void Start()
    {
        InitializePartyIfNeeded();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            ToggleCharacter();
        }
    }

    private void OnGUI()
    {
        EnsureGuiResources();

        if (showSwitchHint)
        {
            const int width = 220;
            const int height = 30;
            Rect rect = new Rect(20f, 20f, width, height);
            GUI.Label(rect, "T: Switch Player", switchHintStyle);
        }

        if (showHealthBar)
        {
            DrawHealthBar();
        }
    }

    private void ResolvePlayers()
    {
        if (player01 == null)
        {
            player01 = FindSceneObjectByNameIncludingInactive(player01Name);
        }

        if (player02 == null)
        {
            player02 = FindSceneObjectByNameIncludingInactive(player02Name);
        }

        if (player01 == null || player02 == null)
        {
            Debug.LogError($"[PARTY] Could not resolve players. player01={player01Name}, player02={player02Name}", this);
            return;
        }

        if (player1Animator == null && player01 != null)
        {
            player1Animator = player01.GetComponent<Animator>();
        }

        if (player2Animator == null && player02 != null)
        {
            player2Animator = player02.GetComponent<Animator>();
        }
    }

    private void ToggleCharacter()
    {
        InitializePartyIfNeeded();
        ResolvePlayers();

        if (player01 == null || player02 == null)
        {
            return;
        }

        if (CurrentPlayer != null)
        {
            Player2PrototypeController activeSkill = CurrentPlayer.GetComponent<Player2PrototypeController>();
            if (activeSkill != null && activeSkill.HasActiveRuntimeSkill)
            {
                Debug.Log("[PLAYER SWITCH] Cannot switch while skill is active.", this);
                return;
            }
        }

        GameObject next = CurrentPlayer == player01 ? player02 : player01;
        SetActivePlayer(next);

        Debug.Log($"[PARTY] Switched current player = {(CurrentPlayer != null ? CurrentPlayer.name : "null")}", this);
    }

    private void SetActivePlayer(GameObject nextActive)
    {
        ResolvePlayers();

        if (player01 == null || player02 == null || nextActive == null)
        {
            return;
        }

        GameObject nextInactive = nextActive == player01 ? player02 : player01;

        if (nextInactive == null || nextInactive == nextActive)
        {
            return;
        }

        if (CurrentPlayer != null && CurrentPlayer != nextActive)
        {
            Vector3 pos = CurrentPlayer.transform.position;
            nextActive.transform.position = pos;
        }

        nextActive.SetActive(true);
        nextInactive.SetActive(false);
        CurrentPlayer = nextActive;

        Rigidbody nextRb = nextActive.GetComponent<Rigidbody>();
        if (nextRb != null)
        {
            nextRb.linearVelocity = Vector3.zero;
            nextRb.angularVelocity = Vector3.zero;
        }

        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<PlayerCameraRig>();
        }

        if (cameraRig != null)
        {
            cameraRig.playerSlot = nextActive.transform;
        }
    }

    public void EnsureInitializedForSpawn()
    {
        InitializePartyIfNeeded();
    }

    private void InitializePartyIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        ResolvePlayers();
        ApplyInitialHealth(player01);
        ApplyInitialHealth(player02);
        cameraRig = FindObjectOfType<PlayerCameraRig>();

        if (partyLeader == null && !string.IsNullOrEmpty(partyLeaderName))
        {
            if (player01 != null && player01.name == partyLeaderName)
            {
                partyLeader = player01;
            }
            else if (player02 != null && player02.name == partyLeaderName)
            {
                partyLeader = player02;
            }
        }

        if (partyLeader == null)
        {
            partyLeader = player01;
        }

        GameObject startPlayer = partyLeader == player02 ? player02 : player01;
        SetActivePlayer(startPlayer);

        if (disablePlayer2AnimatorIfSharedController)
        {
            DisablePlayer2AnimatorIfUsingPlayer01Controller();
        }

        Debug.Log($"[PARTY] Leader = {(partyLeader != null ? partyLeader.name : "null")}", this);
        Debug.Log($"[PARTY] Current Player = {(CurrentPlayer != null ? CurrentPlayer.name : "null")}", this);

        initialized = true;
    }

    private void EnsureGuiResources()
    {
        if (healthBarBackgroundTexture == null)
        {
            healthBarBackgroundTexture = CreateColorTexture(new Color(0f, 0f, 0f, 0.75f));
        }

        if (healthBarFillTexture == null)
        {
            healthBarFillTexture = CreateColorTexture(new Color(0.85f, 0.15f, 0.15f, 1f));
        }

        if (switchHintStyle == null)
        {
            switchHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft
            };
            switchHintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
        }

        if (healthBarLabelStyle == null)
        {
            healthBarLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            healthBarLabelStyle.normal.textColor = Color.white;
        }
    }

    private void DrawHealthBar()
    {
        if (CurrentPlayer == null)
        {
            return;
        }

        float maxHealth = ResolveMaxHealth(CurrentPlayer);
        if (maxHealth <= 0f)
        {
            return;
        }

        float currentHealth = Mathf.Clamp(ResolveCurrentHealth(CurrentPlayer), 0f, maxHealth);
        const float x = 20f;
        const float y = 60f;
        const float width = 240f;
        const float height = 24f;
        const float border = 2f;

        GUI.DrawTexture(new Rect(x, y, width, height), healthBarBackgroundTexture);

        float fillWidth = (width - border * 2f) * (currentHealth / maxHealth);
        if (fillWidth > 0f)
        {
            GUI.DrawTexture(new Rect(x + border, y + border, fillWidth, height - border * 2f), healthBarFillTexture);
        }

        GUI.Label(new Rect(x, y, width, height), $"PLAYER HP  {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}", healthBarLabelStyle);
    }

    private void ApplyInitialHealth(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        CombatStats stats = player.GetComponent<CombatStats>();
        if (stats != null)
        {
            stats.maxHealth = playerStartHealth;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            resourceBank.maxHealth = playerStartHealth;
            resourceBank.currentHealth = playerStartHealth;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            combatHealth.currentHealth = playerStartHealth;
        }
    }

    private float ResolveCurrentHealth(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.currentHealth;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return combatHealth.currentHealth;
        }

        return 0f;
    }

    private float ResolveMaxHealth(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.maxHealth;
        }

        CombatStats stats = player.GetComponent<CombatStats>();
        if (stats != null)
        {
            return stats.maxHealth;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return Mathf.Max(combatHealth.currentHealth, playerStartHealth);
        }

        return playerStartHealth;
    }

    private static Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void DisablePlayer2AnimatorIfUsingPlayer01Controller()
    {
        if (player02 == null)
        {
            return;
        }

        if (player1Animator == null)
        {
            player1Animator = player01 != null ? player01.GetComponent<Animator>() : null;
        }

        if (player2Animator == null)
        {
            player2Animator = player02.GetComponent<Animator>();
        }

        if (player2Animator == null)
        {
            return;
        }

        RuntimeAnimatorController player2ControllerAsset = player2Animator.runtimeAnimatorController;
        RuntimeAnimatorController player1ControllerAsset = player1Animator != null ? player1Animator.runtimeAnimatorController : null;

        bool sharedWithPlayer1 =
            player2ControllerAsset != null &&
            player1ControllerAsset != null &&
            player2ControllerAsset == player1ControllerAsset;

        bool likelyPlayer01Controller =
            player2ControllerAsset != null &&
            player2ControllerAsset.name == "Player";

        if (sharedWithPlayer1 || likelyPlayer01Controller)
        {
            player2Animator.enabled = false;
            Debug.LogWarning("[PLAYER SWITCH] Disabled shared animator on switch/default player to preserve sprite.", player02);
        }
    }

    private static GameObject FindSceneObjectByNameIncludingInactive(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];

            if (go == null)
            {
                continue;
            }

            if (!go.scene.IsValid())
            {
                continue;
            }

            if (go.name == targetName)
            {
                return go;
            }
        }

        return null;
    }
}
