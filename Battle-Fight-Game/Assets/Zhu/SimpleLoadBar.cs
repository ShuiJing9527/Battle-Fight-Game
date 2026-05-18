using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimpleLoadBar : MonoBehaviour
{
    [Header("拖拽赋值")]
    public Image progressBar;
    public TextMeshProUGUI loadText;
    public GameObject loadBarRoot;
    public GameObject blackMask;  // 全屏黑色背景

    [Header("设置")]
    public float loadTime = 3f;
    public string gameSceneName = "GameScene";

    private float timer;
    private bool isLoading;

    void Start()
    {
        // 一开始全部隐藏
        if (loadBarRoot != null)
            loadBarRoot.SetActive(false);
        if (blackMask != null)
            blackMask.SetActive(false);

        isLoading = false;
        timer = 0f;
        if (progressBar != null)
            progressBar.fillAmount = 0;
    }

    // 开始游戏按钮绑定这个
    public void OnClickStartGame()
    {
        // 点击后：显示黑背景 + 进度条
        if (blackMask != null)
            blackMask.SetActive(true);
        if (loadBarRoot != null)
            loadBarRoot.SetActive(true);

        isLoading = true;
        timer = 0f;
        progressBar.fillAmount = 0;
    }

    void Update()
    {
        if (!isLoading) return;

        timer += Time.deltaTime;
        float p = Mathf.Clamp01(timer / loadTime);
        progressBar.fillAmount = p;

        loadText.text = $"加载中 {Mathf.Round(p * 100)}%";

        // 加载完成跳转
        if (p >= 1f)
        {
            isLoading = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
        }
    }
}