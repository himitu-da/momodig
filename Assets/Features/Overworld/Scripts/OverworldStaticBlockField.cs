using System.Collections.Generic;
using UnityEngine;

public class OverworldStaticBlockField : MonoBehaviour
{
    [Header("Block Sprites")]
    [SerializeField] private Texture2D surfaceBlockTexture;
    [SerializeField] private Texture2D fillBlockTexture;
    [SerializeField] private float pixelsPerUnit = 100f;
    [SerializeField, Range(0f, 0.49f)] private float spriteOuterTrimRatio;

    [Header("Layout")]
    [SerializeField] private int columns = 16;
    [SerializeField] private int rows = 2;
    [SerializeField] private float blockSize = 1f;
    [SerializeField] private float centerX = 0f;
    [SerializeField] private float surfaceY = -2.6f;

    [Header("Rendering")]
    [SerializeField] private Material blockMaterial;

    [Header("Collision")]
    [SerializeField] private bool createSingleCollider = true;
    [SerializeField] private BoxCollider groundCollider;
    [SerializeField] private float colliderDepth = 2f;

    private readonly List<GameObject> generatedBlocks = new List<GameObject>();

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        BuildField();
    }

    private void BuildField()
    {
        ClearGeneratedBlocks();

        float startX = centerX - ((columns - 1) * blockSize * 0.5f);
        Sprite surfaceBlockSprite = CreateSprite(surfaceBlockTexture);
        Sprite fillBlockSprite = CreateSprite(fillBlockTexture);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Sprite sprite = row == 0 ? surfaceBlockSprite : fillBlockSprite;

                GameObject block = new GameObject($"GeneratedBlock_{row}_{column}");
                generatedBlocks.Add(block);
                block.transform.SetParent(transform, false);
                block.transform.localPosition = new Vector3(
                    startX + column * blockSize,
                    surfaceY - blockSize * 0.5f - row * blockSize,
                    0f
                );

                SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = blockMaterial;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = new Vector2(blockSize, blockSize);
            }
        }

        UpdateCollider();
    }

    private void ClearGeneratedBlocks()
    {
        for (int i = 0; i < generatedBlocks.Count; i++)
        {
            if (generatedBlocks[i] != null)
            {
                Destroy(generatedBlocks[i]);
            }
        }

        generatedBlocks.Clear();
    }

    private void UpdateCollider()
    {
        if (!createSingleCollider)
        {
            if (groundCollider != null)
            {
                groundCollider.enabled = false;
            }
            return;
        }

        groundCollider.enabled = true;
        groundCollider.isTrigger = false;
        groundCollider.size = new Vector3(columns * blockSize, rows * blockSize, colliderDepth);
        groundCollider.center = new Vector3(centerX, surfaceY - rows * blockSize * 0.5f, 0f);
    }

    private Sprite CreateSprite(Texture2D texture)
    {
        float trimX = texture.width * spriteOuterTrimRatio;
        float trimY = texture.height * spriteOuterTrimRatio;
        Rect spriteRect = new Rect(
            trimX,
            trimY,
            texture.width - trimX * 2f,
            texture.height - trimY * 2f
        );

        return Sprite.Create(
            texture,
            spriteRect,
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (surfaceBlockTexture == null)
        {
            Debug.LogError("OverworldStaticBlockField: surfaceBlockTexture is not configured.", this);
            isValid = false;
        }

        if (fillBlockTexture == null)
        {
            Debug.LogError("OverworldStaticBlockField: fillBlockTexture is not configured.", this);
            isValid = false;
        }

        if (blockMaterial == null)
        {
            Debug.LogError("OverworldStaticBlockField: blockMaterial is not configured.", this);
            isValid = false;
        }

        if (createSingleCollider && groundCollider == null)
        {
            Debug.LogError("OverworldStaticBlockField: groundCollider is not configured.", this);
            isValid = false;
        }

        if (columns <= 0)
        {
            Debug.LogError("OverworldStaticBlockField: columns must be greater than 0.", this);
            isValid = false;
        }

        if (rows <= 0)
        {
            Debug.LogError("OverworldStaticBlockField: rows must be greater than 0.", this);
            isValid = false;
        }

        if (pixelsPerUnit <= 0f)
        {
            Debug.LogError("OverworldStaticBlockField: pixelsPerUnit must be greater than 0.", this);
            isValid = false;
        }

        if (spriteOuterTrimRatio < 0f || spriteOuterTrimRatio >= 0.5f)
        {
            Debug.LogError("OverworldStaticBlockField: spriteOuterTrimRatio must be 0 or greater and less than 0.5.", this);
            isValid = false;
        }

        if (blockSize <= 0f)
        {
            Debug.LogError("OverworldStaticBlockField: blockSize must be greater than 0.", this);
            isValid = false;
        }

        if (colliderDepth <= 0f)
        {
            Debug.LogError("OverworldStaticBlockField: colliderDepth must be greater than 0.", this);
            isValid = false;
        }

        return isValid;
    }
}
