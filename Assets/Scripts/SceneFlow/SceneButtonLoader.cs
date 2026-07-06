using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonLoader : MonoBehaviour
{
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
