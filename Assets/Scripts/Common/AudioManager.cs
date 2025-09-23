using UnityEngine;

/// <summary>
/// ゲーム全体のオーディオを管理するシングルトンクラス。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

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
}
