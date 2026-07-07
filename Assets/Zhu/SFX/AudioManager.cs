using UnityEngine;
using UnityEngine.SceneManagement;

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

    private AudioSource bgmSource;
    private AudioSource sfxSource;

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
        AudioSource[] sources = GetComponents<AudioSource>();

        if (sources.Length >= 1)
        {
            bgmSource = sources[0];
        }
        else
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        if (sources.Length >= 2)
        {
            sfxSource = sources[1];
        }
        else
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
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

        Debug.Log("播放BGM：" + clip.name);
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

        if (sfxSource == null)
        {
            InitAudioSources();
        }

        sfxSource.PlayOneShot(clip, sfxVolume * volumeMultiplier);
    }

    public void PlayPlayerAttack()
    {
        PlaySFX(playerAttackSfx);
    }

    public void PlayEnemyAttack()
    {
        PlaySFX(enemyAttackSfx);
    }

    public void PlayPlayerHit()
    {
        PlaySFX(playerHitSfx);
    }

    public void PlayEnemyHit()
    {
        PlaySFX(enemyHitSfx);
    }

    public void PlayPlayerDeath()
    {
        PlaySFX(playerDeathSfx);
    }

    public void PlayEnemyDeath()
    {
        PlaySFX(enemyDeathSfx);
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