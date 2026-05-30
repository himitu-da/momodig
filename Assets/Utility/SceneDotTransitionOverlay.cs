using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SceneDotTransitionOverlay : MonoBehaviour
{
    private static readonly ProfilerMarker CaptureMarker =
        new ProfilerMarker("SceneDotTransitionOverlay.CaptureCurrentFrame");
    private static readonly ProfilerMarker BuildTilesMarker =
        new ProfilerMarker("SceneDotTransitionOverlay.BuildTiles");
    private static readonly ProfilerMarker PlayRevealMarker =
        new ProfilerMarker("SceneDotTransitionOverlay.PlayReveal");

    [Header("Grid")]
    [SerializeField] private int gridColumns = 22;
    [SerializeField] private Vector2 centerOffsetPixels = Vector2.zero;

    [Header("Timing")]
    [SerializeField] private float revealDuration = 0.7f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Order")]
    [SerializeField] private float randomness = 0.15f;
    [SerializeField] private int randomSeed = 127;

    [Header("Canvas")]
    [SerializeField] private int sortingOrder = 32767;

    private readonly List<RawImage> tiles = new List<RawImage>();
    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private RectTransform tileRoot;
    private Texture2D capturedTexture;

    public bool HasCapturedFrame => capturedTexture != null && tiles.Count > 0;

    private void Awake()
    {
        EnsureOverlayObjects();
        HideOverlay();
    }

    private void OnDestroy()
    {
        ClearCapturedFrame();
    }

    public IEnumerator CaptureCurrentFrame()
    {
        yield return new WaitForEndOfFrame();

        using (CaptureMarker.Auto())
        {
            ClearCapturedFrame();

            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                Debug.LogError("SceneDotTransitionOverlay: Cannot capture the current frame because the screen size is invalid.", this);
                yield break;
            }

            capturedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            capturedTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            capturedTexture.Apply(false, false);

            BuildTiles(width, height);
            ShowOverlay();
        }
    }

    public IEnumerator PlayReveal()
    {
        if (!HasCapturedFrame)
        {
            Debug.LogError("SceneDotTransitionOverlay: Cannot play reveal because no captured frame is available.", this);
            yield break;
        }

        float duration = Mathf.Max(0.01f, revealDuration);
        int hiddenCount = 0;
        float elapsed = 0f;

        while (hiddenCount < tiles.Count)
        {
            using (PlayRevealMarker.Auto())
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float curvedTime = revealCurve != null ? Mathf.Clamp01(revealCurve.Evaluate(normalizedTime)) : normalizedTime;
                int targetHiddenCount = Mathf.Clamp(Mathf.FloorToInt(curvedTime * tiles.Count), 0, tiles.Count);

                for (int i = hiddenCount; i < targetHiddenCount; i++)
                {
                    if (tiles[i] != null)
                    {
                        tiles[i].enabled = false;
                    }
                }

                hiddenCount = targetHiddenCount;
            }

            yield return null;
        }

        ClearCapturedFrame();
        HideOverlay();
    }

    public void ClearOverlay()
    {
        ClearCapturedFrame();
        HideOverlay();
    }

    private void EnsureOverlayObjects()
    {
        if (overlayCanvas != null && overlayGroup != null && tileRoot != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Scene Dot Transition Overlay Canvas");
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = sortingOrder;

        overlayGroup = canvasObject.AddComponent<CanvasGroup>();
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        GameObject rootObject = new GameObject("Tiles");
        rootObject.transform.SetParent(canvasObject.transform, false);
        tileRoot = rootObject.AddComponent<RectTransform>();
        tileRoot.anchorMin = Vector2.zero;
        tileRoot.anchorMax = Vector2.one;
        tileRoot.offsetMin = Vector2.zero;
        tileRoot.offsetMax = Vector2.zero;
    }

    private void BuildTiles(int screenWidth, int screenHeight)
    {
        using (BuildTilesMarker.Auto())
        {
            EnsureOverlayObjects();

            int columns = Mathf.Max(1, gridColumns);
            float cellSize = screenWidth / (float)columns;
            int rows = Mathf.CeilToInt(screenHeight / cellSize);
            float gridHeight = rows * cellSize;
            float gridBottom = (screenHeight - gridHeight) * 0.5f + centerOffsetPixels.y;
            float gridLeft = centerOffsetPixels.x;
            Vector2 screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f) + centerOffsetPixels;

            List<TileBuildData> buildData = new List<TileBuildData>(columns * rows);
            System.Random random = new System.Random(randomSeed);

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float cellMinX = gridLeft + column * cellSize;
                    float cellMinY = gridBottom + row * cellSize;
                    float cellMaxX = cellMinX + cellSize;
                    float cellMaxY = cellMinY + cellSize;

                    float visibleMinX = Mathf.Clamp(cellMinX, 0f, screenWidth);
                    float visibleMinY = Mathf.Clamp(cellMinY, 0f, screenHeight);
                    float visibleMaxX = Mathf.Clamp(cellMaxX, 0f, screenWidth);
                    float visibleMaxY = Mathf.Clamp(cellMaxY, 0f, screenHeight);

                    if (visibleMaxX <= visibleMinX || visibleMaxY <= visibleMinY)
                    {
                        continue;
                    }

                    Vector2 cellCenter = new Vector2((cellMinX + cellMaxX) * 0.5f, (cellMinY + cellMaxY) * 0.5f);
                    float jitter = ((float)random.NextDouble() * 2f - 1f) * Mathf.Max(0f, randomness) * cellSize;
                    float sortKey = Vector2.Distance(cellCenter, screenCenter) + jitter;

                    buildData.Add(new TileBuildData(
                        visibleMinX,
                        visibleMinY,
                        visibleMaxX - visibleMinX,
                        visibleMaxY - visibleMinY,
                        sortKey));
                }
            }

            buildData.Sort((left, right) => left.SortKey.CompareTo(right.SortKey));

            for (int i = 0; i < buildData.Count; i++)
            {
                CreateTile(buildData[i], screenWidth, screenHeight);
            }
        }
    }

    private void CreateTile(TileBuildData data, int screenWidth, int screenHeight)
    {
        GameObject tileObject = new GameObject("Tile");
        tileObject.transform.SetParent(tileRoot, false);

        RectTransform rectTransform = tileObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(data.Width, data.Height);
        rectTransform.anchoredPosition = new Vector2(
            data.X + data.Width * 0.5f - screenWidth * 0.5f,
            data.Y + data.Height * 0.5f - screenHeight * 0.5f);

        RawImage image = tileObject.AddComponent<RawImage>();
        image.texture = capturedTexture;
        image.uvRect = new Rect(
            data.X / screenWidth,
            data.Y / screenHeight,
            data.Width / screenWidth,
            data.Height / screenHeight);
        image.raycastTarget = false;

        tiles.Add(image);
    }

    private void ShowOverlay()
    {
        EnsureOverlayObjects();
        overlayCanvas.sortingOrder = sortingOrder;
        overlayCanvas.enabled = true;
        overlayGroup.alpha = 1f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;
    }

    private void HideOverlay()
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = false;
        }

        if (overlayGroup != null)
        {
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;
        }
    }

    private void ClearCapturedFrame()
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] != null)
            {
                Destroy(tiles[i].gameObject);
            }
        }

        tiles.Clear();

        if (capturedTexture != null)
        {
            Destroy(capturedTexture);
            capturedTexture = null;
        }
    }

    private readonly struct TileBuildData
    {
        public TileBuildData(float x, float y, float width, float height, float sortKey)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            SortKey = sortKey;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float SortKey { get; }
    }
}
