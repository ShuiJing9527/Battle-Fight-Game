using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BattleSceneLoadingGate : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup loadingCanvasGroup;
    [SerializeField] private Image progressFillImage;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private GameObject loadingRoot;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float minimumVisibleSeconds = 0.75f;
    [SerializeField, Min(0.1f)] private float progressSmoothSpeed = 0.9f;
    [SerializeField, Min(1)] private int initialFrameWaitCount = 2;
    [SerializeField, Min(0f)] private float finalFillDuration = 0.25f;

    [Header("Optional Runtime Dependencies")]
    [SerializeField] private EnemyDifficultyDirector difficultyDirector;
    [SerializeField] private EnemySpawner enemySpawner;

    private bool started;

    private void Awake()
    {
        if (loadingRoot != null)
        {
            loadingRoot.SetActive(true);
        }

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 1f;
            loadingCanvasGroup.blocksRaycasts = true;
            loadingCanvasGroup.interactable = true;
        }

        SetProgress(0f);
        CacheDependencies();
        SetGameplaySystemsEnabled(false);
    }

    private void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        StartCoroutine(LoadingRoutine());
    }

    private void CacheDependencies()
    {
        if (difficultyDirector == null)
        {
            difficultyDirector = FindObjectOfType<EnemyDifficultyDirector>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }
    }

    private IEnumerator LoadingRoutine()
    {
        float elapsed = 0f;
        SetProgress(0.1f);

        for (int i = 0; i < Mathf.Max(1, initialFrameWaitCount); i++)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        CacheDependencies();
        SetProgress(0.35f);

        yield return WaitForCoreObjects();
        elapsed += Time.unscaledDeltaTime;

        SetProgress(0.65f);
        yield return null;

        CacheDependencies();
        yield return Resources.UnloadUnusedAssets();

        SetProgress(0.9f);

        while (elapsed < minimumVisibleSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return SmoothFillToOne();

        SetGameplaySystemsEnabled(true);
        HideLoading();
    }

    private IEnumerator WaitForCoreObjects()
    {
        while (!AreCoreObjectsReady())
        {
            yield return null;
        }
    }

    private bool AreCoreObjectsReady()
    {
        if (GameObject.Find("HUDCanvas") == null)
        {
            return false;
        }

        if (FindSceneObjectByName("Player01") == null)
        {
            return false;
        }

        if (FindSceneObjectByName("Player02") == null)
        {
            return false;
        }

        if (difficultyDirector == null)
        {
            difficultyDirector = FindObjectOfType<EnemyDifficultyDirector>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }

        return difficultyDirector != null && enemySpawner != null;
    }

    private IEnumerator SmoothFillToOne()
    {
        float start = progressFillImage != null ? progressFillImage.fillAmount : 0.9f;
        float duration = Mathf.Max(0.01f, finalFillDuration);
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float value = Mathf.Lerp(start, 1f, Mathf.Clamp01(time / duration));
            SetProgress(value);
            yield return null;
        }

        SetProgress(1f);
    }

    private void SetProgress(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = Mathf.Max(progressFillImage.fillAmount, clamped);
            clamped = progressFillImage.fillAmount;
        }

        if (loadingText != null)
        {
            loadingText.text = $"Loading {Mathf.RoundToInt(clamped * 100f)}%";
        }
    }

    private void SetGameplaySystemsEnabled(bool enabled)
    {
        if (enemySpawner != null)
        {
            enemySpawner.enabled = enabled;
        }

        if (difficultyDirector != null)
        {
            difficultyDirector.enabled = enabled;
        }
    }

    private void HideLoading()
    {
        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 0f;
            loadingCanvasGroup.blocksRaycasts = false;
            loadingCanvasGroup.interactable = false;
        }

        if (loadingRoot != null)
        {
            loadingRoot.SetActive(false);
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || !candidate.scene.IsValid())
            {
                continue;
            }

            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }
}
