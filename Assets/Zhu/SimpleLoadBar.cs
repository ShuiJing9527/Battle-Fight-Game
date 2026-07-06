using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SimpleLoadBar : MonoBehaviour
{
    [Header("拖拽赋值")]
    public Image progressBar;
    public TextMeshProUGUI loadText;
    public GameObject loadBarRoot;
    public GameObject blackMask;

    [Header("设置")]
    public string gameSceneName = "\u8349\u539F";
    [Min(0.1f)] public float progressSmoothSpeed = 1.5f;

    private bool isLoading;
    private AsyncOperation loadOperation;

    private void Start()
    {
        if (loadBarRoot != null)
        {
            loadBarRoot.SetActive(false);
        }

        if (blackMask != null)
        {
            blackMask.SetActive(false);
        }

        isLoading = false;
        loadOperation = null;

        if (progressBar != null)
        {
            progressBar.fillAmount = 0f;
        }

        if (loadText != null)
        {
            loadText.text = "Loading 0%";
        }
    }

    public void OnClickStartGame()
    {
        if (isLoading)
        {
            return;
        }

        if (blackMask != null)
        {
            blackMask.SetActive(true);
        }

        if (loadBarRoot != null)
        {
            loadBarRoot.SetActive(true);
        }

        if (progressBar != null)
        {
            progressBar.fillAmount = 0f;
        }

        if (loadText != null)
        {
            loadText.text = "Loading 0%";
        }

        StartCoroutine(LoadSceneAsyncRoutine(gameSceneName));
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SimpleLoadBar] gameSceneName is empty.", this);
            yield break;
        }

        isLoading = true;
        loadOperation = SceneManager.LoadSceneAsync(sceneName);
        if (loadOperation == null)
        {
            Debug.LogWarning($"[SimpleLoadBar] Failed to start async loading for scene '{sceneName}'.", this);
            isLoading = false;
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        float displayedProgress = 0f;
        float speed = Mathf.Max(0.1f, progressSmoothSpeed);

        while (!loadOperation.isDone)
        {
            float targetProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.deltaTime * speed);

            if (progressBar != null)
            {
                progressBar.fillAmount = displayedProgress;
            }

            if (loadText != null)
            {
                loadText.text = $"Loading {Mathf.RoundToInt(displayedProgress * 100f)}%";
            }

            if (loadOperation.progress >= 0.9f && displayedProgress >= 1f)
            {
                loadOperation.allowSceneActivation = true;
            }

            yield return null;
        }

        isLoading = false;
        loadOperation = null;
    }
}
