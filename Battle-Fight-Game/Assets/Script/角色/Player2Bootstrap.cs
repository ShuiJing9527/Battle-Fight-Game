using UnityEngine;
using UnityEngine.InputSystem;

public class Player2Bootstrap : MonoBehaviour
{
    [Header("Spawn")]
    public Vector3 spawnOffset = new Vector3(1.5f, 0f, 0f);
    public Vector3 spawnScale = new Vector3(1f, 2f, 1f);

    private GameObject existingPlayer;
    private GameObject player2;
    private bool usingPlayer2;
    private PlayerCameraRig cameraRig;

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
        existingPlayer = GameObject.FindGameObjectWithTag("Player");
        cameraRig = FindObjectOfType<PlayerCameraRig>();

        BuildPlayer2();
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
        const int width = 360;
        const int height = 60;
        Rect rect = new Rect((Screen.width - width) / 2f, 16f, width, height);
        string label = usingPlayer2 ? "切换到 原角色 (T)" : "切换到 PLAYER2 (T)";

        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 24;

        if (GUI.Button(rect, label, style)) ToggleCharacter();
    }

    private void BuildPlayer2()
    {
        if (player2 != null) return;

        Vector3 spawnPos = spawnOffset;
        if (existingPlayer != null)
        {
            spawnPos = existingPlayer.transform.position + spawnOffset;
            spawnPos.y = existingPlayer.transform.position.y;
        }

        player2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player2.name = "PLAYER2_TestCube";
        player2.transform.position = spawnPos;
        player2.transform.localScale = spawnScale;

        Rigidbody rb = player2.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        player2.AddComponent<Player2PrototypeController>();
    }

    private void ToggleCharacter()
    {
        usingPlayer2 = !usingPlayer2;
        SetPlayer2Active(usingPlayer2);
        Debug.Log(usingPlayer2 ? "[PLAYER2] 已切换到 PLAYER2" : "[PLAYER2] 已切换回原角色");
    }

    private void SetPlayer2Active(bool active)
    {
        if (existingPlayer == null)
        {
            existingPlayer = GameObject.FindGameObjectWithTag("Player");
        }

        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<PlayerCameraRig>();
        }

        if (active)
        {
            if (existingPlayer != null && player2 != null)
            {
                Vector3 p = existingPlayer.transform.position;
                player2.transform.position = new Vector3(p.x, p.y, p.z);
            }

            if (player2 != null) player2.SetActive(true);
            if (player2 != null)
            {
                Rigidbody rb = player2.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.WakeUp();
                }
            }
            if (existingPlayer != null) existingPlayer.SetActive(false);
            if (cameraRig != null && player2 != null) cameraRig.playerSlot = player2.transform;
        }
        else
        {
            if (existingPlayer != null && player2 != null)
            {
                Vector3 p = player2.transform.position;
                existingPlayer.transform.position = new Vector3(p.x, p.y, p.z);
            }

            if (existingPlayer != null) existingPlayer.SetActive(true);
            if (player2 != null) player2.SetActive(false);
            if (cameraRig != null && existingPlayer != null) cameraRig.playerSlot = existingPlayer.transform;
        }
    }
}
