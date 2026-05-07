using UnityEngine;

/// <summary>
/// ツールPrefabに付与する抽象コンポーネント。
/// ScriptableObject(MiningTool)のデータと、実体の挙動・アニメーションを橋渡しします。
/// </summary>
public abstract class MiningToolBehaviour : MonoBehaviour
{
    /// <summary>対応するツールデータ(SO)</summary>
    public MiningTool ToolData { get; private set; }

    /// <summary>装備者(GameObject)。装備時に設定されます。</summary>
    protected GameObject user;

    /// <summary>ツール自身のAnimator（コントローラーから注入）</summary>
    protected Animator toolAnimator;

    /// <summary>掘削実行担当（コントローラーから注入）</summary>
    protected Digger digger;

    /// <summary>採掘アニメーション中フラグ</summary>
    public bool IsMining { get; protected set; } = false;

    /// <summary>装備中フラグ</summary>
    public bool IsEquipped { get; private set; }

    /// <summary>
    /// ツールデータを紐付けます（インスタンス化直後に呼ばれます）。
    /// </summary>
    public virtual void BindTool(MiningTool tool)
    {
        ToolData = tool;
    }

    /// <summary>
    /// 装備時に呼ばれます。
    /// </summary>
    public virtual void OnEquip(GameObject user)
    {
        this.user = user;
        // Digger はコントローラー側から SetDigger で注入される想定
        IsEquipped = true;
    }

    /// <summary>
    /// 脱着時に呼ばれます。
    /// </summary>
    public virtual void OnUnequip()
    {
        IsEquipped = false;
    }

    /// <summary>
    /// コントローラー側から Digger を注入します。
    /// </summary>
    public void SetDigger(Digger digger)
    {
        this.digger = digger;
    }

    /// <summary>
    /// コントローラー側から Tool の Animator を注入します。
    /// </summary>
    public void SetToolAnimator(Animator animator)
    {
        this.toolAnimator = animator;
    }

    /// <summary>
    /// ツール使用（入力に応じてコントローラーから呼ばれる）
    /// </summary>
    public abstract void Use(Vector3 direction, PlayerController playerController);

    /// <summary>
    /// 照準・向きの更新（プレイヤーの移動/入力から転送される）
    /// </summary>
    public virtual void UpdateAim(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        // 派生クラスで照準の更新を実装
    }

    /// <summary>
    /// 掘削SEを再生する共通処理。
    /// アニメーションイベントから呼び出されることを想定しています。
    /// </summary>
    protected virtual async void PlayMiningSound()
    {
        if (digger != null)
        {
            var (hitBlocks, destroyedVoxelCount) = await digger.ExecuteDigFromAnimation();

            AudioClip soundToPlay = null;

            // ヒットしたブロックがあれば、その素材に応じた音を取得
            if (hitBlocks.Count > 0)
            {
                // 最初のブロックを代表として音を決定
                var firstBlock = new System.Collections.Generic.List<Block>(hitBlocks)[0];
                if (firstBlock != null)
                {
                    var representativeData = firstBlock.GetRepresentativeBlockData();
                    if (representativeData != null)
                    {
                        var materialType = representativeData.materialType;
                        soundToPlay = ToolData.GetMiningSound(materialType);
                    }
                }
            }
            
            // 再生する音がまだ決まっていない場合（空振りなど）、デフォルトの音を使用
            if (soundToPlay == null)
            {
                soundToPlay = ToolData.DefaultMiningSound;
            }

            // AudioManagerに再生を依頼
            AudioManager.Instance.PlayDiggingSE(soundToPlay, hitBlocks.Count, ToolData.Volume);
        }
        else
        {
            Debug.LogError("Digger is not set on MiningToolBehaviour. Cannot play mining sound.");
        }
    }
}
