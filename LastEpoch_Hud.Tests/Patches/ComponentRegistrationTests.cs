using LastEpoch_Hud.Tests.Support;
using Mono.Cecil;

namespace LastEpoch_Hud.Tests.Patches;

public sealed class ComponentRegistrationTests
{
    [Fact]
    public void RegisteredComponentsAvoidAbstractUnityBases()
    {
        GameEnvironment.SkipWithoutGame();
        GameEnvironment.SkipWithoutModBuild();
        using var module = GameEnvironment.ReadGameModule(GameEnvironment.ModDll);
        var registered = module.Types.Where(t => t.CustomAttributes.Any(a =>
            a.AttributeType.FullName == "MelonLoader.RegisterTypeInIl2Cpp" )).ToList();
        Assert.NotEmpty(registered);
        foreach (var type in registered)
        {
            // Il2CppInterop 1.5.1 fails in HandleAbstractMethod for BaseMeshEffect,
            // before mod initialization. Keep custom UI meshes outside class injection.
            TypeReference parent = type.BaseType;
            while (parent != null && parent.FullName != "UnityEngine.MonoBehaviour")
            {
                var definition = parent.Resolve();
                Assert.False(definition.IsAbstract && parent.Namespace.StartsWith("UnityEngine"),
                    $"{type.FullName} injects the abstract Unity base {parent.FullName}.");
                parent = definition.BaseType;
            }
        }
    }
}
