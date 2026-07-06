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

    [Header("默认音量")]
    [Range(0f, 1f)] public float defaultBgmVolume = 0.5f;
    [Range(0f, 1f)] public float defaultSfxVolume = 0.8f;

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

    private float GetMusicVolume()
    {
        if (GameManager.Instance != null && GameManager.Instance.settings != null)
        {
            return GameManager.Instance.settings.musicVolume;
        }

        return defaultBgmVolume;
    }

    private float GetSfxVolume()
    {
        return defaultSfxVolume;
    }

    private void PlayBGMByCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        PlayBGMBySceneName(currentScene.name);
    }

    private void PlayBGMBySceneName(string sceneName)
    {
        Debug.Log("当前场景：" + sceneName);

        if (sceneName == menuSceneName)
        {
            PlayMenuBGM();
        }
        else if (sceneName == gameSceneName)
        {
            PlayGameBGM();
        }
        else
        {
            // 其他场景默认继续播放当前音乐
            // 如果你想失败/胜利界面停音乐，可以改成 StopAllBGM();
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
            Debug.LogWarning("BGM 音频没有拖进去");
            return;
        }

        if (bgmSource == null)
        {
            InitAudioSources();
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.volume = GetMusicVolume();
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
        if (bgmSource != null)
        {
            bgmSource.volume = volume;
        }
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

        sfxSource.PlayOneShot(clip, GetSfxVolume() * volumeMultiplier);
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