using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FacilityPromptView : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private RectTransform root;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Button openButton;
    [SerializeField] private TMP_Text promptLabel;

    private Transform currentAnchor;
    private Action openRequested;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        openButton.onClick.AddListener(HandleOpenClicked);
        Hide();
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
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(HandleOpenClicked);
        }
    }

    public void Show(FacilityDefinition facility, Transform anchor, Action openRequestedCallback)
    {
        if (!enabled)
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

        currentAnchor = anchor;
        openRequested = openRequestedCallback;
        promptLabel.SetText(facility.PromptLabel);
        root.gameObject.SetActive(true);
        IsVisible = true;
        UpdatePosition();
    }

    public void Hide()
    {
        if (root != null)
        {
            root.gameObject.SetActive(false);
        }

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

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (root == null)
        {
            Debug.LogError("FacilityPromptView: root is not configured.", this);
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
}
