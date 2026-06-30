using Bindito.Core;
using Timberborn.EntityPanelSystem;
using Timberborn.Particles;
using Timberborn.TemplateInstantiation;

namespace Calloatti.CompactWaterTurbine
{
  [Context("Game")]
  public class CompactWaterTurbineConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<CompactWaterTurbineSynchronizer>().AsSingleton();

      Bind<CompactWaterTurbine>().AsTransient();
      Bind<CompactWaterTurbineParticleController>().AsTransient();
      Bind<CompactWaterTurbineFragment>().AsSingleton();

      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
      MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      TemplateModule.Builder builder = new TemplateModule.Builder();
      builder.AddDecorator<CompactWaterTurbineSpec, CompactWaterTurbine>();
      builder.AddDecorator<CompactWaterTurbineParticleControllerSpec, CompactWaterTurbineParticleController>();
      builder.AddDecorator<CompactWaterTurbineParticleController, ParticlesCache>();
      return builder.Build();
    }

    private class EntityPanelModuleProvider : IProvider<EntityPanelModule>
    {
      private readonly CompactWaterTurbineFragment _fragment;

      public EntityPanelModuleProvider(CompactWaterTurbineFragment fragment)
      {
        _fragment = fragment;
      }

      public EntityPanelModule Get()
      {
        EntityPanelModule.Builder builder = new EntityPanelModule.Builder();
        builder.AddTopFragment(_fragment);
        return builder.Build();
      }
    }
  }
}