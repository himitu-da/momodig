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

    /// <summary>現在の役割（Main/Sub）</summary>
    public ToolActionRole Role { get; private set; } = ToolActionRole.Main;

    private Renderer[] cachedRenderers;
    private bool subUseDisplayActive;

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
        IsEquipped = true;
        ApplyVisibilityForCurrentRole();
    }

    /// <summary>
    /// 脱着時に呼ばれます。
    /// </summary>
    public virtual void OnUnequip()
    {
        IsEquipped = false;
        subUseDisplayActive = false;
        SetRenderersVisible(false);
    }

    /// <summary>
    /// このBehaviourの役割（Main/Sub）を設定します。
    /// 役割によって可視性ポリシーが切り替わります:
    ///   Main: 常時表示
    ///   Sub : Use中のみ表示（既定では非表示）
    /// </summary>
    public void SetRole(ToolActionRole role)
    {
        Role = role;
        ApplyVisibilityForCurrentRole();
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
    /// 照準・向きの更新（プレイヤーの移動/入力から転送される）。
    /// 既定で 8 方向の見た目を更新する RenderForDirection を呼び出します。
    /// </summary>
    public virtual void UpdateAim(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            RenderForDirection(direction.normalized, moveMode);
        }
    }

    /// <summary>
    /// 8方向の見た目を更新する責務。
    /// すべての MiningToolBehaviour は Main 役割で動ける必要があるため、必ず実装します。
    /// </summary>
    protected abstract void RenderForDirection(Vector3 direction, PlayerController.MoveMode moveMode);

    /// <summary>
    /// Sub 役割における Use 表示の開始。派生クラスが Use 演出開始時に呼びます。
    /// Main 役割では何も起こりません（常時表示のため）。
    /// </summary>
    protected void BeginUseDisplay()
    {
        if (Role != ToolActionRole.Sub)
        {
            return;
        }

        subUseDisplayActive = true;
        SetRenderersVisible(true);
    }

    /// <summary>
    /// Sub 役割における Use 表示の終了。派生クラスが Use 演出終了時に呼びます。
    /// </summary>
    protected void EndUseDisplay()
    {
        if (Role != ToolActionRole.Sub)
        {
            return;
        }

        subUseDisplayActive = false;
        SetRenderersVisible(false);
    }

    private void ApplyVisibilityForCurrentRole()
    {
        switch (Role)
        {
            case ToolActionRole.Main:
                SetRenderersVisible(true);
                break;
            case ToolActionRole.Sub:
                SetRenderersVisible(subUseDisplayActive);
                break;
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }
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
