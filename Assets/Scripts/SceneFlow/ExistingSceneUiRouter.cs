using UnityEngine;
using UnityEngine.SceneManagement;

public class ExistingSceneUiRouter : MonoBehaviour
{
    private const string RuntimeRootName = "ExistingSceneUiRouter";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != BattleSceneResultRouter.TitleSceneName)
        {
            return;
        }

        GameObject existing = GameObject.Find(RuntimeRootName);
        if (existing != null)
        {
            ExistingSceneUiRouter existingRouter = existing.GetComponent<ExistingSceneUiRouter>();
            if (existingRouter != null)
            {
                existingRouter.ConfigureTitleScene();
                return;
            }
        }

        GameObject root = new GameObject(RuntimeRootName);
        SceneManager.MoveGameObjectToScene(root, activeScene);
        ExistingSceneUiRouter router = root.AddComponent<ExistingSceneUiRouter>();
        router.ConfigureTitleScene();
    }

    private void ConfigureTitleScene()
    {
        SimpleLoadBar loadBar = FindFirstObjectInScene<SimpleLoadBar>();
        if (loadBar != null)
        {
            loadBar.gameSceneName = BattleSceneResultRouter.BattleSceneName;
        }

        UIManager uiManager = FindFirstObjectInScene<UIManager>();
        if (uiManager != null)
        {
            uiManager.SetStartSceneName(BattleSceneResultRouter.BattleSceneName);
        }
    }

    private static T FindFirstObjectInScene<T>() where T : Object
    {
        T[] allObjects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            T candidate = allObjects[i];
            if (candidate is Component component && component.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }
}
