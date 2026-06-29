using UnityEngine;

[ExecuteAlways]
public class ConveyorBeltPlacement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform anchor;
    [SerializeField] private Transform leftBelt;
    [SerializeField] private Transform rightBelt;
    [SerializeField] private BoxCollider leftInputArea;
    [SerializeField] private BoxCollider rightInputArea;

    [Header("Cell Placement")]
    [SerializeField] private float cellSize = 1f / 3f;
    [SerializeField] private Vector2Int areaCells = new Vector2Int(9, 1);
    [SerializeField] private Vector2Int leftOriginCell = new Vector2Int(-10, 0);
    [SerializeField] private Vector2Int rightOriginCell = new Vector2Int(1, 0);
    [SerializeField] private int inputAdditionalTopCells = 8;
    [SerializeField] private float verticalOffsetBlocks = -2.5f;
    [SerializeField] private Vector3 cellRightAxis = Vector3.right;
    [SerializeField] private Vector3 cellUpAxis = Vector3.up;
    [SerializeField] private Vector3 depthAxis = Vector3.forward;

    [Header("Visual")]
    [SerializeField, Min(0.01f)] private float beltDepth = 0.08f;
    [SerializeField, Min(0.01f)] private float inputDepth = 0.5f;

    public BoxCollider LeftInputArea => leftInputArea;
    public BoxCollider RightInputArea => rightInputArea;
    public Transform LeftVisualCenter => leftBelt;
    public Transform RightVisualCenter => rightBelt;

    private void Awake()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        ApplyPlacement();
    }

    private void OnValidate()
    {
        cellSize = Mathf.Max(0.001f, cellSize);
        areaCells = new Vector2Int(Mathf.Max(1, areaCells.x), Mathf.Max(1, areaCells.y));
        inputAdditionalTopCells = Mathf.Max(0, inputAdditionalTopCells);
        beltDepth = Mathf.Max(0.01f, beltDepth);
        inputDepth = Mathf.Max(0.01f, inputDepth);

        if (CanApplyPlacement())
        {
            ApplyPlacement();
        }
    }

    public void ApplyPlacement()
    {
        Vector3 right = cellRightAxis.normalized;
        Vector3 up = cellUpAxis.normalized;
        Vector3 depth = depthAxis.normalized;
        Quaternion rotation = Quaternion.LookRotation(depth, up);

        ApplyBelt(leftBelt, leftInputArea, leftOriginCell, right, up, rotation);
        ApplyBelt(rightBelt, rightInputArea, rightOriginCell, right, up, rotation);
    }

    private void ApplyBelt(
        Transform belt,
        BoxCollider inputArea,
        Vector2Int originCell,
        Vector3 right,
        Vector3 up,
        Quaternion rotation)
    {
        Vector3 center = CalculateCenter(originCell, right, up);
        Vector3 size = CalculateSize(beltDepth);
        belt.SetPositionAndRotation(center, rotation);
        belt.localScale = size;

        if (inputArea != null)
        {
            float visualCellHeight = Mathf.Max(1, areaCells.y);
            inputArea.isTrigger = true;
            inputArea.center = new Vector3(0f, inputAdditionalTopCells * 0.5f / visualCellHeight, 0f);
            inputArea.size = new Vector3(
                1f,
                (areaCells.y + inputAdditionalTopCells) / visualCellHeight,
                inputDepth / beltDepth);
        }
    }

    private Vector3 CalculateCenter(Vector2Int originCell, Vector3 right, Vector3 up)
    {
        Vector2 centerCell = new Vector2(
            originCell.x + areaCells.x * 0.5f,
            originCell.y + areaCells.y * 0.5f);

        return anchor.position +
            right * centerCell.x * cellSize +
            up * ((centerCell.y * cellSize) + verticalOffsetBlocks);
    }

    private Vector3 CalculateSize(float depth)
    {
        return new Vector3(
            areaCells.x * cellSize,
            areaCells.y * cellSize,
            depth);
    }

    private bool CanApplyPlacement()
    {
        return anchor != null &&
            leftBelt != null &&
            rightBelt != null &&
            IsAxisConfigured(cellRightAxis) &&
            IsAxisConfigured(cellUpAxis) &&
            IsAxisConfigured(depthAxis);
    }

    private bool IsAxisConfigured(Vector3 axis)
    {
        return axis.sqrMagnitude > 0.0001f;
    }

    private bool ValidateConfiguration()
    {
        bool isValid = true;
        if (anchor == null)
        {
            Debug.LogError("ConveyorBeltPlacement: anchor is not configured.", this);
            isValid = false;
        }

        if (leftBelt == null)
        {
            Debug.LogError("ConveyorBeltPlacement: leftBelt is not configured.", this);
            isValid = false;
        }

        if (rightBelt == null)
        {
            Debug.LogError("ConveyorBeltPlacement: rightBelt is not configured.", this);
            isValid = false;
        }

        if (leftInputArea == null)
        {
            Debug.LogError("ConveyorBeltPlacement: leftInputArea is not configured.", this);
            isValid = false;
        }

        if (rightInputArea == null)
        {
            Debug.LogError("ConveyorBeltPlacement: rightInputArea is not configured.", this);
            isValid = false;
        }

        if (!IsAxisConfigured(cellRightAxis))
        {
            Debug.LogError("ConveyorBeltPlacement: cellRightAxis is not configured.", this);
            isValid = false;
        }

        if (!IsAxisConfigured(cellUpAxis))
        {
            Debug.LogError("ConveyorBeltPlacement: cellUpAxis is not configured.", this);
            isValid = false;
        }

        if (!IsAxisConfigured(depthAxis))
        {
            Debug.LogError("ConveyorBeltPlacement: depthAxis is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
