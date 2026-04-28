using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    private GameManager _gameManager;

    void Start()
    {
        _gameManager = GameManager.Instance;
    }

    // 加载指定场景（兼容GameManager）
    public void LoadScene(string sceneName)
    {
        if (_gameManager == null || string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("场景名为空，取消加载");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    // 退出游戏（无报错版）
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}