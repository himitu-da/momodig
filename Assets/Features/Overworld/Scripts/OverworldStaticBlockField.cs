using UnityEngine;

public class OverworldStaticBlockField : MonoBehaviour
{
    [Header("Block Sprites")]
    [SerializeField] private Texture2D surfaceBlockTexture;
    [SerializeField] private Texture2D fillBlockTexture;
    [SerializeField] private float pixelsPerUnit = 100f;

    [Header("Layout")]
    [SerializeField] private int columns = 16;
    [SerializeField] private int rows = 2;
    [SerializeField] private float blockSize = 1f;
    [SerializeField] private float centerX = 0f;
    [SerializeField] private float surfaceY = -2.6f;
    [SerializeField] private int sortingOrder = -5;

    [Header("Collision")]
    [SerializeField] private bool createSingleCollider = true;
    [SerializeField] private float colliderDepth = 2f;

    private const string GeneratedBlockPrefix = "GeneratedBlock_";

    private void Awake()
    {
        BuildField();
    }

    private void BuildField()
    {
        ClearGeneratedBlocks();

        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        float safeBlockSize = Mathf.Max(0.1f, blockSize);
        float startX = centerX - ((safeColumns - 1) * safeBlockSize * 0.5f);
        Sprite surfaceBlockSprite = CreateSprite(surfaceBlockTexture);
        Sprite fillBlockSprite = CreateSprite(fillBlockTexture);

        for (int row = 0; row < safeRows; row++)
        {
            for (int column = 0; column < safeColumns; column++)
            {
                Sprite sprite = row == 0 ? surfaceBlockSprite : fillBlockSprite;
                if (sprite == null)
                {
                    continue;
                }

                GameObject block = new GameObject($"{GeneratedBlockPrefix}{row}_{column}");
                block.transform.SetParent(transform, false);
                block.transform.localPosition = new Vector3(
                    startX + column * safeBlockSize,
                    surfaceY - safeBlockSize * 0.5f - row * safeBlockSize,
                    0f
                );

                SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = new Vector2(safeBlockSize, safeBlockSize);
                renderer.sortingOrder = sortingOrder - row;
            }
        }

        UpdateCollider(safeColumns, safeRows, safeBlockSize);
    }

    private void ClearGeneratedBlocks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith(GeneratedBlockPrefix))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void UpdateCollider(int safeColumns, int safeRows, float safeBlockSize)
    {
        BoxCollider groundCollider = GetComponent<BoxCollider>();
        if (!createSingleCollider)
        {
            if (groundCollider != null)
            {
                groundCollider.enabled = false;
            }
            return;
        }

        if (groundCollider == null)
        {
            groundCollider = gameObject.AddComponent<BoxCollider>();
        }

        groundCollider.enabled = true;
        groundCollider.isTrigger = false;
        groundCollider.size = new Vector3(safeColumns * safeBlockSize, safeRows * safeBlockSize, colliderDepth);
        groundCollider.center = new Vector3(centerX, surfaceY - safeRows * safeBlockSize * 0.5f, 0f);
    }

    private Sprite CreateSprite(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }

        float safePixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            safePixelsPerUnit
        );
    }
}
