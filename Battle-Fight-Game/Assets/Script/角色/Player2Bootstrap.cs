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

    public GameObject CurrentPlayer { get; private set; }
    public Transform CurrentPlayerTransform => CurrentPlayer != null ? CurrentPlayer.transform : null;
    public GameObject PartyLeader => partyLeader;

    private PlayerCameraRig cameraRig;
    private Animator player1Animator;
    private Animator player2Animator;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreateBeforeSceneLoad()
    {
        EnsureBootstrapExists();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateAfterSceneLoad()
    {
        EnsureBootstrapExists();
    }

    private static void EnsureBootstrapExists()
    {
        if (FindObjectOfType<Player2Bootstrap>() != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("PLAYER2_Bootstrap");
        bootstrap.AddComponent<Player2Bootstrap>();
        DontDestroyOnLoad(bootstrap);
    }

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
        if (!showSwitchHint)
        {
            return;
        }

        const int width = 220;
        const int height = 30;
        Rect rect = new Rect(20f, 20f, width, height);

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

        GUI.Label(rect, "T: Switch Player", style);
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