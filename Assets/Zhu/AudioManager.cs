using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("主界面背景音乐")]
    public AudioClip menuBgm;
    [Header("游戏场景背景音乐")]
    public AudioClip gameBgm;

    private AudioSource bgmSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // 自动添加 AudioSource，不用拖！
        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();
    }

    // 播放主界面音乐
    public void PlayMenuBGM()
    {
        if (menuBgm == null) return;

        bgmSource.clip = menuBgm;
        bgmSource.volume = GameManager.Instance.settings.musicVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    // 切换游戏音乐
    public void PlayGameBGM()
    {
        if (gameBgm == null) return;

        bgmSource.clip = gameBgm;
        bgmSource.volume = GameManager.Instance.settings.musicVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    // 停止音乐
    public void StopAllBGM()
    {
        bgmSource.Stop();
    }

    // 同步音量
    public void SetBgmVolume(float vol)
    {
        bgmSource.volume = vol;
    }
}