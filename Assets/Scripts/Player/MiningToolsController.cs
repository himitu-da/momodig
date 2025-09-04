using UnityEngine;
using System.Collections.Generic;

public class MiningToolsController : MonoBehaviour
{
    [Header("掘削ツール設定")]
    [SerializeField] private List<MiningTool> usableMiningTools;
    [SerializeField] private MiningTool _mainMiningTool;
    [SerializeField] private MiningTool _subMiningTool; // サブ用ツール
    public MiningTool mainMiningTool
    {
        get => _mainMiningTool;
        private set // PlayerControllerからは変更させない
        {
            _mainMiningTool = value;
            if (_mainMiningTool != null && _mainMiningTool.miningModule != null)
            {
                // Diggerの掘削範囲を現在のツールの設定で初期化
                Digger digger = GetComponentInChildren<Digger>();
                if (digger != null)
                {
                    digger.SetDiggingAreaParameters(
                        _mainMiningTool.miningModule.DiggingCenter,
                        _mainMiningTool.miningModule.DiggingSize
                    );
                }
            }
        }
    }

    // サブ用ツールのプロパティ（外部からは変更不可）
    public MiningTool subMiningTool
    {
        get => _subMiningTool;
        private set => _subMiningTool = value;
    }

    void Awake()
    {
        if (usableMiningTools != null && usableMiningTools.Count > 0)
        {
            // プロパティ経由で設定することで、Diggerの初期化も行われる
            mainMiningTool = usableMiningTools[0];
        }

        // 未指定で、2本以上登録がある場合は2本目をサブに割り当て
        if (_subMiningTool == null && usableMiningTools != null && usableMiningTools.Count > 1)
        {
            subMiningTool = usableMiningTools[1];
        }
    }

    /// <summary>
    /// メイン用ツールを使用して掘削を実行する
    /// </summary>
    public void UseMainMineTool(GameObject user)
    {
        if (mainMiningTool != null)
        {
            mainMiningTool.Use(user);
        }
        else
        {
            Debug.LogWarning("No mining tool is currently selected.");
        }
    }

    /// <summary>
    /// サブ用ツールを使用して掘削を実行する
    /// </summary>
    public void UseSubMineTool(GameObject user)
    {
        var tool = subMiningTool != null ? subMiningTool : mainMiningTool;
        if (tool == null)
        {
            Debug.LogWarning("No sub mining tool is set, and no main tool to fallback.");
            return;
        }

        // 掘削範囲をサブツール設定に切り替え（念のため）
        if (tool.miningModule != null)
        {
            Digger digger = GetComponentInChildren<Digger>();
            if (digger != null)
            {
                digger.SetDiggingAreaParameters(
                    tool.miningModule.DiggingCenter,
                    tool.miningModule.DiggingSize
                );
            }
        }

    tool.Use(user);
    }

    /// <summary>
    /// 外部からツールの向きを更新するためのメソッド
    /// </summary>
    public void UpdateRotation(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        if (direction.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation;
            if (moveMode == PlayerController.MoveMode.TopDown)
            {
                // TopDownモードの回転計算
                float angle = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
                targetRotation = Quaternion.AngleAxis(angle, Vector3.up);
            }
            else // SideScroller
            {
                // SideScrollerモードの回転計算
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            
            // このオブジェクト（MiningTools）の向きを更新
            transform.rotation = targetRotation;
        }
    }

    // 任意: スクリプトから差し替えるためのSetter
    public void SetmainTool(MiningTool tool) => mainMiningTool = tool;
    public void SetSubTool(MiningTool tool) => subMiningTool = tool;
}
