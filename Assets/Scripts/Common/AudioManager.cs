using UnityEngine;

/// <summary>
/// ゲーム全体のオーディオを管理するシングルトンクラス。
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;

    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AudioManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                }
            }
            return _instance;
        }
    }

    private AudioSource _seSource;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // SE再生用のAudioSourceを追加
        _seSource = gameObject.AddComponent<AudioSource>();
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
