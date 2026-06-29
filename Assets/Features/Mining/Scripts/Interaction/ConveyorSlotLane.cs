using System.Collections.Generic;
using UnityEngine;

public sealed class ConveyorSlotLane
{
    private const float Epsilon = 0.0001f;

    public sealed class Reservation
    {
        internal Reservation(int laneIndex, int minSlotIndex, int slotCount, int stackLayer)
        {
            LaneIndex = laneIndex;
            MinSlotIndex = minSlotIndex;
            SlotCount = slotCount;
            StackLayer = stackLayer;
        }

        public int LaneIndex { get; }
        public int MinSlotIndex { get; }
        public int SlotCount { get; }
        public int StackLayer { get; }
        public int MaxSlotIndex => MinSlotIndex + SlotCount - 1;
        public bool IsReleased { get; internal set; }
    }

    private readonly List<Reservation> reservations = new List<Reservation>();
    private int laneCount = 1;
    private int slotCapacity = 1;
    private int maxStackLayers = 1;
    private float laneLength = 1f;
    private float slotSpacing = 1f;
    private float speed = 1f;
    private float travelDistance;

    public float LaneLength => laneLength;
    public float SlotSpacing => slotSpacing;
    public int SlotCapacity => slotCapacity;
    public int MaxStackLayers => maxStackLayers;
    public int TotalSlotCapacity => slotCapacity * maxStackLayers;

    public bool Configure(
        int newLaneCount,
        float newLaneLength,
        int newSlotCapacity,
        int newMaxStackLayers,
        float newSpeed)
    {
        newLaneCount = Mathf.Max(1, newLaneCount);
        newLaneLength = Mathf.Max(0.001f, newLaneLength);
        newSlotCapacity = Mathf.Max(1, newSlotCapacity);
        newMaxStackLayers = Mathf.Max(1, newMaxStackLayers);
        newSpeed = Mathf.Max(0.001f, newSpeed);

        bool topologyChanged =
            newLaneCount != laneCount ||
            newSlotCapacity != slotCapacity ||
            newMaxStackLayers != maxStackLayers ||
            !Mathf.Approximately(newLaneLength, laneLength);

        laneCount = newLaneCount;
        laneLength = newLaneLength;
        slotCapacity = newSlotCapacity;
        maxStackLayers = newMaxStackLayers;
        speed = newSpeed;
        slotSpacing = laneLength / slotCapacity;

        if (topologyChanged)
        {
            Clear();
        }

        return topologyChanged;
    }

    public void Advance(float deltaTime, bool canAdvance)
    {
        if (canAdvance)
        {
            travelDistance += Mathf.Max(0f, deltaTime) * speed;
        }

        ReleaseCompletedReservations();
    }

    public bool TryReserve(
        int laneIndex,
        float sweptFromDistance,
        float sweptToDistance,
        int requiredSlots,
        out Reservation reservation)
    {
        reservation = null;
        if (laneIndex < 0 || laneIndex >= laneCount)
        {
            return false;
        }

        if (!IsFinite(sweptFromDistance) || !IsFinite(sweptToDistance))
        {
            return false;
        }

        ReleaseCompletedReservations();

        requiredSlots = Mathf.Max(1, requiredSlots);
        if (requiredSlots > slotCapacity)
        {
            return false;
        }

        float minDistance = Mathf.Clamp(Mathf.Min(sweptFromDistance, sweptToDistance), 0f, laneLength);
        float maxDistance = Mathf.Clamp(Mathf.Max(sweptFromDistance, sweptToDistance), 0f, laneLength);
        float acceptancePadding = slotSpacing * 0.5f;
        float acceptedMin = Mathf.Max(0f, minDistance - acceptancePadding);
        float acceptedMax = Mathf.Min(laneLength, maxDistance + acceptancePadding);

        int minVisibleIndex = GetMinVisibleSlotIndex();
        int maxVisibleIndex = GetMaxVisibleSlotIndex();
        for (int slotIndex = maxVisibleIndex; slotIndex >= minVisibleIndex; slotIndex--)
        {
            float slotDistance = GetSlotDistance(slotIndex);
            if (slotDistance < acceptedMin - Epsilon || slotDistance > acceptedMax + Epsilon)
            {
                continue;
            }

            int minSlotIndex = slotIndex - requiredSlots + 1;
            if (!IsSlotRangeVisible(minSlotIndex, requiredSlots))
            {
                continue;
            }

            for (int stackLayer = 0; stackLayer < maxStackLayers; stackLayer++)
            {
                if (!IsSlotRangeFree(laneIndex, minSlotIndex, requiredSlots, stackLayer))
                {
                    continue;
                }

                reservation = new Reservation(laneIndex, minSlotIndex, requiredSlots, stackLayer);
                reservations.Add(reservation);
                return true;
            }
        }

        return false;
    }

    public float GetReservationCenterDistance(Reservation reservation)
    {
        if (reservation == null)
        {
            return laneLength;
        }

        float minDistance = GetSlotDistance(reservation.MinSlotIndex);
        float maxDistance = GetSlotDistance(reservation.MaxSlotIndex);
        return (minDistance + maxDistance) * 0.5f;
    }

    public void Release(Reservation reservation)
    {
        if (reservation == null || reservation.IsReleased)
        {
            return;
        }

        reservation.IsReleased = true;
        reservations.Remove(reservation);
    }

    public void Clear()
    {
        for (int i = 0; i < reservations.Count; i++)
        {
            reservations[i].IsReleased = true;
        }

        reservations.Clear();
        travelDistance = 0f;
    }

    private void ReleaseCompletedReservations()
    {
        for (int i = reservations.Count - 1; i >= 0; i--)
        {
            Reservation reservation = reservations[i];
            if (reservation == null || reservation.IsReleased)
            {
                reservations.RemoveAt(i);
                continue;
            }

            if (GetSlotDistance(reservation.MaxSlotIndex) >= laneLength - Epsilon)
            {
                reservation.IsReleased = true;
                reservations.RemoveAt(i);
            }
        }
    }

    private bool IsSlotRangeVisible(int minSlotIndex, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float distance = GetSlotDistance(minSlotIndex + i);
            if (distance < -Epsilon || distance >= laneLength - Epsilon)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsSlotRangeFree(int laneIndex, int minSlotIndex, int count, int stackLayer)
    {
        int maxSlotIndex = minSlotIndex + count - 1;
        for (int i = 0; i < reservations.Count; i++)
        {
            Reservation active = reservations[i];
            if (active == null ||
                active.IsReleased ||
                active.LaneIndex != laneIndex ||
                active.StackLayer != stackLayer)
            {
                continue;
            }

            if (minSlotIndex <= active.MaxSlotIndex && maxSlotIndex >= active.MinSlotIndex)
            {
                return false;
            }
        }

        return true;
    }

    private int GetMinVisibleSlotIndex()
    {
        return Mathf.FloorToInt((travelDistance - laneLength) / slotSpacing) + 1;
    }

    private int GetMaxVisibleSlotIndex()
    {
        return Mathf.FloorToInt(travelDistance / slotSpacing);
    }

    private float GetSlotDistance(int slotIndex)
    {
        return travelDistance - slotIndex * slotSpacing;
    }

    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
