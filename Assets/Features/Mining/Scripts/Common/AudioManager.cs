using UnityEngine;

/// <summary>
/// ゲーム全体のオーディオを管理するシングルトンクラス。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Voxel Destruction Sound Settings")]
    [SerializeField] private float minDestructionVolume = 1.0f;
    [SerializeField] private float volumePerVoxel = 0.05f;
    [SerializeField] private float maxDestructionVolume = 2.5f;

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
            // 掘削音はツールごとの音量設定をそのまま使うシンプルな形に変更
            _seSource.PlayOneShot(clip, toolVolume);
        }
    }

    /// <summary>
    /// ボクセル破壊SEを再生します。音量は破壊されたボクセル数とブロック固有の音量に応じて計算されます。
    /// </summary>
    /// <param name="clip">再生するAudioClip</param>
    /// <param name="destroyedVoxelCount">破壊されたボクセルの数</param>
    /// <param name="blockVolume">ブロックデータに設定された基本音量</param>
    public void PlayVoxelDestroyedSE(AudioClip clip, int destroyedVoxelCount, float blockVolume)
    {
        if (clip != null && destroyedVoxelCount > 0)
        {
            float volume = minDestructionVolume + (destroyedVoxelCount * volumePerVoxel);
            volume = Mathf.Min(volume, maxDestructionVolume);
            _seSource.PlayOneShot(clip, volume * blockVolume);
        }
    }
}
