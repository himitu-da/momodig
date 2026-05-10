using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FacilityPromptView : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup visibilityGroup;
    [SerializeField] private Button openButton;
    [SerializeField] private TMP_Text promptLabel;

    private Canvas parentCanvas;
    private Camera worldCamera;
    private Transform currentAnchor;
    private Action openRequested;
    private bool isInitialized;
    private bool hasSceneReferences;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (!InitializePrefabReferences())
        {
            enabled = false;
            return;
        }

        Hide();
    }

    private void Start()
    {
        if (!ValidateSceneReferences())
        {
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!IsVisible)
        {
            return;
        }

        UpdatePosition();
    }

    private void OnDestroy()
    {
        if (isInitialized && openButton != null)
        {
            openButton.onClick.RemoveListener(HandleOpenClicked);
        }
    }

    public bool BindSceneReferences(Canvas requiredParentCanvas, Camera requiredWorldCamera)
    {
        if (!enabled)
        {
            Debug.LogError("FacilityPromptView: cannot bind scene references because the view is disabled.", this);
            return false;
        }

        bool isValid = true;

        if (requiredParentCanvas == null)
        {
            Debug.LogError("FacilityPromptView: parentCanvas is not configured.", this);
            isValid = false;
        }

        if (requiredWorldCamera == null)
        {
            Debug.LogError("FacilityPromptView: worldCamera is not configured.", this);
            isValid = false;
        }

        if (!isValid)
        {
            enabled = false;
            return false;
        }

        parentCanvas = requiredParentCanvas;
        worldCamera = requiredWorldCamera;
        hasSceneReferences = true;
        Hide();
        return true;
    }

    public void Show(FacilityDefinition facility, Transform anchor, Action openRequestedCallback)
    {
        if (!enabled)
        {
            return;
        }

        if (!isInitialized)
        {
            Debug.LogError("FacilityPromptView: prefab references are not initialized.", this);
            return;
        }

        if (!ValidateSceneReferences())
        {
            return;
        }

        if (facility == null)
        {
            Debug.LogError("FacilityPromptView: facility is not configured.", this);
            Hide();
            return;
        }

        if (!facility.ValidateConfiguration(this))
        {
            Hide();
            return;
        }

        if (anchor == null)
        {
            Debug.LogError($"FacilityPromptView: prompt anchor is not configured for '{facility.DisplayName}'.", this);
            Hide();
            return;
        }

        if (openRequestedCallback == null)
        {
            Debug.LogError($"FacilityPromptView: open callback is not configured for '{facility.DisplayName}'.", this);
            Hide();
            return;
        }

        if (!root.gameObject.activeSelf)
        {
            root.gameObject.SetActive(true);
        }

        currentAnchor = anchor;
        openRequested = openRequestedCallback;
        promptLabel.SetText(facility.PromptLabel);
        SetVisibility(true);
        IsVisible = true;
        UpdatePosition();
    }

    public void Hide()
    {
        SetVisibility(false);
        IsVisible = false;
        currentAnchor = null;
        openRequested = null;
    }

    private void HandleOpenClicked()
    {
        if (!IsVisible || openRequested == null)
        {
            Debug.LogError("FacilityPromptView: open button was clicked without an active facility.", this);
            return;
        }

        openRequested.Invoke();
    }

    private void UpdatePosition()
    {
        if (!ValidateSceneReferences())
        {
            Hide();
            return;
        }

        if (currentAnchor == null)
        {
            Debug.LogError("FacilityPromptView: current anchor was lost while visible.", this);
            Hide();
            return;
        }

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            Debug.LogError("FacilityPromptView: parentCanvas does not have a RectTransform.", this);
            Hide();
            return;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(currentAnchor.position);
        Camera canvasCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay && canvasCamera == null)
        {
            Debug.LogError("FacilityPromptView: parentCanvas.worldCamera is not configured.", this);
            Hide();
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 localPoint))
        {
            Debug.LogError("FacilityPromptView: failed to convert prompt position to canvas coordinates.", this);
            Hide();
            return;
        }

        root.anchoredPosition = localPoint;
    }

    private bool InitializePrefabReferences()
    {
        if (isInitialized)
        {
            return true;
        }

        if (!ValidatePrefabReferences())
        {
            enabled = false;
            return false;
        }

        openButton.onClick.AddListener(HandleOpenClicked);
        isInitialized = true;
        return true;
    }

    private void SetVisibility(bool visible)
    {
        if (visibilityGroup == null)
        {
            Debug.LogError("FacilityPromptView: visibilityGroup is not configured.", this);
            enabled = false;
            return;
        }

        visibilityGroup.alpha = visible ? 1f : 0f;
        visibilityGroup.interactable = visible;
        visibilityGroup.blocksRaycasts = visible;
    }

    private bool ValidatePrefabReferences()
    {
        bool isValid = true;

        if (root == null)
        {
            Debug.LogError("FacilityPromptView: root is not configured.", this);
            isValid = false;
        }

        if (visibilityGroup == null)
        {
            Debug.LogError("FacilityPromptView: visibilityGroup is not configured.", this);
            isValid = false;
        }

        if (openButton == null)
        {
            Debug.LogError("FacilityPromptView: openButton is not configured.", this);
            isValid = false;
        }

        if (promptLabel == null)
        {
            Debug.LogError("FacilityPromptView: promptLabel is not configured.", this);
            isValid = false;
        }

        return isValid;
    }

    private bool ValidateSceneReferences()
    {
        bool isValid = true;

        if (!hasSceneReferences)
        {
            Debug.LogError("FacilityPromptView: scene references are not configured.", this);
            isValid = false;
        }

        if (parentCanvas == null)
        {
            Debug.LogError("FacilityPromptView: parentCanvas is not configured.", this);
            isValid = false;
        }

        if (worldCamera == null)
        {
            Debug.LogError("FacilityPromptView: worldCamera is not configured.", this);
            isValid = false;
        }

        if (!isValid)
        {
            hasSceneReferences = false;
            enabled = false;
        }

        return isValid;
    }
}
