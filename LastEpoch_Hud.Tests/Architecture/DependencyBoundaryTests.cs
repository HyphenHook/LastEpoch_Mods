using LastEpoch_Hud.Tests.Support;
using Mono.Cecil;

namespace LastEpoch_Hud.Tests.Architecture;

public sealed class DependencyBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Core_HasNoEngineOrHostDependencies(bool builtMod)
    {
        if (builtMod) GameEnvironment.SkipWithoutModBuild();
        using var module = ModuleDefinition.ReadModule(builtMod
            ? GameEnvironment.ModDll : typeof(DependencyBoundaryTests).Assembly.Location);
        var core = module.GetTypes().Where(type => IsCore(Namespace(type))).ToList();
        Assert.NotEmpty(core);

        var offenders = core.SelectMany(type => Dependencies(type)
                .SelectMany(Expand)
                .Where(reference => IsEngineOrHost(reference.Namespace))
                .Select(reference => $"{type.FullName} -> {reference.FullName}"))
            .Distinct().Order().ToList();

        Assert.True(offenders.Count == 0,
            "Core must remain independent of engines and hosts:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void RuntimeDispatcher_HasNoFeatureOrPresentationDependencies()
    {
        using var module = ModuleDefinition.ReadModule(typeof(DependencyBoundaryTests).Assembly.Location);
        var runtime = module.GetTypes()
            .Where(type => Namespace(type) == "LastEpoch_Hud.Application.Runtime").ToList();
        Assert.NotEmpty(runtime);
        var offenders = runtime.SelectMany(type => Dependencies(type).SelectMany(Expand)
                .Where(reference => IsEngineOrHost(reference.Namespace)
                    || (reference.Namespace.StartsWith("LastEpoch_Hud.")
                        && !IsCore(reference.Namespace)
                        && reference.Namespace != "LastEpoch_Hud.Application.Runtime"))
                .Select(reference => $"{type.FullName} -> {reference.FullName}"))
            .Distinct().Order().ToList();
        Assert.True(offenders.Count == 0,
            "Runtime dispatch must depend on contracts, not concrete features:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Main_LifecycleDoesNotCallConcreteFeatures()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var module = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var main = module.GetType("LastEpoch_Hud.Main");
        Assert.NotNull(main);
        var callbacks = main.Methods.Where(method => method.IsVirtual && method.Name.StartsWith("On")).ToList();
        Assert.NotEmpty(callbacks);
        var offenders = callbacks.Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions
                .Where(instruction => instruction.Operand is MemberReference)
                .Select(instruction => (MemberReference)instruction.Operand)
                .Where(member => member.DeclaringType != null)
                .Where(member => member.DeclaringType.Namespace.StartsWith("LastEpoch_Hud.Scripts.Mods.")
                    || member.DeclaringType.Namespace.StartsWith("LastEpoch_Hud.Scripts.ModUI")
                    || member.DeclaringType.Namespace == "LastEpoch_Hud.Scripts.Dev")
                .Select(member => $"{method.Name} -> {member.FullName}"))
            .Distinct().Order().ToList();
        Assert.True(offenders.Count == 0,
            "Main lifecycle callbacks must dispatch through composition/runtime adapters:\n" + string.Join("\n", offenders));
    }

    private static bool IsCore(string ns) => ns == "LastEpoch_Hud.Scripts.Core"
        || ns.StartsWith("LastEpoch_Hud.Scripts.Core.")
        || ns == "LastEpoch_Hud.Core" || ns.StartsWith("LastEpoch_Hud.Core.");

    private static bool IsEngineOrHost(string ns) => ns.StartsWith("UnityEngine")
        || ns.StartsWith("Il2Cpp") || ns.StartsWith("HarmonyLib") || ns.StartsWith("MelonLoader");

    private static string Namespace(TypeDefinition type) =>
        type.DeclaringType == null ? type.Namespace : Namespace(type.DeclaringType);

    private static IEnumerable<TypeReference> Expand(TypeReference type)
    {
        if (type == null) yield break;
        yield return type;
        if (type.DeclaringType != null)
            foreach (var reference in Expand(type.DeclaringType)) yield return reference;
        if (type is TypeSpecification specification)
            foreach (var reference in Expand(specification.ElementType)) yield return reference;
        if (type is GenericInstanceType generic)
            foreach (var argument in generic.GenericArguments)
                foreach (var reference in Expand(argument)) yield return reference;
    }

    private static IEnumerable<TypeReference> Dependencies(TypeDefinition type)
    {
        yield return type.BaseType;
        foreach (var implementation in type.Interfaces) yield return implementation.InterfaceType;
        foreach (var attribute in type.CustomAttributes) yield return attribute.AttributeType;
        foreach (var field in type.Fields) yield return field.FieldType;
        foreach (var property in type.Properties) yield return property.PropertyType;
        foreach (var eventDefinition in type.Events) yield return eventDefinition.EventType;
        foreach (var parameter in type.GenericParameters)
            foreach (var constraint in parameter.Constraints) yield return constraint.ConstraintType;
        foreach (var method in type.Methods)
        {
            yield return method.ReturnType;
            foreach (var parameter in method.Parameters) yield return parameter.ParameterType;
            foreach (var parameter in method.GenericParameters)
                foreach (var constraint in parameter.Constraints) yield return constraint.ConstraintType;
            foreach (var attribute in method.CustomAttributes) yield return attribute.AttributeType;
            if (!method.HasBody) continue;
            foreach (var local in method.Body.Variables) yield return local.VariableType;
            foreach (var handler in method.Body.ExceptionHandlers) yield return handler.CatchType;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is TypeReference reference) yield return reference;
                if (instruction.Operand is FieldReference field)
                {
                    yield return field.DeclaringType;
                    yield return field.FieldType;
                }
                if (instruction.Operand is MethodReference call)
                {
                    yield return call.DeclaringType;
                    yield return call.ReturnType;
                    foreach (var parameter in call.Parameters) yield return parameter.ParameterType;
                    if (call is GenericInstanceMethod generic)
                        foreach (var argument in generic.GenericArguments) yield return argument;
                }
            }
        }
    }
}
