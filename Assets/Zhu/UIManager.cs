using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public Canvas mainCanvas;
    [SerializeField] private string startSceneName = "\u8349\u539F";

    public void StartGame()
    {
        GameLocalization.MarkFormalGameStart();
        SceneManager.LoadScene(startSceneName);
    }

    public void SetStartSceneName(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            startSceneName = sceneName;
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
