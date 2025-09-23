using UnityEngine;

/// <summary>
/// ゲーム全体のオーディオを管理するシングルトンクラス。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Digging Sound Settings")]
    [SerializeField] private float minDiggingVolume = 1.0f;
    [SerializeField] private float volumePerBlock = 0.1f;
    [SerializeField] private float maxDiggingVolume = 2.0f;

    private AudioSource _seSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // SE再生用のAudioSourceを取得
        _seSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// SEを再生します。
    /// </summary>
    /// <param name="clip">再生するAudioClip</param>
    public void PlaySE(AudioClip clip)
    {
        if (clip != null)
        {
            _seSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// SEを音量を指定して再生します。
    /// </summary>
    /// <param name="clip">再生するAudioClip</param>
    /// <param name="volumeScale">音量スケール</param>
    public void PlaySE(AudioClip clip, float volumeScale)
    {
        if (clip != null)
        {
            _seSource.PlayOneShot(clip, volumeScale);
        }
    }

    /// <summary>
    /// 掘削SEを再生します。音量は掘削したブロック数とツールの基本音量に応じて計算されます。
    /// </summary>
    /// <param name="clip">再生するAudioClip</param>
    /// <param name="hitBlockCount">ヒットしたブロックの数</param>
    /// <param name="toolVolume">ツールの基本音量</param>
    public void PlayDiggingSE(AudioClip clip, int hitBlockCount, float toolVolume)
    {
        if (clip != null)
        {
            float volume = minDiggingVolume + (hitBlockCount * volumePerBlock);
            volume = Mathf.Min(volume, maxDiggingVolume);
            _seSource.PlayOneShot(clip, volume * toolVolume);
        }
    }
}
