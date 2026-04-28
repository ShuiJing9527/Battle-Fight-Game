using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    // 拖拽赋值你的整个画布
    public Canvas mainCanvas;

    // 开始游戏按钮点击事件
    public void StartGame()
    {
        // 写你真实游戏场景名
        SceneManager.LoadScene("GameScene");
    }

    // 退出游戏
    public void ExitGame()
    {
        Application.Quit();
    }
}