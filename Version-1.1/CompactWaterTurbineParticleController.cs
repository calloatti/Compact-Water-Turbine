using System.Collections.Immutable;
using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;
using Timberborn.EntitySystem;
using Timberborn.Particles;
using Timberborn.Persistence;
using Timberborn.TemplateAttachmentSystem;
using Timberborn.TickSystem;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  public class CompactWaterTurbineParticleController : TickableComponent, IAwakableComponent, IInitializableEntity, IPostLoadableEntity
  {
    public const float MaxFlowGravity = 0.69f;
    public const float MinFlowGravity = 1.0f;

    private const float MaxDensity = 1.42f;
    private const float MinDensity = 0.2f;

    private const float MaxHorizontalSpeed = 0.71f;
    private const float MinHorizontalSpeed = 0.1f;

    private const float PermanentSizeMultiplier = 3.0f;
    private const float UnityGravity = 9.81f;
    private const float MinimumLifetime = 0.1f;

    private static readonly Color BadwaterColor = new Color(0.46f, 0.15f, 0.09f, 1.0f);

    private CompactWaterTurbine _turbine;
    private WaterOutput _waterOutput;
    private ParticlesRunner _particlesRunner;
    private ParticleSystem[] _particleSystems;

    private float[] _initialStartSpeeds;
    private float[] _initialEmissionRates;
    private Color[] _initialColors;

    private float _lastFlowPercentage = -1f;
    private float _lastContamination = -1f;

    public void Awake()
    {
      _turbine = GetComponent<CompactWaterTurbine>();
      _waterOutput = GetComponent<WaterOutput>();
    }

    public void InitializeEntity()
    {
      ImmutableArray<string> attachmentIds = GetComponent<CompactWaterTurbineParticleControllerSpec>().AttachmentIds;
      _particlesRunner = GetComponent<ParticlesCache>().GetParticlesRunner(attachmentIds);

      if (attachmentIds.Length > 0)
      {
        var attachment = GetComponent<TemplateAttachments>().GetOrCreateAttachment(attachmentIds[0]);
        if (attachment != null)
        {
          _particleSystems = attachment.GameObject.GetComponentsInChildren<ParticleSystem>(true);

          _initialStartSpeeds = new float[_particleSystems.Length];
          _initialEmissionRates = new float[_particleSystems.Length];
          _initialColors = new Color[_particleSystems.Length];

          for (int i = 0; i < _particleSystems.Length; i++)
          {
            _initialStartSpeeds[i] = _particleSystems[i].main.startSpeedMultiplier;
            _initialEmissionRates[i] = _particleSystems[i].emission.rateOverTimeMultiplier;
            _initialColors[i] = _particleSystems[i].main.startColor.color;

            var main = _particleSystems[i].main;
            main.startSizeMultiplier *= PermanentSizeMultiplier;
          }
        }
      }
    }

    public void PostLoadEntity()
    {
      UpdateParticles();
    }

    public override void Tick()
    {
      UpdateParticles();
    }

    private void UpdateParticles()
    {
      if (_turbine.EffectiveFlowRate > 0.01f)
      {
        if (_particleSystems != null && _particleSystems.Length > 0)
        {
          float flowPercentage = _turbine.MaxFlowRate > 0f ? (_turbine.EffectiveFlowRate / _turbine.MaxFlowRate) : 0f;
          flowPercentage = Mathf.Clamp(flowPercentage, 0.01f, 1.0f);

          float currentContamination = _turbine.CurrentContamination;

          float currentGravity = Mathf.Lerp(MinFlowGravity, MaxFlowGravity, flowPercentage);

          // Read the precise visual Y height of the emitter nozzle asset
          float spoutAbsoluteHeight = _particleSystems[0].transform.position.y;
          float waterSurfaceAbsoluteHeight = _turbine.GetWaterSurfaceAbsoluteHeight();

          float verticalDistance = Mathf.Max(0f, spoutAbsoluteHeight - waterSurfaceAbsoluteHeight);

          float effectiveGravity = UnityGravity * currentGravity;
          float calculatedLifetime = Mathf.Sqrt((2f * verticalDistance) / effectiveGravity);

          // Keep the 75% immersion formula intact
          calculatedLifetime /= 0.75f;
          float finalLifetime = Mathf.Max(MinimumLifetime, calculatedLifetime);

          bool fluidPropsChanged = Mathf.Abs(_lastFlowPercentage - flowPercentage) > 0.005f || Mathf.Abs(_lastContamination - currentContamination) > 0.005f;

          if (fluidPropsChanged)
          {
            _lastFlowPercentage = flowPercentage;
            _lastContamination = currentContamination;
          }

          float currentSpeed = Mathf.Lerp(MinHorizontalSpeed, MaxHorizontalSpeed, flowPercentage);
          float currentDensity = Mathf.Lerp(MinDensity, MaxDensity, flowPercentage);

          for (int i = 0; i < _particleSystems.Length; i++)
          {
            var main = _particleSystems[i].main;

            main.startLifetime = finalLifetime;
            main.gravityModifierMultiplier = currentGravity;

            if (fluidPropsChanged)
            {
              var emission = _particleSystems[i].emission;

              main.startSpeedMultiplier = _initialStartSpeeds[i] * currentSpeed;
              emission.rateOverTimeMultiplier = _initialEmissionRates[i] * currentDensity;

              Color targetColor = Color.Lerp(_initialColors[i], BadwaterColor, currentContamination);
              main.startColor = new ParticleSystem.MinMaxGradient(targetColor);
            }
          }
        }

        _particlesRunner.Play();
      }
      else
      {
        _lastFlowPercentage = -1f;
        _lastContamination = -1f;
        _particlesRunner.Stop();
      }
    }
  }

  public record CompactWaterTurbineParticleControllerSpec : ComponentSpec
  {
    [Serialize]
    public ImmutableArray<string> AttachmentIds { get; init; }
  }
}