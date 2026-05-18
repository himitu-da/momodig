using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class DroppedItemOutlineManager : MonoBehaviour
{
    private static readonly ProfilerMarker UpdateMarker =
        new ProfilerMarker("DroppedItemOutlineManager.Update");

    public static DroppedItemOutlineManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerController playerController;

    private readonly List<DroppedItemOutline> outlines = new List<DroppedItemOutline>(512);
    private bool missingPlayerLogged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("DroppedItemOutlineManager: multiple managers exist in the scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        ValidateConfiguration();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        using (UpdateMarker.Auto())
        {
            float playerInputMagnitude = GetPlayerInputMagnitude();
            float deltaTime = Time.deltaTime;
            for (int i = outlines.Count - 1; i >= 0; i--)
            {
                DroppedItemOutline outline = outlines[i];
                if (outline == null)
                {
                    outlines.RemoveAt(i);
                    continue;
                }

                outline.TickOutline(playerInputMagnitude, deltaTime);
            }
        }
    }

    public void Register(DroppedItemOutline outline)
    {
        if (outline == null)
        {
            Debug.LogError("DroppedItemOutlineManager: cannot register a null outline.", this);
            return;
        }

        if (!outlines.Contains(outline))
        {
            outlines.Add(outline);
        }
    }

    public void Unregister(DroppedItemOutline outline)
    {
        if (outline == null)
        {
            return;
        }

        outlines.Remove(outline);
    }

    private float GetPlayerInputMagnitude()
    {
        if (playerController == null)
        {
            if (!missingPlayerLogged)
            {
                missingPlayerLogged = true;
                Debug.LogError("DroppedItemOutlineManager: playerController is not configured.", this);
            }

            return 0f;
        }

        missingPlayerLogged = false;
        return Mathf.Clamp01(playerController.MoveInput.magnitude);
    }

    private void ValidateConfiguration()
    {
        if (playerController == null)
        {
            Debug.LogError("DroppedItemOutlineManager: playerController is not configured.", this);
        }
    }
}
