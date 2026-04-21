using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 适配多语言的场景加载器（Unity6 原生支持）
/// 【挂载位置】：和 GameManager 同一个空物体上
/// 【前置依赖】：GameManager 已初始化
/// </summary>
public class SceneLoader : MonoBehaviour
{
    private GameManager _gameManager;

    void Start()
    {
        // 使用现有的单例引用并做空值检查
        _gameManager = GameManager.Instance;
        if (_gameManager == null)
        {
            Debug.LogError("【SceneLoader】GameManager.Instance 为空！请确保 GameManager 先于 UI/SceneLoader 初始化。", this);
        }
    }

    // 加载下一个场景（供 UI 按钮调用）
    public void LoadNextScene()
    {
        if (_gameManager == null) return;

        // 先保存
        _gameManager.SaveGame();

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentSceneIndex + 1;
        int total = SceneManager.sceneCountInBuildSettings;

        if (nextIndex >= 0 && nextIndex < total)
        {
            // 多语言提示：加载下一场景
            string loadTip = _gameManager.GetLocalizedText("sceneloader_load_next");
            Debug.Log(string.Format(loadTip, nextIndex));
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            // 多语言警告：场景索引越界
            string outOfRangeTip = _gameManager.GetLocalizedText("sceneloader_index_out_range");
            Debug.LogWarning(string.Format(outOfRangeTip, nextIndex, total - 1));
        }
    }

    // 加载指定场景（供读档时调用）
    public void LoadScene(string sceneName)
    {
        if (_gameManager == null) return;

        _gameManager.SaveGame();
        if (string.IsNullOrEmpty(sceneName))
        {
            // 多语言警告：场景名为空
            string emptySceneTip = _gameManager.GetLocalizedText("sceneloader_scene_name_empty");
            Debug.LogWarning(emptySceneTip);
            return;
        }
        // 多语言提示：加载指定场景
        string loadSceneTip = _gameManager.GetLocalizedText("sceneloader_load_scene");
        Debug.Log(string.Format(loadSceneTip, sceneName));
        SceneManager.LoadScene(sceneName);
    }

    // 加载存档中的场景（读档后调用）
    public void LoadSavedScene()
    {
        if (_gameManager == null) return;

        // 确保 saveData 已有内容
        var save = _gameManager.saveData;
        if (save == null || string.IsNullOrEmpty(save.currentScene))
        {
            // 多语言警告：存档无效
            string invalidSaveTip = _gameManager.GetLocalizedText("sceneloader_invalid_save");
            Debug.LogWarning(invalidSaveTip);
            // 尝试让 GameManager 处理加载（GameManager.LoadGame 会自己加载场景）
            _gameManager.LoadGame();
            return;
        }

        // 捕获保存的位置，场景加载完成后再移动玩家
        Vector3 savedPos = save.position;

        // 订阅一次性的回调
        void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            // 只在目标场景触发时处理
            if (scene.name == save.currentScene)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    player.transform.position = savedPos;
                    // 多语言提示：玩家位置恢复成功
                    string restorePosTip = _gameManager.GetLocalizedText("sceneloader_restore_pos");
                    Debug.Log(restorePosTip);
                }
                else
                {
                    // 多语言警告：未找到玩家
                    string noPlayerTip = _gameManager.GetLocalizedText("sceneloader_no_player");
                    Debug.LogWarning(noPlayerTip);
                }

                // 取消订阅
                SceneManager.sceneLoaded -= OnLoaded;
            }
        }

        SceneManager.sceneLoaded += OnLoaded;
        SceneManager.LoadScene(save.currentScene);
    }

    // 退出游戏（供 UI 按钮调用，全平台兼容）
    public void QuitGame()
    {
        // 1. 空值保护：防止 GameManager 为空导致代码卡住
        if (_gameManager != null)
        {
            _gameManager.SaveGame();
            // 多语言提示：存档已保存，准备退出
            string saveQuitTip = _gameManager.GetLocalizedText("sceneloader_save_quit");
            Debug.Log(saveQuitTip);
        }
        else
        {
            // 多语言警告：GameManager 为空，跳过存档
            string noGMQuitTip = _gameManager.GetLocalizedText("sceneloader_no_gm_quit");
            Debug.LogWarning(noGMQuitTip);
        }

        // 2. 分平台处理退出逻辑（覆盖所有场景）
#if UNITY_EDITOR
        // 编辑器内：停止播放模式
        EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE // PC 单机版（Windows/Mac/Linux）
        Application.Quit();
#elif UNITY_ANDROID // 安卓
        AndroidJavaClass jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject jo = jc.GetStatic<AndroidJavaObject>("currentActivity");
        jo.Call("finish");
#elif UNITY_IOS // iOS
        Application.Quit();
#elif UNITY_WEBGL // 网页端
        Application.OpenURL("about:blank");
#endif
    }
}