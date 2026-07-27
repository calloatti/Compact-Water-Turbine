using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  public class CompactWaterTurbineSynchronizer
  {
    private readonly IBlockService _blockService;
    private readonly Queue<CompactWaterTurbine> _neighborsQueue = new Queue<CompactWaterTurbine>();
    private readonly HashSet<CompactWaterTurbine> _visitedNeighbors = new HashSet<CompactWaterTurbine>();

    public CompactWaterTurbineSynchronizer(IBlockService blockService)
    {
      _blockService = blockService;
    }

    public void SynchronizeAllNeighbors(CompactWaterTurbine turbine)
    {
      SynchronizeNeighbors(turbine);
    }

    public void SynchronizeWithAllNeighbors(CompactWaterTurbine turbine)
    {
      if (turbine.IsSynchronized)
      {
        SynchronizeNeighbors(turbine);
      }
    }

    private void SynchronizeNeighbors(CompactWaterTurbine startingTurbine)
    {
      if (!startingTurbine.IsSynchronized)
      {
        return;
      }

      EnqueueTurbine(startingTurbine);

      try
      {
        while (_neighborsQueue.Count > 0)
        {
          CompactWaterTurbine turbine = _neighborsQueue.Dequeue();
          BlockObject blockObject = turbine.GetComponent<BlockObject>();
          if (blockObject == null || blockObject.PositionedBlocks == null) continue;

          // Grab every single precise 3D grid cell this turbine occupies in the world
          var occupiedCoordinates = blockObject.PositionedBlocks.GetOccupiedCoordinates();
          Vector3Int[] horizontalDeltas = Deltas.Neighbors4Vector3Int;

          foreach (Vector3Int occupiedCell in occupiedCoordinates)
          {
            foreach (Vector3Int delta in horizontalDeltas)
            {
              // Check the direct horizontal neighbors touching any block of the machine's body
              Vector3Int neighborCell = occupiedCell + delta;

              CompactWaterTurbine adjacentTurbine = _blockService.GetBottomObjectComponentAt<CompactWaterTurbine>(neighborCell);

              // Ensure we found a turbine, it's an entirely separate instance, and it isn't already processed
              if (adjacentTurbine != null && adjacentTurbine != turbine && !_visitedNeighbors.Contains(adjacentTurbine))
              {
                if (adjacentTurbine.IsSynchronized)
                {
                  // Sync the targeted flow rates
                  adjacentTurbine.SetFlowRate(startingTurbine.FlowRate);
                  EnqueueTurbine(adjacentTurbine);
                }
              }
            }
          }
        }
      }
      finally
      {
        // GUARANTEES memory is freed and ready for the next run, even if the loop throws an exception
        _neighborsQueue.Clear();
        _visitedNeighbors.Clear();
      }
    }

    private void EnqueueTurbine(CompactWaterTurbine turbine)
    {
      _neighborsQueue.Enqueue(turbine);
      _visitedNeighbors.Add(turbine);
    }
  }
}