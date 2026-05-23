using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Unity.Profiling;

// トロッコ管理クラス
public class MinecartManager : MonoBehaviour
{
    private static readonly ProfilerMarker UpdateCapacityUIMarker = new ProfilerMarker("MinecartManager.UpdateCapacityUI");

    [Header("プレイヤー設定")]
    public Transform playerTransform; // プレイヤーのTransform

    [Header("トロッコ設定")]
    public GameObject minecartPrefab; // トロッコのプレハブ
    [SerializeField] private FacilityUpgradeCatalog facilityUpgradeCatalog;
    public Stat CartCapacity = new Stat { BaseValue = 500 };

    [Header("UI設定")]
    public GameObject minecartCapacityUIPrefab; // UIプレハブ
    public Transform worldCanvasTransform; // UIを配置するCanvas
    [SerializeField] private Camera uiWorldCamera; // カート位置をUI座標へ変換するCamera
    public Vector3 uiOffset; // UIのオフセット
    [Header("トロッコ移動設定")]
    public Vector3 groundStationPosition = Vector3.zero; // 地上の停留点
    public Stat followMoveSpeed = new Stat { BaseValue = 5f }; // プレイヤー追従時の速度
    public Stat groundMoveSpeed = new Stat { BaseValue = 10f }; // 地上へ向かう際の速度
    public Stat unloadTime = new Stat { BaseValue = 2.0f }; // 地上での荷降ろし時間
    public Stat cartunit = new Stat { BaseValue = 2 };
    public List<Minecart> minecarts = new List<Minecart>();

    [Header("軌跡追従設定")]
    [Tooltip("軌跡を記録する最小移動距離（格子点の間隔）")]
    public float pathRecordInterval = 1.0f;
    [Tooltip("トロッコ間の距離（軌跡点の数）")]
    public int cartDistanceInPoints = 5;
    [Tooltip("記録する軌跡の最大数")]
    public int maxPathPoints = 100;
    private List<Vector3> pathPoints = new List<Vector3>();

    [Header("トロッコ状態")]
    public bool digable = true;

    private void OnEnable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged += ApplyEnhancements;
    }

    private void OnDisable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged -= ApplyEnhancements;
    }

    void Start()
    {
        ApplyEnhancements(); // 初期化時に適用
        // トロッコを必要数まで生成
        while (minecarts.Count < cartunit.IntValue)
        {
            addnewcart();
        }
        // 軌跡記録の初期化
        if (playerTransform != null)
        {
            pathPoints.Add(playerTransform.position);
        }
    }

    // 新しいトロッコを追加
    public void addnewcart()
    {
        if (minecartPrefab != null)
        {
            GameObject newMinecartObject = Instantiate(minecartPrefab, Vector3.zero, Quaternion.identity, transform);
            MinecartMovement movement = newMinecartObject.GetComponent<MinecartMovement>();
            if (movement == null)
            {
                movement = newMinecartObject.AddComponent<MinecartMovement>();
            }
            movement.moveSpeed = followMoveSpeed.Value;

            Minecart newMinecart = new Minecart(newMinecartObject);

            if (TryCreateCapacityUI(newMinecart))
            {
                UpdateCapacityUI(newMinecart);
            }

            minecarts.Add(newMinecart);
        }
        else
        {
            Debug.LogError("minecartPrefabが設定されていません！");
        }
    }

    public bool AddItemToFrontCart(VoxelItemData itemData)
    {
        if (itemData == null || !itemData.IsValid("MinecartManager.AddItemToFrontCart"))
        {
            return false;
        }

        if (minecarts == null || minecarts.Count == 0)
        {
            Debug.LogError("MinecartManager: no minecart is available.");
            return false;
        }

        int currentCart = 0;
        if (!minecarts[currentCart].AddItem(itemData, CartCapacity.IntValue))
        {
            return false;
        }

        if (minecarts[currentCart].CurrentLoad >= CartCapacity.IntValue)
        {
            SendCartToHome(currentCart);
        }

        return true;
    }

    // トロッコを地上(0,0,0)に送り、キューを進める
    private void SendCartToHome(int minecartnum)
    {
        Minecart cart = minecarts[minecartnum];
        if (cart.gameObject != null && minecartnum == 0) // 先頭のトロッコのみ処理
        {
            cart.state = MinecartState.GoingToGround; // 状態を地上へ移動中に変更
            cart.movement.targetPosition = groundStationPosition; // 地上の停留点に移動
            cart.movement.moveSpeed = groundMoveSpeed.Value; // 地上への移動速度を設定

            // 使用済みのトロッコをリストの末尾に移動（キューの末尾へ）
            Minecart movedCart = minecarts[0];
            minecarts.RemoveAt(0);
            minecarts.Add(movedCart);

            // 次のトロッコ（新しい先頭）がすぐに利用可能
            digable = true;
        }
    }

    // トロッコの位置を更新する
    public void UpdateMinecartPositions()
    {
        if (pathPoints.Count == 0) return;

        for (int i = 0; i < minecarts.Count; i++)
        {
            Minecart cart = minecarts[i];
            // 追従状態のトロッコのみ位置を更新
            if (cart.state == MinecartState.Following && cart.gameObject != null && cart.movement != null)
            {
                // 各トロッコの目標となる軌跡リスト上のインデックスを計算
                int targetIndex = pathPoints.Count - 1 - (cartDistanceInPoints * (i + 1));

                // インデックスが範囲外にならないように調整
                if (targetIndex < 0)
                {
                    targetIndex = 0;
                }

                Vector3 targetPosition = pathPoints[targetIndex];
                cart.movement.targetPosition = targetPosition;
            }
        }
    }

    // 互換用オーバーロード（旧呼び出しに対応）
    // 旧API: プレイヤー位置・最後の移動方向・オフセットを受け取る
    // 実装: プレイヤー位置を軌跡に取り込み、軌跡ベースの追従ロジックへ委譲
    public void UpdateMinecartPositions(Vector3 playerPosition, Vector3 playerLastMoveDirection, float offset)
    {
        // 軌跡初期化または追記
        if (pathPoints.Count == 0)
        {
            pathPoints.Add(playerPosition);
        }
        else
        {
            if (Vector3.Distance(playerPosition, pathPoints[pathPoints.Count - 1]) > pathRecordInterval)
            {
                pathPoints.Add(playerPosition);
                if (pathPoints.Count > maxPathPoints)
                {
                    pathPoints.RemoveAt(0);
                }
            }
        }

        // 軌跡に基づいて各トロッコの目標位置を更新
        UpdateMinecartPositions();
    }

    void Update()
    {
        // プレイヤーの軌跡を記録
        if (playerTransform != null)
        {
            if (pathPoints.Count > 0)
            {
                float distance = Vector3.Distance(playerTransform.position, pathPoints[pathPoints.Count - 1]);
                if (distance > pathRecordInterval)
                {
                    pathPoints.Add(playerTransform.position);
                    // 軌跡リストが最大数を超えたら古いものから削除
                    if (pathPoints.Count > maxPathPoints)
                    {
                        pathPoints.RemoveAt(0);
                    }
                }
            }
            else
            {
                pathPoints.Add(playerTransform.position);
            }
        }

        // トロッコの位置を更新
        UpdateMinecartPositions();

        // トロッコの状態とUIを更新
        foreach (Minecart cart in minecarts)
        {
            UpdateCapacityUI(cart);

            switch (cart.state)
            {
                case MinecartState.GoingToGround:
                    // 地上到着チェック
                    if (Vector3.Distance(cart.gameObject.transform.position, groundStationPosition) < 0.1f)
                    {
                        cart.state = MinecartState.Unloading; // 状態を荷降ろし中に変更
                        cart.time = unloadTime.Value; // 荷降ろしタイマーを設定

                        if (StorageManager.Instance != null)
                        {
                            if (!cart.TryDrainItems(out List<VoxelItemData> unloadedItems))
                            {
                                Debug.LogError("MinecartManager: failed to unload minecart because it contains invalid voxel item data.");
                            }
                            else if (unloadedItems.Count > 0 &&
                                     VoxelItemData.TryAggregateResourceCounts(unloadedItems, out Dictionary<ResourceType, int> resourceCounts, "MinecartManager.Unload"))
                            {
                                StorageManager.Instance.AddResources(resourceCounts);
                            }
                            else if (unloadedItems.Count > 0)
                            {
                                Debug.LogError("MinecartManager: failed to unload minecart because it contains invalid voxel item data.");
                            }
                        }
                    }
                    break;

                case MinecartState.Unloading:
                    // 荷降ろしタイマーを減らす
                    if (cart.time > 0)
                    {
                        cart.time -= Time.deltaTime;
                    }
                    else
                    {
                        cart.state = MinecartState.Following; // 状態を追従中に変更
                        cart.movement.moveSpeed = followMoveSpeed.Value; // 追従速度に戻す
                    }
                    break;

                case MinecartState.Following:
                    // 追従中の処理はUpdateMinecartPositionsで行う
                    break;
            }
        }

        // 採掘可能かどうかの判定
        if (minecarts.Count > 0 && minecarts[0].state == MinecartState.Following)
        {
            digable = true;
        }
        else
        {
            digable = false;
        }
    }

    private bool TryCreateCapacityUI(Minecart minecart)
    {
        if (minecart == null)
        {
            Debug.LogError("MinecartManager: cannot create capacity UI because minecart is null.", this);
            return false;
        }

        if (minecartCapacityUIPrefab == null)
        {
            Debug.LogError("MinecartManager: minecartCapacityUIPrefab is not configured.", this);
            return false;
        }

        if (!TryGetConfiguredCanvas(out Canvas canvas, out _))
        {
            return false;
        }

        GameObject uiObject = Instantiate(minecartCapacityUIPrefab, worldCanvasTransform);
        TextMeshProUGUI capacityText = uiObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (capacityText == null)
        {
            Debug.LogError(
                $"MinecartManager: minecartCapacityUIPrefab '{minecartCapacityUIPrefab.name}' does not contain a TextMeshProUGUI.",
                this);
            Destroy(uiObject);
            return false;
        }

        if (canvas.renderMode != RenderMode.WorldSpace && uiWorldCamera == null)
        {
            Debug.LogError("MinecartManager: uiWorldCamera is not configured for screen-space minecart capacity UI.", this);
            Destroy(uiObject);
            return false;
        }

        minecart.capacityText = capacityText;
        return true;
    }

    private void UpdateCapacityUI(Minecart cart)
    {
        using (UpdateCapacityUIMarker.Auto())
        {
            if (cart == null || cart.capacityText == null)
            {
                return;
            }

            if (cart.gameObject == null)
            {
                Debug.LogError("MinecartManager: minecart gameObject is null while updating capacity UI.", this);
                cart.capacityText.gameObject.SetActive(false);
                return;
            }

            if (!cart.gameObject.activeSelf)
            {
                cart.capacityText.gameObject.SetActive(false);
                return;
            }

            if (!TryGetConfiguredCanvas(out Canvas canvas, out RectTransform canvasRect))
            {
                cart.capacityText.gameObject.SetActive(false);
                return;
            }

            Vector3 worldPosition = cart.gameObject.transform.position + uiOffset;
            if (!TrySetCapacityUIPosition(cart.capacityText.rectTransform, canvas, canvasRect, worldPosition))
            {
                cart.capacityText.gameObject.SetActive(false);
                return;
            }

            cart.capacityText.gameObject.SetActive(true);
            cart.capacityText.SetText("{0} / {1}", cart.CurrentLoad, CartCapacity.IntValue);
        }
    }

    private bool TryGetConfiguredCanvas(out Canvas canvas, out RectTransform canvasRect)
    {
        canvas = null;
        canvasRect = null;

        if (worldCanvasTransform == null)
        {
            Debug.LogError("MinecartManager: worldCanvasTransform is not configured.", this);
            return false;
        }

        canvas = worldCanvasTransform.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MinecartManager: worldCanvasTransform must be assigned under a Canvas.", this);
            return false;
        }

        canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            Debug.LogError("MinecartManager: configured Canvas does not have a RectTransform.", this);
            return false;
        }

        return true;
    }

    private bool TrySetCapacityUIPosition(RectTransform uiRect, Canvas canvas, RectTransform canvasRect, Vector3 worldPosition)
    {
        if (uiRect == null)
        {
            Debug.LogError("MinecartManager: capacity UI RectTransform is null.", this);
            return false;
        }

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            uiRect.position = worldPosition;
            if (uiWorldCamera != null)
            {
                uiRect.rotation = uiWorldCamera.transform.rotation;
            }

            return true;
        }

        if (uiWorldCamera == null)
        {
            Debug.LogError("MinecartManager: uiWorldCamera is not configured.", this);
            return false;
        }

        Vector3 viewportPosition = uiWorldCamera.WorldToViewportPoint(worldPosition);
        if (viewportPosition.z <= 0f)
        {
            return false;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiWorldCamera, worldPosition);
        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 localPoint))
        {
            return false;
        }

        uiRect.SetParent(worldCanvasTransform, false);
        uiRect.anchoredPosition = localPoint;
        return true;
    }

    public void ApplyEnhancements()
    {
        if (!ValidateFacilityUpgradeCatalog())
        {
            return;
        }

        // Statの補正値をリセット
        CartCapacity.RemoveAllModifiers();
        followMoveSpeed.RemoveAllModifiers();
        groundMoveSpeed.RemoveAllModifiers();
        unloadTime.RemoveAllModifiers();
        cartunit.RemoveAllModifiers();

        GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
        IReadOnlyList<FacilityUpgradeDefinition> upgrades = facilityUpgradeCatalog.Upgrades;
        for (int upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
        {
            FacilityUpgradeDefinition upgrade = upgrades[upgradeIndex];
            int level = persistence.GetFacilityUpgradeLevel(upgrade.UpgradeId, upgrade.InitialLevel);
            int effectLevel = upgrade.GetEffectLevel(level);

            if (effectLevel == 0) continue; // レベル0のアイテムは効果なし

            IReadOnlyList<Enhancement> enhancements = upgrade.Enhancements;
            for (int enhancementIndex = 0; enhancementIndex < enhancements.Count; enhancementIndex++)
            {
                Enhancement enhancement = enhancements[enhancementIndex];
                // カテゴリが "Minecart" の場合のみ適用
                if (enhancement.TargetCategory == "Minecart")
                {
                    Stat targetStat = GetStatByName(enhancement.TargetStatName);
                    if (targetStat == null)
                    {
                        Debug.LogError(
                            $"MinecartManager: enhancement '{enhancement.name}' targets unsupported stat '{enhancement.TargetStatName}'.",
                            this);
                        continue;
                    }

                    ApplyModifier(targetStat, enhancement, effectLevel);
                }
            }
        }

        // トロッコ数が変わった可能性があるので更新
        UpdateCartCount();
    }

    private bool ValidateFacilityUpgradeCatalog()
    {
        if (facilityUpgradeCatalog == null)
        {
            Debug.LogError("MinecartManager: facilityUpgradeCatalog is not configured.", this);
            return false;
        }

        return facilityUpgradeCatalog.ValidateConfiguration(this);
    }

    private Stat GetStatByName(string statName)
    {
        switch (statName)
        {
            case "CartCapacity": return CartCapacity;
            case "followMoveSpeed": return followMoveSpeed;
            case "groundMoveSpeed": return groundMoveSpeed;
            case "unloadTime": return unloadTime;
            case "cartunit": return cartunit;
            default: return null;
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
            // 乗算の場合は、基本値1.0に対して補正をかけるのが一般的
            // 例: Value=1.1 (10%増) の場合、(1.1-1.0) * level + 1.0 のような計算が考えられる
            // 今回は累乗で実装
            stat.AddMultiplicativeModifier(Mathf.Pow(enhancement.Value, level));
        }
    }

    private void UpdateCartCount()
    {
        // 現在のトロッコ数が必要数より多い場合は削除
        while (minecarts.Count > cartunit.IntValue)
        {
            Minecart cartToRemove = minecarts[minecarts.Count - 1];
            Destroy(cartToRemove.gameObject);
            if (cartToRemove.capacityText != null)
            {
                Destroy(cartToRemove.capacityText.gameObject);
            }
            minecarts.RemoveAt(minecarts.Count - 1);
        }

        // 現在のトロッコ数が必要数より少ない場合は追加
        while (minecarts.Count < cartunit.IntValue)
        {
            addnewcart();
        }
    }
}
