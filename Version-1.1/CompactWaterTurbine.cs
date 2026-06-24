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
    private float _overflowPressureFactor = 20f; // Fallback, will be overwritten by the true game spec

    private readonly ITickService _tickService;
    private readonly IThreadSafeWaterMap _threadSafeWaterMap;
    private readonly CompactWaterTurbineSynchronizer _synchronizer;
    private readonly ISpecService _specService;

    private MechanicalNode _mechanicalNode;
    private WaterInput _waterInput;
    private WaterOutput _waterOutput;
    private CompactWaterTurbineSpec _spec;
    private BlockObject _blockObject;

    private Vector3Int _outputCoordinates;

    private bool _isWaterFlowActive;

    public bool CanMoveWater => _isWaterFlowActive;
    public float FlowRate { get; private set; }

    // Smoothed output representing mechanical momentum
    public float EffectiveFlowRate { get; private set; }
    public float MaxFlowRate => _spec.MaxWaterPerSecond;
    public bool IsSynchronized { get; private set; } = true;

    // INJECTED: ISpecService to pull the game's internal pressure physics variables
    public CompactWaterTurbine(ITickService tickService, IThreadSafeWaterMap threadSafeWaterMap, CompactWaterTurbineSynchronizer synchronizer, ISpecService specService)
    {
      _tickService = tickService;
      _threadSafeWaterMap = threadSafeWaterMap;
      _synchronizer = synchronizer;
      _specService = specService;
    }

    public void Awake()
    {
      _mechanicalNode = GetComponent<MechanicalNode>();
      _waterInput = GetComponent<WaterInput>();
      _waterOutput = GetComponent<WaterOutput>();
      _spec = GetComponent<CompactWaterTurbineSpec>();
      _blockObject = GetComponent<BlockObject>();
      FlowRate = _spec.MaxWaterPerSecond;

      // Pull the actual physical pressure multiplier from the game engine
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

      float rawEffectiveFlow = 0f;
      if (CanMoveWater)
      {
        float requestedWater = _tickService.TickIntervalInSeconds * GetFlowCapacity();
        float actuallyMoved = MoveWater(requestedWater);
        rawEffectiveFlow = actuallyMoved / _tickService.TickIntervalInSeconds;
      }

      float rampRate = MaxFlowRate / RampDurationSeconds;
      EffectiveFlowRate = Mathf.MoveTowards(EffectiveFlowRate, rawEffectiveFlow, rampRate * _tickService.TickIntervalInSeconds);

      UpdatePowerGeneration(EffectiveFlowRate, _cachedHead);
    }

    private void UpdateFlowState()
    {
      // Calculate true available physical water mass
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
      float inputHeight = _threadSafeWaterMap.WaterHeightOrFloor(_waterInput.Coordinates);
      // FIXED: Multiply the physical compressed mass by the pressure factor to calculate true head in meters!
      float inputPressure = _threadSafeWaterMap.ColumnOverflow(_waterInput.Coordinates) * _overflowPressureFactor;
      float totalInputHead = inputHeight + inputPressure;

      float rawOutputHeight = _threadSafeWaterMap.WaterHeightOrFloor(_outputCoordinates);
      // FIXED: Apply backpressure correctly for submerged outputs
      float outputPressure = _threadSafeWaterMap.ColumnOverflow(_outputCoordinates) * _overflowPressureFactor;
      float totalOutputHead = rawOutputHeight + outputPressure;

      float turbineTopHeight = _blockObject.Coordinates.z + 1.0f;
      float effectiveOutputHeight = Mathf.Max(totalOutputHead, turbineTopHeight);

      return totalInputHead - effectiveOutputHeight;
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

      // We do NOT multiply by the pressure factor here, because we are moving physical volume, not height!
      float availableDepth = _threadSafeWaterMap.WaterDepth(_waterInput.Coordinates) + _threadSafeWaterMap.ColumnOverflow(_waterInput.Coordinates);

      float totalToMove = Mathf.Min(waterAmount, availableDepth);
      if (totalToMove <= 0f) return 0f;

      float cleanMoved = totalToMove * (1f - contamination);
      float contamMoved = totalToMove * contamination;

      if (cleanMoved > 0f) _waterInput.RemoveCleanWater(cleanMoved);
      if (contamMoved > 0f) _waterInput.RemoveContaminatedWater(contamMoved);

      _waterOutput.AddWater(cleanMoved, contamMoved);

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