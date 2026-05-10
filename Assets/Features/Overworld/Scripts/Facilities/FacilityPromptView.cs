using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FacilityPromptView : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup visibilityGroup;
    [SerializeField] private Button openButton;
    [SerializeField] private TMP_Text promptLabel;

    private FacilityDefinition boundFacility;
    private Action openRequested;
    private bool isInitialized;
    private bool isBound;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (!InitializeRequiredReferences())
        {
            enabled = false;
            return;
        }

        Hide();
    }

    private void OnDestroy()
    {
        if (isInitialized && openButton != null)
        {
            openButton.onClick.RemoveListener(HandleOpenClicked);
        }
    }

    public bool Bind(FacilityDefinition facility, Action openRequestedCallback)
    {
        if (!enabled)
        {
            Debug.LogError("FacilityPromptView: cannot bind because the view is disabled.", this);
            return false;
        }

        if (!isInitialized && !InitializeRequiredReferences())
        {
            return false;
        }

        if (facility == null)
        {
            Debug.LogError("FacilityPromptView: facility is not configured.", this);
            DisableView();
            return false;
        }

        if (!facility.ValidateConfiguration(this))
        {
            DisableView();
            return false;
        }

        if (openRequestedCallback == null)
        {
            Debug.LogError($"FacilityPromptView: open callback is not configured for '{facility.DisplayName}'.", this);
            DisableView();
            return false;
        }

        boundFacility = facility;
        openRequested = openRequestedCallback;
        promptLabel.SetText(facility.PromptLabel);
        isBound = true;
        Hide();
        return true;
    }

    public void Show()
    {
        if (!enabled)
        {
            return;
        }

        if (!isInitialized && !InitializeRequiredReferences())
        {
            return;
        }

        if (!isBound || boundFacility == null || openRequested == null)
        {
            Debug.LogError("FacilityPromptView: cannot show because the view is not bound.", this);
            DisableView();
            return;
        }

        if (!boundFacility.ValidateConfiguration(this))
        {
            Hide();
            DisableView();
            return;
        }

        if (!root.gameObject.activeSelf)
        {
            root.gameObject.SetActive(true);
        }

        SetVisibility(true);
        IsVisible = true;
    }

    public void Hide()
    {
        if (visibilityGroup == null)
        {
            return;
        }

        SetVisibility(false);
        IsVisible = false;
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

    private bool InitializeRequiredReferences()
    {
        if (isInitialized)
        {
            return true;
        }

        if (!ValidateRequiredReferences())
        {
            DisableView();
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
            DisableView();
            return;
        }

        visibilityGroup.alpha = visible ? 1f : 0f;
        visibilityGroup.interactable = visible;
        visibilityGroup.blocksRaycasts = visible;
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (worldCanvas == null)
        {
            Debug.LogError("FacilityPromptView: worldCanvas is not configured.", this);
            isValid = false;
        }
        else
        {
            if (worldCanvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogError("FacilityPromptView: worldCanvas must use RenderMode.WorldSpace.", worldCanvas);
                isValid = false;
            }

            if (worldCanvas.worldCamera == null)
            {
                Debug.LogError("FacilityPromptView: worldCanvas.worldCamera is not configured.", worldCanvas);
                isValid = false;
            }
        }

        if (graphicRaycaster == null)
        {
            Debug.LogError("FacilityPromptView: graphicRaycaster is not configured.", this);
            isValid = false;
        }

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

    private void DisableView()
    {
        enabled = false;
    }
}
