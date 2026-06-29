# Conveyor Export System

## Overview

The conveyor belt is unlocked by the Garage facility upgrade `garage.conveyor.unlock`.
Before the upgrade is purchased, existing surface return behavior continues to move player inventory into storage little by little.
After the upgrade reaches level 1, `ConveyorExportSystem` becomes the single runtime entry point for automatic conveyor export near the surface exit.

## Runtime Rules

- Player inventory is exported when the player enters any configured player input area.
- Minecart and fairy carried items are exported through `TryExportExternalItem(s)` after the carrier reaches its configured ground/home point.
- Dropped voxel items are exported when their bounds enter any configured dropped item input area.
- The conveyor accepts an item only when a moving slot intersects the item's current swept input range.
- Storage is updated only after a conveyor slot is reserved and the temporary visual item is created.
- Visual transfer items are temporary animation objects only; storage is already authoritative.
- If slot reservation or visual creation fails, the source item, player inventory, or minecart load is left untouched.

## Scene Setup

`ConveyorExportSystem` uses serialized scene references only.
Do not add object searches or fallback-generated managers for conveyor dependencies.

Required references:

- `FacilityUpgradeCatalog`
- `StorageManager`
- `PlayerController`
- `DroppedItemManager`
- `TerrainDataManager`
- `playerInputAreas`
- `droppedItemInputAreas`
- `visualAreaCenters`

Optional references:

- `conveyorRoot`: visible belt root to toggle with unlock state.
- `externalDepositPoint`: point used by non-player carriers before items are shown on the belt.

## Visual Placement

`Assets/Features/Mining/Resources/Prefab/ConveyorBelt.prefab` is a simple rectangular visual prefab with two belt children.
`ConveyorBeltPlacement` positions the left and right belt rectangles from an anchor transform using cell units.

Default MiningScene placement:

- Anchor: `OverWorldPassage`
- Cell size: `0.33333334`
- Belt visual size: `9 x 1` cells
- Input area size: `9 x 9` cells
- Vertical offset: `-2.5` blocks
- Left origin cell: `(-10, 0)`
- Right origin cell: `(1, 0)`

The left and right `BoxCollider` triggers are also used as conveyor input areas.
Visual items appear at the closest point on the nearest left/right belt, then move toward that belt's inward edge.

## Slot Lane

The conveyor is treated like a moving slot lane, implemented by `ConveyorSlotLane`.
Slots continuously move from the outside input edge toward the inward edge, and reservations follow those moving slots rather than a fixed array of lane positions.

- `conveyorWidthBlocks` defines the conveyor width in block units.
- `voxelsPerBlock` defines how many voxel-width slots fit in one block.
- Total slot capacity per visual lane is `conveyorWidthBlocks * voxelsPerBlock`.
- A normal voxel item reserves one slot when its width matches one slot.
- Wider visual items reserve multiple slots based on their scale along the inward axis.
- Slot reservations are stored as logical slot ranges. The display position is recalculated every frame from the reservation's current continuous lane distance.
- The input check uses the source's previous and current lane distance as a swept range, so fast movement across multiple slots can reserve multiple available slots in one pass.
- Batched external export reserves multiple currently visible free slots; if the whole batch cannot be reserved, no source items are consumed.
- If the lane is full, or every slot in the swept range is already reserved, the conveyor rejects the item and leaves the source item untouched.
- A reserved slot is released after the reservation passes the inward end of the lane, and the visual item is destroyed at that point.
- Slot movement is advanced through a single lane update. If a future output blocker needs to stop the conveyor, the lane should stop advancing instead of allowing slots to pass through the blockage.
