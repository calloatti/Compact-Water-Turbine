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

    private const float PermanentSizeMultiplier = 3.5f;
    private const float UnityGravity = 9.81f;
    private const float MinimumLifetime = 0.1f;

    // Exact colors extracted from Timberborn 1.1 asset blueprints
    //private static readonly Color DeepCleanWaterColor = new Color(0.196911722f, 0.57316947f, 0.7075472f, 1.0f);
    //private static readonly Color BadwaterColor = new Color(0.9529412f, 0.321568638f, 0.192156866f, 1.0f);

    // Exact colors extracted from Timberborn 1.1 asset blueprints (Darkened by 30%)
    private static readonly Color DeepCleanWaterColor = new Color(0.1378382f, 0.4012186f, 0.4952830f, 1.0f);
    private static readonly Color BadwaterColor = new Color(0.6670588f, 0.2250980f, 0.1345098f, 1.0f);

    private CompactWaterTurbine _turbine;
    private WaterOutput _waterOutput;
    private ParticlesRunner _particlesRunner;
    private ParticleSystem[] _particleSystems;

    private float[] _initialStartSpeeds;
    private float[] _initialEmissionRates;
    private Color[] _initialColors;

    private float _lastFlowPercentage = -1f;
    private float _lastContamination = -1f;

    // NEW CACHE VARIABLES
    private float _lastWaterSurfaceHeight = -1f;
    private float _cachedLifetime = MinimumLifetime;
    private float _cachedGravity = MinFlowGravity;

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
          float waterSurfaceAbsoluteHeight = _turbine.GetWaterSurfaceAbsoluteHeight();

          // Check if fluid properties OR the physical drop distance changed
          bool fluidPropsChanged = Mathf.Abs(_lastFlowPercentage - flowPercentage) > 0.005f || Mathf.Abs(_lastContamination - currentContamination) > 0.005f;
          bool heightChanged = Mathf.Abs(_lastWaterSurfaceHeight - waterSurfaceAbsoluteHeight) > 0.01f;

          // Only recalculate gravity and lifetime if something actually shifted
          if (fluidPropsChanged || heightChanged)
          {
            _lastFlowPercentage = flowPercentage;
            _lastContamination = currentContamination;
            _lastWaterSurfaceHeight = waterSurfaceAbsoluteHeight;

            _cachedGravity = Mathf.Lerp(MinFlowGravity, MaxFlowGravity, flowPercentage);

            float spoutAbsoluteHeight = _particleSystems[0].transform.position.y;
            float verticalDistance = Mathf.Max(0f, spoutAbsoluteHeight - waterSurfaceAbsoluteHeight);

            float effectiveGravity = UnityGravity * _cachedGravity;
            float calculatedLifetime = Mathf.Sqrt((2f * verticalDistance) / effectiveGravity);
            calculatedLifetime /= 0.75f;

            _cachedLifetime = Mathf.Max(MinimumLifetime, calculatedLifetime);
          }

          float currentSpeed = Mathf.Lerp(MinHorizontalSpeed, MaxHorizontalSpeed, flowPercentage);
          float currentDensity = Mathf.Lerp(MinDensity, MaxDensity, flowPercentage);

          for (int i = 0; i < _particleSystems.Length; i++)
          {
            var main = _particleSystems[i].main;

            // Constantly apply the cached values (costs virtually nothing compared to math)
            main.startLifetime = _cachedLifetime;
            main.gravityModifierMultiplier = _cachedGravity;

            // Only update colors and emission rates if the fluid properties changed
            if (fluidPropsChanged)
            {
              var emission = _particleSystems[i].emission;

              main.startSpeedMultiplier = _initialStartSpeeds[i] * currentSpeed;
              emission.rateOverTimeMultiplier = _initialEmissionRates[i] * currentDensity;

              // Force the 1.0 engine to blend using the precise 1.1 blueprint color gradient
              Color targetColor = Color.Lerp(DeepCleanWaterColor, BadwaterColor, currentContamination);
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
        _lastWaterSurfaceHeight = -1f;
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