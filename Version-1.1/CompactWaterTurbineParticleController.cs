using System.Collections.Immutable;
using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;
using Timberborn.EntitySystem;
using Timberborn.Particles;
using Timberborn.Persistence;
using Timberborn.TemplateAttachmentSystem;
using Timberborn.TickSystem;
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

    private CompactWaterTurbine _turbine;
    private ParticlesRunner _particlesRunner;
    private ParticleSystem[] _particleSystems;

    private float[] _initialStartSpeeds;
    private float[] _initialEmissionRates;

    private float _lastFlowPercentage = -1f;

    public void Awake()
    {
      _turbine = GetComponent<CompactWaterTurbine>();
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

          for (int i = 0; i < _particleSystems.Length; i++)
          {
            _initialStartSpeeds[i] = _particleSystems[i].main.startSpeedMultiplier;
            _initialEmissionRates[i] = _particleSystems[i].emission.rateOverTimeMultiplier;
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
      // NEW: We no longer check CanMoveWater. We stay ON until the smoothed residual flow drops to 0.
      if (_turbine.EffectiveFlowRate > 0.01f)
      {
        if (_particleSystems != null)
        {
          float flowPercentage = _turbine.MaxFlowRate > 0f ? (_turbine.EffectiveFlowRate / _turbine.MaxFlowRate) : 0f;
          flowPercentage = Mathf.Clamp(flowPercentage, 0.01f, 1.0f);

          if (Mathf.Abs(_lastFlowPercentage - flowPercentage) > 0.005f)
          {
            _lastFlowPercentage = flowPercentage;

            float currentSpeed = Mathf.Lerp(MinHorizontalSpeed, MaxHorizontalSpeed, flowPercentage);
            float currentDensity = Mathf.Lerp(MinDensity, MaxDensity, flowPercentage);
            float currentGravity = Mathf.Lerp(MinFlowGravity, MaxFlowGravity, flowPercentage);

            for (int i = 0; i < _particleSystems.Length; i++)
            {
              var main = _particleSystems[i].main;
              var emission = _particleSystems[i].emission;

              main.startSpeedMultiplier = _initialStartSpeeds[i] * currentSpeed;
              emission.rateOverTimeMultiplier = _initialEmissionRates[i] * currentDensity;
              main.gravityModifierMultiplier = currentGravity;
            }
          }
        }

        _particlesRunner.Play();
      }
      else
      {
        _lastFlowPercentage = -1f;
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