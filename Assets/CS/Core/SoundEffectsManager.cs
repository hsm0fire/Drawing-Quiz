using UnityEngine;

public class SoundEffectsManager : MonoBehaviour
{
    public static SoundEffectsManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Kenney UI Clips")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip buttonHoverClip;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip wrongClip;
    [SerializeField] private AudioClip roundEndClip;

    [Header("Background Music")]
    [SerializeField] private AudioClip bgmClip;

    private const string SFX_VOL_KEY = "SFXVolume";
    private const string BGM_VOL_KEY = "BGMVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // إعداد مصادر الصوت تلقائياً إن لم تكن مربوطة
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
        }

        sfxSource.playOnAwake = false;
        musicSource.playOnAwake = false;

        // تحميل مستويات الصوت السابقة
        float savedSFX = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.8f);
        float savedBGM = PlayerPrefs.GetFloat(BGM_VOL_KEY, 0.5f);

        SetSFXVolume(savedSFX);
        SetBGMVolume(savedBGM);

        // تشغيل موسيقى الخلفية إن وجدت
        if (bgmClip != null && !musicSource.isPlaying)
        {
            musicSource.clip = bgmClip;
            musicSource.Play();
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxSource.volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, volume);
        PlayerPrefs.Save();
    }

    public void SetBGMVolume(float volume)
    {
        musicSource.volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BGM_VOL_KEY, volume);
        PlayerPrefs.Save();
    }

    public float GetSFXVolume() => PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.8f);
    public float GetBGMVolume() => PlayerPrefs.GetFloat(BGM_VOL_KEY, 0.5f);

    public void PlayButtonClick() => PlayClip(buttonClickClip);
    public void PlayButtonHover() => PlayClip(buttonHoverClip);
    public void PlaySuccess() => PlayClip(successClip);
    public void PlayWrong() => PlayClip(wrongClip);
    public void PlayRoundEnd() => PlayClip(roundEndClip);

    public void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }
}