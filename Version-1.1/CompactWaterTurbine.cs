using System;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.DuplicationSystem;
using Timberborn.EntitySystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.TickSystem;
using Timberborn.WaterBuildings;
using Timberborn.WaterSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  public class CompactWaterTurbine : TickableComponent, IAwakableComponent, IPostLoadableEntity, IPersistentEntity, IFinishedStateListener, IDuplicable<CompactWaterTurbine>, IDuplicable
  {
    private static readonly ComponentKey CompactWaterTurbineKey = new ComponentKey("CompactWaterTurbine");
    private static readonly PropertyKey<float> FlowRateKey = new PropertyKey<float>("FlowRate");
    private static readonly PropertyKey<bool> IsSynchronizedKey = new PropertyKey<bool>("IsSynchronized");

    // --- HYSTERESIS & SMOOTHING TUNING ---
    private const float SampleIntervalSeconds = 1.0f;
    private const float ActivationHeadBuffer = 0.6f;
    private const float RampDurationSeconds = 4.0f;

    private float _sampleTimer = 0f;
    private float _cachedHead = 0f;
    private float _overflowPressureFactor = 20f;

    private readonly ITickService _tickService;
    private readonly IThreadSafeWaterMap _threadSafeWaterMap;
    private readonly CompactWaterTurbineSynchronizer _synchronizer;
    private readonly ISpecService _specService;
    private readonly IWaterService _waterService;

    private MechanicalNode _mechanicalNode;
    private WaterInput _waterInput;
    private WaterOutput _waterOutput;
    private CompactWaterTurbineSpec _spec;
    private BlockObject _blockObject;

    private Vector3Int _outputCoordinates;

    private bool _isWaterFlowActive;

    public bool CanMoveWater => _isWaterFlowActive;
    public float FlowRate { get; private set; }

    public float EffectiveFlowRate { get; private set; }

    // NEW: Expose the current contamination level for the particle controller
    public float CurrentContamination { get; private set; }

    public float MaxFlowRate => _spec.MaxWaterPerSecond;
    public bool IsSynchronized { get; private set; } = true;

    public CompactWaterTurbine(ITickService tickService, IThreadSafeWaterMap threadSafeWaterMap, CompactWaterTurbineSynchronizer synchronizer, ISpecService specService, IWaterService waterService)
    {
      _tickService = tickService;
      _threadSafeWaterMap = threadSafeWaterMap;
      _synchronizer = synchronizer;
      _specService = specService;
      _waterService = waterService;
    }

    public void Awake()
    {
      _mechanicalNode = GetComponent<MechanicalNode>();
      _waterInput = GetComponent<WaterInput>();
      _waterOutput = GetComponent<WaterOutput>();
      _spec = GetComponent<CompactWaterTurbineSpec>();
      _blockObject = GetComponent<BlockObject>();
      FlowRate = _spec.MaxWaterPerSecond;

      _overflowPressureFactor = _specService.GetSingleSpec<WaterSimulatorSpec>().OverflowPressureFactor;

      DisableComponent();
    }

    public void PostLoadEntity()
    {
      UpdatePowerGeneration(0f, 0f);
    }

    public void OnEnterFinishedState()
    {
      WaterOutputSpec outputSpec = GetComponent<WaterOutputSpec>();
      _outputCoordinates = _blockObject.TransformCoordinates(outputSpec.WaterCoordinates);

      _sampleTimer = SampleIntervalSeconds;
      EnableComponent();
    }

    public void OnExitFinishedState()
    {
      DisableComponent();
      EffectiveFlowRate = 0f;
      CurrentContamination = 0f;
      UpdatePowerGeneration(0f, 0f);
      _sampleTimer = 0f;
      _cachedHead = 0f;
      _isWaterFlowActive = false;
    }

    public override void Tick()
    {
      _sampleTimer += _tickService.TickIntervalInSeconds;
      if (_sampleTimer >= SampleIntervalSeconds)
      {
        _cachedHead = GetCurrentHead();
        _sampleTimer = 0f;
      }

      UpdateFlowState();

      float targetFlow = _isWaterFlowActive ? FlowRate : 0f;

      float rampRate = MaxFlowRate / RampDurationSeconds;
      EffectiveFlowRate = Mathf.MoveTowards(EffectiveFlowRate, targetFlow, rampRate * _tickService.TickIntervalInSeconds);

      if (EffectiveFlowRate > 0.001f)
      {
        float requestedWater = _tickService.TickIntervalInSeconds * EffectiveFlowRate;
        MoveWater(requestedWater);
      }

      UpdatePowerGeneration(EffectiveFlowRate, _cachedHead);
    }

    private void UpdateFlowState()
    {
      float intakeWaterAvailable = _threadSafeWaterMap.WaterDepth(_waterInput.Coordinates) + _threadSafeWaterMap.ColumnOverflow(_waterInput.Coordinates);
      bool isIntakeSubmerged = intakeWaterAvailable > 0.001f;

      if (!_mechanicalNode.Active || !isIntakeSubmerged)
      {
        _isWaterFlowActive = false;
        return;
      }

      if (!_isWaterFlowActive)
      {
        if (_cachedHead >= (_spec.MinWaterDrop + ActivationHeadBuffer))
        {
          _isWaterFlowActive = true;
        }
      }
      else
      {
        if (_cachedHead < _spec.MinWaterDrop)
        {
          _isWaterFlowActive = false;
        }
      }
    }

    public float GetCurrentHead()
    {
      float inputWaterLevel = _threadSafeWaterMap.WaterHeightOrFloor(_waterInput.Coordinates)
                              + (_threadSafeWaterMap.ColumnOverflow(_waterInput.Coordinates) * _overflowPressureFactor);

      float effectiveInputHeight = Mathf.Max(_waterInput.Coordinates.z, inputWaterLevel);

      float outputWaterLevel = _threadSafeWaterMap.WaterHeightOrFloor(_outputCoordinates)
                               + (_threadSafeWaterMap.ColumnOverflow(_outputCoordinates) * _overflowPressureFactor);

      float effectiveOutputHeight = Mathf.Max(_outputCoordinates.z, outputWaterLevel);

      return effectiveInputHeight - effectiveOutputHeight;
    }

    public float GetFlowCapacity()
    {
      if (!_isWaterFlowActive) return 0f;

      return FlowRate;
    }

    public void Save(IEntitySaver entitySaver)
    {
      IObjectSaver component = entitySaver.GetComponent(CompactWaterTurbineKey);
      component.Set(FlowRateKey, FlowRate);
      component.Set(IsSynchronizedKey, IsSynchronized);
    }

    public void Load(IEntityLoader entityLoader)
    {
      if (!entityLoader.HasComponent(CompactWaterTurbineKey)) return;

      IObjectLoader component = entityLoader.GetComponent(CompactWaterTurbineKey);
      if (component.Has(FlowRateKey))
      {
        FlowRate = component.Get(FlowRateKey);
      }
      if (component.Has(IsSynchronizedKey))
      {
        IsSynchronized = component.Get(IsSynchronizedKey);
      }
    }

    public void DuplicateFrom(CompactWaterTurbine source)
    {
      FlowRate = source.FlowRate;
      IsSynchronized = source.IsSynchronized;
    }

    public void SetFlowRateAndSynchronize(float value)
    {
      SetFlowRate(value);
      SynchronizeAllNeighbors();
    }

    public void SetFlowRate(float value)
    {
      FlowRate = value;
    }

    public void ToggleSynchronization(bool newValue)
    {
      IsSynchronized = newValue;
      _synchronizer.SynchronizeWithAllNeighbors(this);
    }

    private void SynchronizeAllNeighbors()
    {
      _synchronizer.SynchronizeAllNeighbors(this);
    }

    private float MoveWater(float waterAmount)
    {
      float contamination = _threadSafeWaterMap.ColumnContamination(_waterInput.Coordinates);

      // Update the exposed property for the particle controller
      CurrentContamination = contamination;

      float availableDepth = _threadSafeWaterMap.WaterDepth(_waterInput.Coordinates) + _threadSafeWaterMap.ColumnOverflow(_waterInput.Coordinates);

      float totalToMove = Mathf.Min(waterAmount, availableDepth);
      if (totalToMove <= 0f) return 0f;

      float cleanMoved = totalToMove * (1f - contamination);
      float contamMoved = totalToMove * contamination;

      if (cleanMoved > 0f)
      {
        _waterService.RemoveCleanWater(_waterInput.Coordinates, cleanMoved);
        _waterService.AddCleanWater(_outputCoordinates, cleanMoved);
      }
      if (contamMoved > 0f)
      {
        _waterService.RemoveContaminatedWater(_waterInput.Coordinates, contamMoved);
        _waterService.AddContaminatedWater(_outputCoordinates, contamMoved);
      }

      return totalToMove;
    }

    private void UpdatePowerGeneration(float currentEffectiveFlow, float head)
    {
      if (MaxFlowRate <= 0f || currentEffectiveFlow <= 0.001f)
      {
        _mechanicalNode.SetOutputMultiplier(0f);
        return;
      }

      float safeHead = Mathf.Max(head, _spec.MinWaterDrop);
      float headMultiplier = Mathf.Clamp01((safeHead - _spec.MinWaterDrop) / (_spec.MaxWaterDrop - _spec.MinWaterDrop));
      float flowMultiplier = currentEffectiveFlow / MaxFlowRate;

      _mechanicalNode.SetOutputMultiplier(headMultiplier * flowMultiplier);
    }
  }

  public record CompactWaterTurbineSpec : ComponentSpec
  {
    [Serialize]
    public float MaxWaterPerSecond { get; init; } = 0.5f;

    [Serialize]
    public float MinWaterDrop { get; init; } = 1.0f;

    [Serialize]
    public float MaxWaterDrop { get; init; } = 5.0f;
  }
}