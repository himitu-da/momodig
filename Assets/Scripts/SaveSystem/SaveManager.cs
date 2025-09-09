using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

    /// <summary>
    /// ゲーム全体のセーブとロードを管理するシングルトンクラス
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [SerializeField] private string saveProfileName = "default";
        [SerializeField] private bool prettyPrintJson = true; // デバッグ用にJSONを整形するか

        private Dictionary<string, ISaveable> _saveableEntities;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _saveableEntities = new Dictionary<string, ISaveable>();
        }

        private void Start()
        {
            // シーン内のISaveableをすべて検索して登録
            RegisterAllSaveableEntities();
        }

        /// <summary>
        /// 現在のゲーム状態をファイルに保存します
        /// </summary>
        public void SaveGame()
        {
            Debug.Log($"Saving game to profile: {saveProfileName}");
            foreach (var saveable in _saveableEntities.Values)
            {
                string fileName = saveable.SaveFileName;
                object state = saveable.CaptureState();
                string json = JsonUtility.ToJson(state, prettyPrintJson);
                
                WriteToFile(fileName, json);
            }
            Debug.Log("Save complete.");
        }

        /// <summary>
        /// ファイルからゲーム状態をロードします
        /// </summary>
        public void LoadGame()
        {
            Debug.Log($"Loading game from profile: {saveProfileName}");
            foreach (var saveable in _saveableEntities.Values)
            {
                string fileName = saveable.SaveFileName;
                string json = ReadFromFile(fileName);

                if (!string.IsNullOrEmpty(json))
                {
                    object state = JsonUtility.FromJson(json, saveable.CaptureState().GetType());
                    saveable.RestoreState(state);
                }
            }
            Debug.Log("Load complete.");
        }

        private void WriteToFile(string fileName, string json)
        {
            string path = GetSavePath(fileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to write to {path}. Error: {e.Message}");
            }
        }

        private string ReadFromFile(string fileName)
        {
            string path = GetSavePath(fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Save file not found at {path}");
                return null;
            }

            try
            {
                return File.ReadAllText(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to read from {path}. Error: {e.Message}");
                return null;
            }
        }

        private string GetSavePath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, saveProfileName, $"{fileName}.json");
        }

        /// <summary>
        /// シーン内のすべてのISaveableを検索して登録します
        /// </summary>
        private void RegisterAllSaveableEntities()
        {
            _saveableEntities.Clear();
            var saveables = FindObjectsOfType<MonoBehaviour>(true).OfType<ISaveable>();
            foreach (var saveable in saveables)
            {
                if (string.IsNullOrEmpty(saveable.SaveFileName))
                {
                    Debug.LogError($"{saveable.GetType().Name} has a null or empty SaveFileName.");
                    continue;
                }

                if (_saveableEntities.ContainsKey(saveable.SaveFileName))
                {
                    Debug.LogWarning($"Duplicate SaveFileName found: {saveable.SaveFileName}. Overwriting.");
                }
                _saveableEntities[saveable.SaveFileName] = saveable;
            }
            Debug.Log($"Registered {_saveableEntities.Count} saveable entities.");
        }
    }
