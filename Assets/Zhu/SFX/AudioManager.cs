using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("主界面背景音乐")]
    public AudioClip menuBgm;

    [Header("游戏场景背景音乐")]
    public AudioClip gameBgm;

    [Header("通用音效")]
    public AudioClip playerAttackSfx;
    public AudioClip enemyAttackSfx;
    public AudioClip playerHitSfx;
    public AudioClip enemyHitSfx;
    public AudioClip playerDeathSfx;
    public AudioClip enemyDeathSfx;

    [Header("UI音效")]
    public AudioClip buttonClickSfx;
    public AudioClip openPanelSfx;
    public AudioClip closePanelSfx;

    [Header("音量")]
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    [Header("场景名称设置")]
    public string menuSceneName = "StartScene";
    public string gameSceneName = "GameScene";

    [Header("音效限制")]
    public int maxSfxVoices = 8;

    [Tooltip("同一个音效最短间隔，防止一堆史莱姆同时乱叫")]
    public float sameClipCooldown = 0.08f;

    [Tooltip("普通音效整体压低，防止盖住BGM")]
    [Range(0f, 1f)] public float globalSfxLimiter = 0.75f;

    private AudioSource bgmSource;
    private List<AudioSource> sfxSources = new List<AudioSource>();
    private Dictionary<AudioClip, float> lastClipPlayTime = new Dictionary<AudioClip, float>();

    private int sfxIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitAudioSources();
        LoadVolumeFromGameManager();
        ApplyVolume();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        PlayBGMByCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LoadVolumeFromGameManager();
        ApplyVolume();
        PlayBGMBySceneName(scene.name);
    }

    private void InitAudioSources()
    {
        AudioSource[] oldSources = GetComponents<AudioSource>();

        if (oldSources.Length >= 1)
        {
            bgmSource = oldSources[0];
        }
        else
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;

        sfxSources.Clear();

        for (int i = 1; i < oldSources.Length; i++)
        {
            sfxSources.Add(oldSources[i]);
        }

        while (sfxSources.Count < maxSfxVoices)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            sfxSources.Add(source);
        }

        foreach (AudioSource source in sfxSources)
        {
            source.playOnAwake = false;
            source.loop = false;
        }
    }

    private void LoadVolumeFromGameManager()
    {
        if (GameManager.Instance == null || GameManager.Instance.settings == null)
            return;

        bgmVolume = Mathf.Clamp01(GameManager.Instance.settings.musicVolume);
        sfxVolume = Mathf.Clamp01(GameManager.Instance.settings.sfxVolume);
    }

    private void SaveVolumeToGameManager()
    {
        if (GameManager.Instance == null || GameManager.Instance.settings == null)
            return;

        GameManager.Instance.settings.musicVolume = bgmVolume;
        GameManager.Instance.settings.sfxVolume = sfxVolume;
    }

    private void ApplyVolume()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    private void PlayBGMByCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        PlayBGMBySceneName(currentScene.name);
    }

    private void PlayBGMBySceneName(string sceneName)
    {
        if (sceneName == menuSceneName)
        {
            PlayMenuBGM();
        }
        else if (sceneName == gameSceneName)
        {
            PlayGameBGM();
        }
    }

    public void PlayMenuBGM()
    {
        PlayBGM(menuBgm);
    }

    public void PlayGameBGM()
    {
        PlayBGM(gameBgm);
    }

    private void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("BGM 没有拖进去");
            return;
        }

        if (bgmSource == null)
        {
            InitAudioSources();
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            bgmSource.volume = bgmVolume;
            return;
        }

        bgmSource.clip = clip;
        bgmSource.volume = bgmVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopAllBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);

        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }

        SaveVolumeToGameManager();
    }

    public void SetMusicVolume(float volume)
    {
        SetBgmVolume(volume);
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveVolumeToGameManager();
    }

    public void SetSoundVolume(float volume)
    {
        SetSfxVolume(volume);
    }

    public float GetBgmVolume()
    {
        return bgmVolume;
    }

    public float GetSfxVolume()
    {
        return sfxVolume;
    }

    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f);
    }

    public void PlaySFX(AudioClip clip, float volumeMultiplier)
    {
        if (clip == null) return;

        if (sfxSources == null || sfxSources.Count == 0)
        {
            InitAudioSources();
        }

        if (IsSameClipTooSoon(clip))
        {
            return;
        }

        AudioSource source = GetAvailableSfxSource();

        if (source == null)
        {
            return;
        }

        float finalVolume = sfxVolume * volumeMultiplier * globalSfxLimiter;
        finalVolume = Mathf.Clamp01(finalVolume);

        lastClipPlayTime[clip] = Time.time;

        source.Stop();
        source.clip = clip;
        source.volume = finalVolume;
        source.Play();
    }

    private bool IsSameClipTooSoon(AudioClip clip)
    {
        if (clip == null) return true;

        if (lastClipPlayTime.TryGetValue(clip, out float lastTime))
        {
            if (Time.time - lastTime < sameClipCooldown)
            {
                return true;
            }
        }

        return false;
    }

    private AudioSource GetAvailableSfxSource()
    {
        for (int i = 0; i < sfxSources.Count; i++)
        {
            AudioSource source = sfxSources[i];

            if (source != null && !source.isPlaying)
            {
                return source;
            }
        }

        sfxIndex++;

        if (sfxIndex >= sfxSources.Count)
        {
            sfxIndex = 0;
        }

        return sfxSources[sfxIndex];
    }

    public void PlayPlayerAttack()
    {
        PlaySFX(playerAttackSfx);
    }

    public void PlayEnemyAttack()
    {
        PlaySFX(enemyAttackSfx, 0.7f);
    }

    public void PlayPlayerHit()
    {
        PlaySFX(playerHitSfx);
    }

    public void PlayEnemyHit()
    {
        PlaySFX(enemyHitSfx, 0.6f);
    }

    public void PlayPlayerDeath()
    {
        PlaySFX(playerDeathSfx);
    }

    public void PlayEnemyDeath()
    {
        PlaySFX(enemyDeathSfx, 0.7f);
    }

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSfx);
    }

    public void PlayOpenPanel()
    {
        PlaySFX(openPanelSfx);
    }

    public void PlayClosePanel()
    {
        PlaySFX(closePanelSfx);
    }
}