using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Player2EditorSetup
{
    private const string Player2ObjectName = "Player02";
    private static double lastEnsureTime;

    static Player2EditorSetup()
    {
        EditorApplication.delayCall += EnsurePlayer2ControllerInEditor;
        EditorApplication.hierarchyChanged += HandleHierarchyChanged;
    }

    private static void HandleHierarchyChanged()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup - lastEnsureTime < 0.2d)
        {
            return;
        }

        EnsurePlayer2ControllerInEditor();
    }

    private static void EnsurePlayer2ControllerInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        lastEnsureTime = EditorApplication.timeSinceStartup;

        GameObject player2 = GameObject.Find(Player2ObjectName);
        if (player2 == null)
        {
            return;
        }

        if (player2.GetComponent<Player2PrototypeController>() != null)
        {
            return;
        }

        Undo.AddComponent<Player2PrototypeController>(player2);
        EditorSceneManager.MarkSceneDirty(player2.scene);
        Debug.Log("[PLAYER2] Auto-added Player2PrototypeController in Editor so you can tune parameters before Play.", player2);
    }
}
