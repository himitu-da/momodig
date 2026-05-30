using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class OverworldBackgroundController : MonoBehaviour
{
    private static readonly ProfilerMarker BuildBackgroundMarker =
        new ProfilerMarker("OverworldBackgroundController.BuildBackground");

    [Header("Required References")]
    [SerializeField] private Sprite[] backgroundSprites = System.Array.Empty<Sprite>();
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Material tileMaterial;

    [Header("Layout")]
    [SerializeField, Min(1)] private int columns = 1;
    [SerializeField, Min(1)] private int rows = 1;
    [SerializeField] private Vector2 tileSize = Vector2.one;
    [SerializeField] private Vector2 center = Vector2.zero;
    [SerializeField] private float worldZ = 1f;
    [SerializeField] private int backgroundSeed = 101;

    [Header("Decoration")]
    [SerializeField] private DecorationRule[] decorationRules = System.Array.Empty<DecorationRule>();
    [SerializeField] private int decorationSeed = 202;
    [SerializeField, Min(1)] private int decorationColumns = 1;
    [SerializeField, Min(1)] private int decorationRows = 1;
    [SerializeField] private Vector2 decorationCellSize = Vector2.one;
    [SerializeField] private Vector2 decorationCenter = Vector2.zero;
    [SerializeField] private float decorationWorldZ = 0.9f;

    [Header("Rendering")]
    [SerializeField] private RenderQueueLayer renderQueueLayer = RenderQueueLayer.Background;
    [SerializeField] private int renderQueueOffset;
    [SerializeField] private int sortingOrder = -30;
    [SerializeField] private RenderQueueLayer decorationRenderQueueLayer = RenderQueueLayer.Scenery;
    [SerializeField] private int decorationRenderQueueOffset;
    [SerializeField] private int decorationSortingOrder = -10;

    private readonly List<GameObject> generatedTiles = new List<GameObject>();
    private Material runtimeBackgroundMaterial;
    private Material runtimeDecorationMaterial;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        BuildBackground();
    }

    private void OnDestroy()
    {
        ClearGeneratedTiles();
        DestroyRuntimeMaterials();
    }

    [ContextMenu("Rebuild Background")]
    private void RebuildBackground()
    {
        if (!ValidateRequiredReferences())
        {
            return;
        }

        BuildBackground();
    }

    private void BuildBackground()
    {
        using (BuildBackgroundMarker.Auto())
        {
            ClearGeneratedTiles();
            DestroyRuntimeMaterials();
            runtimeBackgroundMaterial = CreateRuntimeMaterial(renderQueueLayer, renderQueueOffset, "Background");
            runtimeDecorationMaterial = CreateRuntimeMaterial(decorationRenderQueueLayer, decorationRenderQueueOffset, "Decoration");

            BuildTiles();
            BuildDecorations();
        }
    }

    private void BuildTiles()
    {
        float startX = center.x - (columns - 1) * tileSize.x * 0.5f;
        float startY = center.y - (rows - 1) * tileSize.y * 0.5f;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector3 position = new Vector3(
                    startX + column * tileSize.x,
                    startY + row * tileSize.y,
                    worldZ
                );

                CreateBackgroundTile(column, row, position);
            }
        }
    }

    private void CreateBackgroundTile(int column, int row, Vector3 position)
    {
        GameObject tile = Instantiate(tilePrefab, position, Quaternion.identity, transform);
        tile.name = $"OverworldBackground_{column}_{row}";
        tile.transform.localScale = Vector3.one;
        generatedTiles.Add(tile);

        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            Debug.LogError("OverworldBackgroundController: tilePrefab must have a SpriteRenderer on its root GameObject.", tile);
            enabled = false;
            return;
        }

        renderer.sprite = SelectBackgroundSprite(column, row);
        renderer.sharedMaterial = runtimeBackgroundMaterial;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = tileSize;
        renderer.sortingOrder = sortingOrder;
    }

    private void BuildDecorations()
    {
        if (decorationRules == null || decorationRules.Length == 0)
        {
            return;
        }

        float startX = decorationCenter.x - (decorationColumns - 1) * decorationCellSize.x * 0.5f;
        float startY = decorationCenter.y - (decorationRows - 1) * decorationCellSize.y * 0.5f;

        for (int row = 0; row < decorationRows; row++)
        {
            for (int column = 0; column < decorationColumns; column++)
            {
                DecorationRule rule = SelectDecorationRule(column, row);
                if (rule == null)
                {
                    continue;
                }

                Vector3 position = new Vector3(
                    startX + column * decorationCellSize.x + rule.Offset.x,
                    startY + row * decorationCellSize.y + rule.Offset.y,
                    decorationWorldZ
                );

                CreateDecoration(column, row, position, rule);
            }
        }
    }

    private void CreateDecoration(int column, int row, Vector3 position, DecorationRule rule)
    {
        GameObject decoration = Instantiate(tilePrefab, position, Quaternion.identity, transform);
        decoration.name = $"OverworldDecoration_{rule.RuleName}_{column}_{row}";
        decoration.transform.localScale = Vector3.one;
        generatedTiles.Add(decoration);

        SpriteRenderer renderer = decoration.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            Debug.LogError("OverworldBackgroundController: tilePrefab must have a SpriteRenderer on its root GameObject.", decoration);
            enabled = false;
            return;
        }

        renderer.sprite = rule.Sprite;
        renderer.sharedMaterial = runtimeDecorationMaterial;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = rule.Size;
        renderer.sortingOrder = decorationSortingOrder + rule.SortingOrderOffset;
    }

    private Sprite SelectBackgroundSprite(int column, int row)
    {
        int index = PositiveModulo((int)Hash(backgroundSeed, column, row, 0), backgroundSprites.Length);
        return backgroundSprites[index];
    }

    private DecorationRule SelectDecorationRule(int column, int row)
    {
        float roll = Random01(decorationSeed, column, row, 0);
        float cursor = 0f;

        for (int i = 0; i < decorationRules.Length; i++)
        {
            DecorationRule rule = decorationRules[i];
            if (rule == null || !rule.Enabled)
            {
                continue;
            }

            cursor += rule.SpawnProbability;
            if (roll < cursor)
            {
                return rule;
            }
        }

        return null;
    }

    private Material CreateRuntimeMaterial(RenderQueueLayer layer, int offset, string label)
    {
        int renderQueue = RenderQueue.Resolve(layer) + offset;
        Material material = new Material(tileMaterial)
        {
            name = $"{tileMaterial.name}_{name}_{label}_{renderQueue}",
            renderQueue = renderQueue
        };

        return material;
    }

    private float Random01(int seed, int column, int row, int salt)
    {
        return (Hash(seed, column, row, salt) & 0x00FFFFFFu) / 16777216f;
    }

    private uint Hash(int seed, int column, int row, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)column) * 16777619u;
            hash = (hash ^ (uint)row) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private void ClearGeneratedTiles()
    {
        for (int i = 0; i < generatedTiles.Count; i++)
        {
            DestroyUnityObject(generatedTiles[i]);
        }

        generatedTiles.Clear();
    }

    private void DestroyRuntimeMaterials()
    {
        if (runtimeBackgroundMaterial != null)
        {
            DestroyUnityObject(runtimeBackgroundMaterial);
            runtimeBackgroundMaterial = null;
        }

        if (runtimeDecorationMaterial != null)
        {
            DestroyUnityObject(runtimeDecorationMaterial);
            runtimeDecorationMaterial = null;
        }
    }

    private void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
        {
            Debug.LogError("OverworldBackgroundController: backgroundSprites are not configured.", this);
            isValid = false;
        }
        else
        {
            for (int i = 0; i < backgroundSprites.Length; i++)
            {
                if (backgroundSprites[i] == null)
                {
                    Debug.LogError($"OverworldBackgroundController: backgroundSprites[{i}] is not configured.", this);
                    isValid = false;
                }
            }
        }

        if (tilePrefab == null)
        {
            Debug.LogError("OverworldBackgroundController: tilePrefab is not configured.", this);
            isValid = false;
        }
        else if (tilePrefab.GetComponent<SpriteRenderer>() == null)
        {
            Debug.LogError("OverworldBackgroundController: tilePrefab must have a SpriteRenderer on its root GameObject.", tilePrefab);
            isValid = false;
        }

        if (tileMaterial == null)
        {
            Debug.LogError("OverworldBackgroundController: tileMaterial is not configured.", this);
            isValid = false;
        }

        if (columns <= 0)
        {
            Debug.LogError("OverworldBackgroundController: columns must be greater than 0.", this);
            isValid = false;
        }

        if (rows <= 0)
        {
            Debug.LogError("OverworldBackgroundController: rows must be greater than 0.", this);
            isValid = false;
        }

        if (tileSize.x <= 0f || tileSize.y <= 0f)
        {
            Debug.LogError("OverworldBackgroundController: tileSize must be greater than 0.", this);
            isValid = false;
        }

        if (!System.Enum.IsDefined(typeof(RenderQueueLayer), renderQueueLayer))
        {
            Debug.LogError("OverworldBackgroundController: renderQueueLayer is invalid.", this);
            isValid = false;
        }

        if (!System.Enum.IsDefined(typeof(RenderQueueLayer), decorationRenderQueueLayer))
        {
            Debug.LogError("OverworldBackgroundController: decorationRenderQueueLayer is invalid.", this);
            isValid = false;
        }

        isValid &= ValidateDecorationSettings();

        return isValid;
    }

    private bool ValidateDecorationSettings()
    {
        bool isValid = true;

        if (decorationColumns <= 0)
        {
            Debug.LogError("OverworldBackgroundController: decorationColumns must be greater than 0.", this);
            isValid = false;
        }

        if (decorationRows <= 0)
        {
            Debug.LogError("OverworldBackgroundController: decorationRows must be greater than 0.", this);
            isValid = false;
        }

        if (decorationCellSize.x <= 0f || decorationCellSize.y <= 0f)
        {
            Debug.LogError("OverworldBackgroundController: decorationCellSize must be greater than 0.", this);
            isValid = false;
        }

        if (decorationRules == null || decorationRules.Length == 0)
        {
            return isValid;
        }

        float totalProbability = 0f;
        for (int i = 0; i < decorationRules.Length; i++)
        {
            DecorationRule rule = decorationRules[i];
            if (rule == null)
            {
                Debug.LogError($"OverworldBackgroundController: decorationRules[{i}] is not configured.", this);
                isValid = false;
                continue;
            }

            if (!rule.Enabled)
            {
                continue;
            }

            isValid &= rule.Validate(i, this);
            totalProbability += rule.SpawnProbability;
        }

        if (totalProbability > 1f)
        {
            Debug.LogError("OverworldBackgroundController: enabled decoration spawn probabilities must total 1.0 or less.", this);
            isValid = false;
        }

        return isValid;
    }

    [System.Serializable]
    private sealed class DecorationRule
    {
        [SerializeField] private string ruleName = "Decoration";
        [SerializeField] private bool enabled = true;
        [SerializeField] private Sprite sprite;
        [SerializeField, Range(0f, 1f)] private float spawnProbability = 0.1f;
        [SerializeField] private Vector2 size = Vector2.one;
        [SerializeField] private Vector2 offset = Vector2.zero;
        [SerializeField] private int sortingOrderOffset;

        public string RuleName => string.IsNullOrWhiteSpace(ruleName) ? "Decoration" : ruleName;
        public bool Enabled => enabled;
        public Sprite Sprite => sprite;
        public float SpawnProbability => spawnProbability;
        public Vector2 Size => size;
        public Vector2 Offset => offset;
        public int SortingOrderOffset => sortingOrderOffset;

        public bool Validate(int index, Object context)
        {
            bool isValid = true;
            string displayName = string.IsNullOrWhiteSpace(ruleName) ? $"decorationRules[{index}]" : ruleName;

            if (sprite == null)
            {
                Debug.LogError($"OverworldBackgroundController: {displayName} sprite is not configured.", context);
                isValid = false;
            }

            if (spawnProbability < 0f || spawnProbability > 1f)
            {
                Debug.LogError($"OverworldBackgroundController: {displayName} spawnProbability must be between 0 and 1.", context);
                isValid = false;
            }

            if (size.x <= 0f || size.y <= 0f)
            {
                Debug.LogError($"OverworldBackgroundController: {displayName} size must be greater than 0.", context);
                isValid = false;
            }

            return isValid;
        }
    }
}
