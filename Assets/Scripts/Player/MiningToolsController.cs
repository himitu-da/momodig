using UnityEngine;
using System.Collections.Generic;

public class MiningToolsController : MonoBehaviour
{
    [Header("掘削ツール設定")]
    [SerializeField] private List<MiningTool> usableMiningTools;
    [SerializeField] private MiningTool _currentMiningTool;
    public MiningTool currentMiningTool
    {
        get => _currentMiningTool;
        private set // PlayerControllerからは変更させない
        {
            _currentMiningTool = value;
            if (_currentMiningTool != null && _currentMiningTool.miningModule != null)
            {
                // Diggerの掘削範囲を現在のツールの設定で初期化
                Digger digger = GetComponentInChildren<Digger>();
                if (digger != null)
                {
                    digger.SetDiggingAreaParameters(
                        _currentMiningTool.miningModule.DiggingCenter,
                        _currentMiningTool.miningModule.DiggingSize
                    );
                }
            }
        }
    }

    void Awake()
    {
        if (usableMiningTools != null && usableMiningTools.Count > 0)
        {
            // プロパティ経由で設定することで、Diggerの初期化も行われる
            currentMiningTool = usableMiningTools[0];
        }
    }

    /// <summary>
    /// 現在のツールを使用して掘削を実行する
    /// </summary>
    public void UseCurrentTool(GameObject user)
    {
        if (currentMiningTool != null)
        {
            currentMiningTool.Use(user);
        }
        else
        {
            Debug.LogWarning("No mining tool is currently selected.");
        }
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
}
