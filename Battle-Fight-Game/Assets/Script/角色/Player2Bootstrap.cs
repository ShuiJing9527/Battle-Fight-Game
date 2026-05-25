using UnityEngine;
using UnityEngine.InputSystem;

public class Player2Bootstrap : MonoBehaviour
{
    [Header("Scene Players")]
    [SerializeField] private string player01Name = "Player01";
    [SerializeField] private string player02Name = "Player02";
    [SerializeField] private bool showSwitchHint = true;
    [SerializeField] private bool disablePlayer2AnimatorIfSharedController = true;

    private GameObject player1;
    private GameObject player2;
    private bool usingPlayer2;
    private PlayerCameraRig cameraRig;
    private PlayerMovement player1Movement;
    private Player2PrototypeController player2Controller;
    private Animator player1Animator;
    private Animator player2Animator;

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
        if (FindObjectOfType<Player2Bootstrap>() != null) return;

        GameObject bootstrap = new GameObject("PLAYER2_Bootstrap");
        bootstrap.AddComponent<Player2Bootstrap>();
        DontDestroyOnLoad(bootstrap);
    }

    private void Start()
    {
        ResolveScenePlayers();
        cameraRig = FindObjectOfType<PlayerCameraRig>();

        CleanupLegacyPlayer2Cube();
        EnsurePlayer2Setup();
        SetPlayer2Active(false);

        Debug.Log("[PLAYER2] Bootstrap loaded. Press T to toggle.");
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
        string label = "T: Switch Player";

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

        GUI.Label(rect, label, style);
    }

    private void ResolveScenePlayers()
    {
        if (player1 == null)
        {
            player1 = GameObject.Find(player01Name);
        }

        if (player2 == null)
        {
            player2 = GameObject.Find(player02Name);
        }

        if (player1 == null || player2 == null)
        {
            return;
        }

        if (player1Movement == null)
        {
            player1Movement = player1.GetComponent<PlayerMovement>();
        }

        if (player2Controller == null)
        {
            player2Controller = player2.GetComponent<Player2PrototypeController>();
        }

        if (player1Animator == null)
        {
            player1Animator = player1.GetComponent<Animator>();
        }

        if (player2Animator == null)
        {
            player2Animator = player2.GetComponent<Animator>();
        }
    }

    private void CleanupLegacyPlayer2Cube()
    {
        GameObject legacy = GameObject.Find("PLAYER2_TestCube");
        if (legacy != null)
        {
            Destroy(legacy);
            Debug.Log("[PLAYER2] Removed legacy PLAYER2_TestCube.");
        }
    }

    private void EnsurePlayer2Setup()
    {
        if (player2 == null)
        {
            return;
        }

        Rigidbody rb = player2.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = player2.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        if (player2.GetComponent<Collider>() == null)
        {
            BoxCollider collider = player2.AddComponent<BoxCollider>();
            SpriteRenderer sr = player2.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Vector3 size = sr.sprite.bounds.size;
                collider.size = new Vector3(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y), 0.8f);
            }
        }

        Player2PrototypeController p2Controller = player2.GetComponent<Player2PrototypeController>();
        if (p2Controller == null)
        {
            p2Controller = player2.AddComponent<Player2PrototypeController>();
        }
        p2Controller.rb = rb;
        player2Controller = p2Controller;

        PlayerMovement movement = player2.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        if (disablePlayer2AnimatorIfSharedController)
        {
            DisablePlayer2AnimatorIfUsingPlayer01Controller();
        }
    }

    private void DisablePlayer2AnimatorIfUsingPlayer01Controller()
    {
        if (player2 == null)
        {
            return;
        }

        if (player1Animator == null)
        {
            player1Animator = player1 != null ? player1.GetComponent<Animator>() : null;
        }

        if (player2Animator == null)
        {
            player2Animator = player2.GetComponent<Animator>();
        }

        if (player2Animator == null)
        {
            return;
        }

        RuntimeAnimatorController player2ControllerAsset = player2Animator.runtimeAnimatorController;
        RuntimeAnimatorController player1ControllerAsset = player1Animator != null ? player1Animator.runtimeAnimatorController : null;

        bool sharedWithPlayer1 = player2ControllerAsset != null && player1ControllerAsset != null && player2ControllerAsset == player1ControllerAsset;
        bool likelyPlayer01Controller = player2ControllerAsset != null && player2ControllerAsset.name == "Player";

        if (sharedWithPlayer1 || likelyPlayer01Controller)
        {
            player2Animator.enabled = false;
            Debug.LogWarning("[PLAYER2] Disabled Player02 Animator because it was using Player01/shared controller and could override Sprite.", player2);
        }
    }

    private void ToggleCharacter()
    {
        usingPlayer2 = !usingPlayer2;
        SetPlayer2Active(usingPlayer2);
        Debug.Log(usingPlayer2 ? "[PLAYER2] Switched to PLAYER2" : "[PLAYER2] Switched back to Player01");
    }

    private void SetPlayer2Active(bool active)
    {
        ResolveScenePlayers();

        if (player1 == null || player2 == null)
        {
            return;
        }

        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<PlayerCameraRig>();
        }

        if (active)
        {
            Vector3 p = player1.transform.position;
            player2.transform.position = new Vector3(p.x, p.y, p.z);

            player2.SetActive(true);
            Rigidbody rb = player2.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.WakeUp();
            }

            if (player1Movement != null) player1Movement.enabled = false;
            if (player2Controller != null) player2Controller.enabled = true;
            player1.SetActive(false);
            if (cameraRig != null) cameraRig.playerSlot = player2.transform;
        }
        else
        {
            Vector3 p = player2.transform.position;
            player1.transform.position = new Vector3(p.x, p.y, p.z);

            player1.SetActive(true);
            if (player1Movement != null) player1Movement.enabled = true;
            if (player2Controller != null) player2Controller.enabled = false;
            player2.SetActive(false);
            if (cameraRig != null) cameraRig.playerSlot = player1.transform;
        }
    }
}