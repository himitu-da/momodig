using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 素材タイプとそれに対応するAudioClipを関連付けるためのシリアライズ可能なクラス。
/// </summary>
[System.Serializable]
public class MaterialSound
{
    public MaterialType materialType;
    public AudioClip sound;
}

[CreateAssetMenu(fileName = "MiningTool", menuName = "Scriptable Objects/MiningTool")]
public abstract class MiningTool : ScriptableObject
{
    [Header("ツール設定")]
    [SerializeField] protected string toolName;
    [SerializeField] protected Sprite toolIcon;

    [Header("アニメーション設定")]
    [Tooltip("Player Animatorの待機モーション切り替え用ID")]
    [SerializeField] private int toolTypeID = 0;
    [Tooltip("Player Animatorの採掘トリガー名")]
    [SerializeField] private string animationTriggerName = "Mine";
    [Tooltip("Player Animatorの採掘ステート名")]
    [SerializeField] private string animationStateName = "Mine";

    [Header("ランタイム用 Prefab（必須: MiningToolBehaviour 付き）")]
    [SerializeField] private GameObject toolPrefab;

    [Header("掘削モジュール")]
    public MiningModule miningModule;

    [Header("サウンド設定")]
    [Tooltip("標準の掘削音")]
    [SerializeField] private AudioClip defaultMiningSound;
    [Tooltip("ブロックの素材タイプごとの掘削音")]
    [SerializeField] private List<MaterialSound> materialSounds;

    public GameObject ToolPrefab => toolPrefab;
    public int ToolTypeID => toolTypeID;
    public string AnimationTriggerName => animationTriggerName;
    public string AnimationStateName => animationStateName;
    public AudioClip DefaultMiningSound => defaultMiningSound;

    /// <summary>
    /// 指定された素材タイプに対応する掘削音を取得します。
    /// 対応する音がない場合は、標準の掘削音を返します。
    /// </summary>
    public AudioClip GetMiningSound(MaterialType materialType)
    {
        var materialSound = materialSounds.FirstOrDefault(s => s.materialType == materialType);
        if (materialSound != null && materialSound.sound != null)
        {
            return materialSound.sound;
        }
        return defaultMiningSound;
    }

    /// <summary>
    /// コントローラから呼ばれ、ツールPrefabを生成して MiningToolBehaviour を返します。
    /// </summary>
    public MiningToolBehaviour InstantiateBehaviour(Transform parent)
    {
        if (toolPrefab == null)
        {
            Debug.LogError($"[{name}] Tool Prefab is not assigned.");
            return null;
        }

        var go = Object.Instantiate(toolPrefab, parent);
        go.name = toolPrefab.name; // (Clone) を避けるため任意で整える

        var behaviour = go.GetComponent<MiningToolBehaviour>();
        if (behaviour == null)
        {
            Debug.LogError($"Tool Prefab '{toolPrefab.name}' does not have a MiningToolBehaviour component.");
            Object.Destroy(go);
            return null;
        }

        // SO データをバインド
        behaviour.BindTool(this);
        return behaviour;
    }

    private void OnValidate()
    {
        if (toolPrefab != null)
        {
            var hasBehaviour = toolPrefab.GetComponent<MiningToolBehaviour>() != null;
            if (!hasBehaviour)
            {
                Debug.LogWarning($"Tool Prefab '{toolPrefab.name}' does not contain MiningToolBehaviour. Attach a concrete behaviour.");
            }
        }
    }
}
