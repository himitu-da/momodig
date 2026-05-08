using UnityEngine;
using System.Collections.Generic;

public class MiningToolsController : MonoBehaviour
{
    [Header("掘削ツール設定")]
    [SerializeField] private List<MiningTool> usableMiningTools;
    [SerializeField] private ToolInventory toolInventory;
    [SerializeField] private MiningTool _mainMiningTool;
    [SerializeField] private MiningTool _subMiningTool; // サブ用ツール

    [Header("Digger Prefabs")]
    [SerializeField] private GameObject _mainDiggerPrefab;  // MainDiggerのPrefab
    [SerializeField] private GameObject _subDiggerPrefab;   // SubDiggerのPrefab

    [Header("ツールの装着先(未指定なら自身)")]
    [SerializeField] private Transform toolMount;

    // Behaviour 駆動用キャッシュと参照
    private readonly Dictionary<MiningTool, MiningToolBehaviour> _mainBehaviourCache = new Dictionary<MiningTool, MiningToolBehaviour>();
    private readonly Dictionary<MiningTool, MiningToolBehaviour> _subBehaviourCache = new Dictionary<MiningTool, MiningToolBehaviour>();
    private MiningToolBehaviour _mainBehaviour;
    private MiningToolBehaviour _subBehaviour;

    // 各ツール用の独立したDigger
    private Digger _mainDigger;
    private Digger _subDigger;

    // 外部参照用（読み取りのみ）
    public MiningTool mainMiningTool => _mainMiningTool;
    public MiningTool subMiningTool => _subMiningTool;
    public ToolInventory ToolInventory => toolInventory;

    private PlayerController _playerController; // PlayerControllerへの参照
    private Vector3 _currentDirection = Vector3.right;
    private bool isSubscribedToToolInventory;
    private bool hasAwakened;

    private void OnEnable()
    {
        GameDataPersistenceManager.OnPurchasedItemsChanged += ApplyEnhancements;
        if (hasAwakened)
        {
            SubscribeToToolInventory();
        }
    }

    private void OnDisable()
    {
        GameDataPersistenceManager.OnPurchasedItemsChanged -= ApplyEnhancements;
        UnsubscribeFromToolInventory();
    }

    private void ResolveToolInventory()
    {
        if (toolInventory != null)
        {
            return;
        }

        toolInventory = GetComponent<ToolInventory>();
        if (toolInventory == null)
        {
            toolInventory = gameObject.AddComponent<ToolInventory>();
        }
    }

    private void InitializeToolInventoryFromLegacySettings()
    {
        if (toolInventory == null)
        {
            return;
        }

        toolInventory.EnsureInitializedFromTools(BuildFallbackToolList(), _mainMiningTool, _subMiningTool);
    }

    private List<MiningTool> BuildFallbackToolList()
    {
        List<MiningTool> tools = new List<MiningTool>();

        if (usableMiningTools != null)
        {
            foreach (MiningTool tool in usableMiningTools)
            {
                AddToolIfMissing(tools, tool);
            }
        }

        AddToolIfMissing(tools, _mainMiningTool);
        AddToolIfMissing(tools, _subMiningTool);

        return tools;
    }

    private static void AddToolIfMissing(List<MiningTool> tools, MiningTool tool)
    {
        if (tool != null && !tools.Contains(tool))
        {
            tools.Add(tool);
        }
    }

    private void SubscribeToToolInventory()
    {
        if (toolInventory == null || isSubscribedToToolInventory)
        {
            return;
        }

        toolInventory.OnSlotsChanged += HandleToolInventoryChanged;
        toolInventory.OnRoleBindingsChanged += HandleToolInventoryChanged;
        isSubscribedToToolInventory = true;
    }

    private void UnsubscribeFromToolInventory()
    {
        if (toolInventory == null || !isSubscribedToToolInventory)
        {
            return;
        }

        toolInventory.OnSlotsChanged -= HandleToolInventoryChanged;
        toolInventory.OnRoleBindingsChanged -= HandleToolInventoryChanged;
        isSubscribedToToolInventory = false;
    }

    private void HandleToolInventoryChanged()
    {
        ApplyEnhancements();
        SyncEquippedToolsFromInventory();
    }

    private void SyncEquippedToolsFromInventory()
    {
        MiningTool mainTool = toolInventory != null ? toolInventory.MainTool : _mainMiningTool;
        MiningTool subTool = toolInventory != null ? toolInventory.SubTool : _subMiningTool;

        if (_mainMiningTool != mainTool || (_mainBehaviour == null && mainTool != null))
        {
            EquipMain(mainTool, this.gameObject);
        }

        if (_subMiningTool != subTool || (_subBehaviour == null && subTool != null))
        {
            EquipSub(subTool, this.gameObject, false);
        }
    }

    private void Awake()
    {
        ResolveToolInventory();
        InitializeToolInventoryFromLegacySettings();

        ApplyEnhancements(); // 初期化時に適用

        if (toolMount == null) toolMount = this.transform;

        // 親オブジェクト（Player）からPlayerControllerを取得
        _playerController = GetComponentInParent<PlayerController>();
        if (_playerController == null)
        {
            Debug.LogError("PlayerControllerが見つかりません。");
        }

        // MainDiggerとSubDiggerをPrefabからインスタンス化
        if (_mainDiggerPrefab != null)
        {
            GameObject mainDiggerObj = Instantiate(_mainDiggerPrefab, toolMount);
            mainDiggerObj.name = "MainDigger";
            _mainDigger = mainDiggerObj.GetComponent<Digger>();
            if (_mainDigger == null)
            {
                Debug.LogError("MainDiggerPrefab does not have a Digger component.");
            }
        }
        else
        {
            Debug.LogWarning("MainDiggerPrefab is not assigned.");
        }

        if (_subDiggerPrefab != null)
        {
            GameObject subDiggerObj = Instantiate(_subDiggerPrefab, toolMount);
            subDiggerObj.name = "SubDigger";
            _subDigger = subDiggerObj.GetComponent<Digger>();
            if (_subDigger == null)
            {
                Debug.LogError("SubDiggerPrefab does not have a Digger component.");
            }
        }
        else
        {
            Debug.LogWarning("SubDiggerPrefab is not assigned.");
        }

        // 初期装備
        SyncEquippedToolsFromInventory();
        hasAwakened = true;
        SubscribeToToolInventory();
    }

    /// <summary>
    /// メイン用ツールを使用（Behaviour に委譲）
    /// </summary>
    public void UseMainMineTool(GameObject user, Vector3 direction)
    {
        if (_mainBehaviour != null)
        {
            // UseメソッドにPlayerControllerを渡して、コールバックを可能にする
            _mainBehaviour.Use(direction, _playerController);
        }
        else
        {
            Debug.LogWarning("No main mining tool behaviour is equipped.");
        }
    }

    /// <summary>
    /// サブ用ツールを使用（Behaviour に委譲）
    /// </summary>
    public void UseSubMineTool(GameObject user, Vector3 direction)
    {
        if (_subBehaviour != null)
        {
            // こちらも同様にPlayerControllerを渡す（もしサブツールも同期させるなら）
            _subBehaviour.Use(direction, _playerController);
        }
        else
        {
            Debug.LogWarning("No sub mining tool behaviour is equipped.");
        }
    }

    /// <summary>
    /// ツールの向き・照準更新（Behaviour に転送）
    /// </summary>
    public void UpdateRotation(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            _currentDirection = direction;
        }

        // メインツールの更新
        if (_mainBehaviour != null)
        {
            // 照準の更新は常に行う
            _mainBehaviour.UpdateAim(direction, moveMode);

            // ツール自体の回転は PickaxeToolBehaviour 側で制御するため、ここからは削除
            // if (!_mainBehaviour.IsMining)
            // {
            //     if (direction.sqrMagnitude > 0.1f)
            //     {
            //         UpdateToolRotation(direction, moveMode);
            //     }
            // }
        }

        // サブツールの更新
        if (_subBehaviour != null)
        {
            // サブツールも照準更新は常に行う
            _subBehaviour.UpdateAim(direction, moveMode);
        }
    }

    /// <summary>
    /// ツールホルダー自体の向きを更新する
    /// </summary>
    private void UpdateToolRotation(Vector3 direction, PlayerController.MoveMode moveMode)
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

    /// <summary>
    /// 現在のメインツールのAnimationTriggerNameを取得する
    /// </summary>
    public string GetCurrentToolTriggerName()
    {
        return _mainMiningTool != null ? _mainMiningTool.AnimationTriggerName : string.Empty;
    }

    /// <summary>
    /// 現在のメインツールのAnimationStateNameを取得する
    /// </summary>
    public string GetCurrentToolStateName()
    {
        return _mainMiningTool != null ? _mainMiningTool.AnimationStateName : string.Empty;
    }

    /// <summary>
    /// 外部からメインツールを切り替える API
    /// </summary>
    public void SetMainTool(MiningTool tool)
    {
        if (toolInventory != null && !string.IsNullOrEmpty(toolInventory.MainSlotId) &&
            toolInventory.SetSlotTool(toolInventory.MainSlotId, tool))
        {
            SyncEquippedToolsFromInventory();
            return;
        }

        EquipMain(tool, this.gameObject);
    }

    /// <summary>
    /// 外部からサブツールを切り替える API
    /// </summary>
    public void SetSubTool(MiningTool tool, bool active = true)
    {
        if (toolInventory != null && !string.IsNullOrEmpty(toolInventory.SubSlotId) &&
            toolInventory.SetSlotTool(toolInventory.SubSlotId, tool))
        {
            SyncEquippedToolsFromInventory();
            return;
        }

        EquipSub(tool, this.gameObject, active);
    }

    public bool BindMainSlot(string slotId)
    {
        bool bound = toolInventory != null && toolInventory.BindSlotToRole(slotId, ToolActionRole.Main);
        if (bound)
        {
            SyncEquippedToolsFromInventory();
        }

        return bound;
    }

    public bool BindSubSlot(string slotId)
    {
        bool bound = toolInventory != null && toolInventory.BindSlotToRole(slotId, ToolActionRole.Sub);
        if (bound)
        {
            SyncEquippedToolsFromInventory();
        }

        return bound;
    }

    public bool MoveToolSlot(string fromSlotId, string toSlotId, bool swapIfOccupied = true)
    {
        bool moved = toolInventory != null && toolInventory.MoveTool(fromSlotId, toSlotId, swapIfOccupied);
        if (moved)
        {
            SyncEquippedToolsFromInventory();
        }

        return moved;
    }

    public bool SwapToolSlots(string firstSlotId, string secondSlotId)
    {
        bool swapped = toolInventory != null && toolInventory.SwapSlots(firstSlotId, secondSlotId);
        if (swapped)
        {
            SyncEquippedToolsFromInventory();
        }

        return swapped;
    }

    /// <summary>
    /// Behaviour を生成 or キャッシュから取得
    /// </summary>
    private MiningToolBehaviour GetOrCreateBehaviour(MiningTool tool, Dictionary<MiningTool, MiningToolBehaviour> cache)
    {
        if (tool == null) return null;
        if (cache.TryGetValue(tool, out var cached) && cached != null)
        {
            return cached;
        }

        var behaviour = tool.InstantiateBehaviour(toolMount);
        if (behaviour != null)
        {
            behaviour.gameObject.name = tool.name; // ツール名を設定
            behaviour.gameObject.SetActive(false);
            cache[tool] = behaviour;
        }
        return behaviour;
    }

    /// <summary>
    /// メイン装備処理（旧来の SO.Use ではなく Behaviour を装備）
    /// </summary>
    public void EquipMain(MiningTool tool, GameObject user)
    {
        // 既存のメインを外す
        if (_mainBehaviour != null)
        {
            _mainBehaviour.OnUnequip();
            _mainBehaviour.gameObject.SetActive(false);
        }

        _mainMiningTool = tool;
        _mainBehaviour = GetOrCreateBehaviour(tool, _mainBehaviourCache);
        if (_mainBehaviour != null)
        {
            _mainBehaviour.SetToolAnimator(_mainBehaviour.GetComponent<Animator>()); // ToolのAnimatorを注入
            _mainBehaviour.SetDigger(_mainDigger);  // MainDiggerを渡す
            _mainBehaviour.gameObject.SetActive(true);
            _mainBehaviour.OnEquip(user);
        }

        // PlayerControllerにToolTypeIDを通知
        if (_playerController != null)
        {
            int toolId = _mainMiningTool != null ? _mainMiningTool.ToolTypeID : 0;
            _playerController.SetToolAnimationType(toolId);
        }
    }

    /// <summary>
    /// サブ装備処理（必要なら非表示で装備）
    /// </summary>
    public void EquipSub(MiningTool tool, GameObject user, bool active = true)
    {
        if (_subBehaviour != null)
        {
            _subBehaviour.OnUnequip();
            _subBehaviour.gameObject.SetActive(false);
        }

        _subMiningTool = tool;
        _subBehaviour = GetOrCreateBehaviour(tool, _subBehaviourCache);
        if (_subBehaviour != null)
        {
            _subBehaviour.SetToolAnimator(_subBehaviour.GetComponent<Animator>()); // ToolのAnimatorを注入
            _subBehaviour.SetDigger(_subDigger);  // SubDiggerを渡す
            _subBehaviour.gameObject.SetActive(active);
            _subBehaviour.OnEquip(user);
            if (!active) _subBehaviour.gameObject.SetActive(false);
        }
    }

    // --- Animation Event Relays ---
    // PlayerControllerから呼び出され、現在アクティブなツールのBehaviourに処理を中継する。
    // ↑ このセクションは不要になったため削除

    private List<MiningTool> GetEnhancementTargetTools()
    {
        List<MiningTool> tools = BuildFallbackToolList();

        if (toolInventory != null)
        {
            foreach (MiningTool tool in toolInventory.GetAllTools())
            {
                AddToolIfMissing(tools, tool);
            }
        }

        return tools;
    }

    public void ApplyEnhancements()
    {
        List<MiningTool> enhancementTargets = GetEnhancementTargetTools();
        if (enhancementTargets.Count == 0) return;

        // 全ての利用可能なツールのステータスをリセット
        foreach (var tool in enhancementTargets)
        {
            if (tool.miningModule != null)
            {
                ResetMiningModuleStats(tool.miningModule);
            }
        }

        var purchasedItems = GameDataPersistenceManager.Instance.purchaseditems;
        foreach (var item in purchasedItems)
        {
            ItemData itemData = item.Key;
            int level = item.Value;

            if (level == 0) continue;

            foreach (var enhancement in itemData.enhancements)
            {
                // どのツールのステータスを強化するかを判断する必要がある
                // ここでは、全ツールに対して適用を試みる
                foreach (var tool in enhancementTargets)
                {
                    // Enhancementに設定されたTargetCategoryと現在のtool名が一致する場合のみ適用
                    if (enhancement.TargetCategory == tool.name && tool.miningModule != null)
                    {
                        ApplyEnhancementToModule(tool.miningModule, enhancement, level);
                    }
                }
            }
        }
    }

    private void ResetMiningModuleStats(MiningModule module)
    {
        module.DamagePerHit.RemoveAllModifiers();
        module.DiggingSize.RemoveAllModifiers();

        if (module is DynamiteMiningModule dynamiteModule)
        {
            dynamiteModule.ThrowForce.RemoveAllModifiers();
            dynamiteModule.MaxThrowDistance.RemoveAllModifiers();
            dynamiteModule.ExplosionForce.RemoveAllModifiers();
        }
        else if (module is PickaxeMiningModule pickaxeModule)
        {
            pickaxeModule.MiningForce.RemoveAllModifiers();
            pickaxeModule.VerticalMiningForce.RemoveAllModifiers();
            pickaxeModule.VerticalDiggingSize.RemoveAllModifiers();
        }
    }

    private void ApplyEnhancementToModule(MiningModule module, Enhancement enhancement, int level)
    {
        Stat targetStat = null;

        // Common stats
        if (enhancement.TargetStatName == "DamagePerHit") targetStat = module.DamagePerHit;
        
        // Dynamite-specific stats
        if (module is DynamiteMiningModule dynamiteModule)
        {
            switch (enhancement.TargetStatName)
            {
                case "ThrowForce":
                    targetStat = dynamiteModule.ThrowForce;
                    break;
                case "MaxThrowDistance":
                    targetStat = dynamiteModule.MaxThrowDistance;
                    break;
                case "ExplosionForce":
                    targetStat = dynamiteModule.ExplosionForce;
                    break;
            }
        }
        // Pickaxe-specific stats
        else if (module is PickaxeMiningModule pickaxeModule)
        {
            switch (enhancement.TargetStatName)
            {
                case "MiningForce":
                    targetStat = pickaxeModule.MiningForce;
                    break;
                case "VerticalMiningForce":
                    targetStat = pickaxeModule.VerticalMiningForce;
                    break;
            }
        }
        
        // Vector3 stats need special handling
        if (enhancement.TargetStatName == "DiggingSize.X") targetStat = module.DiggingSize.X;
        if (enhancement.TargetStatName == "DiggingSize.Y") targetStat = module.DiggingSize.Y;
        if (enhancement.TargetStatName == "DiggingSize.Z") targetStat = module.DiggingSize.Z;

        if (module is PickaxeMiningModule pickaxeModuleForSize)
        {
            if (enhancement.TargetStatName == "VerticalDiggingSize.X") targetStat = pickaxeModuleForSize.VerticalDiggingSize.X;
            if (enhancement.TargetStatName == "VerticalDiggingSize.Y") targetStat = pickaxeModuleForSize.VerticalDiggingSize.Y;
            if (enhancement.TargetStatName == "VerticalDiggingSize.Z") targetStat = pickaxeModuleForSize.VerticalDiggingSize.Z;
        }


        if (targetStat != null)
        {
            ApplyModifier(targetStat, enhancement, level);
        }
    }

    private void ApplyModifier(Stat stat, Enhancement enhancement, int level)
    {
        if (enhancement.Type == EnhancementType.Additive)
        {
            stat.AddAdditiveModifier(level * enhancement.Value);
        }
        else if (enhancement.Type == EnhancementType.Multiplicative)
        {
            stat.AddMultiplicativeModifier(Mathf.Pow(enhancement.Value, level));
        }
    }
}
