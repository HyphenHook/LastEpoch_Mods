using System;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;
using Il2CppItemFiltering;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>
/// Calls the native ItemFilter.Match directly. The generated wrapper mishandles its Nullable out
/// parameters (it reads struct bytes as an object pointer), so every out gets its own stack slot here.
/// </summary>
public static class LootFilterMatch
{
    private const int ArgCount = 9;

    private static readonly IntPtr _matchMethod = ResolveMatchMethod();

    public static unsafe bool TryMatch(
        ItemFilter filter,
        ItemDataUnpacked item,
        out Rule.RuleOutcome outcome
    )
    {
        outcome = default;
        if (_matchMethod == IntPtr.Zero)
        {
            return false;
        }

        IntPtr itemPointer = item.Pointer;
        ulong nullableInt = 0;
        ulong nullableBool = 0;
        int intA = 0;
        int intB = 0;
        int intC = 0;
        int flag = 0;
        int beamSize = 0;
        UnityEngine.Color color = default;
        void** args = stackalloc void*[ArgCount];
        args[0] = (void*)itemPointer;
        args[1] = &nullableInt;
        args[2] = &nullableBool;
        args[3] = &intA;
        args[4] = &intB;
        args[5] = &intC;
        args[6] = &flag;
        args[7] = &beamSize;
        args[8] = &color;

        IntPtr exception = IntPtr.Zero;
        IntPtr result = IL2CPP.il2cpp_runtime_invoke(
            _matchMethod,
            filter.Pointer,
            (void**)args,
            ref exception
        );
        Il2CppException.RaiseExceptionIfNecessary(exception);
        if (result == IntPtr.Zero)
        {
            return false;
        }

        outcome = *(Rule.RuleOutcome*)IL2CPP.il2cpp_object_unbox(result);
        return true;
    }

    private static IntPtr ResolveMatchMethod()
    {
        MethodInfo method = typeof(ItemFilter).GetMethod(
            nameof(ItemFilter.Match),
            new[]
            {
                typeof(ItemDataUnpacked),
                typeof(Il2CppSystem.Nullable<int>).MakeByRefType(),
                typeof(Il2CppSystem.Nullable<bool>).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType(),
                typeof(EpochColor.BeamSize).MakeByRefType(),
                typeof(UnityEngine.Color).MakeByRefType(),
            }
        );
        if (method == null || method.ReturnType != typeof(Rule.RuleOutcome))
        {
            return IntPtr.Zero;
        }

        FieldInfo field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(
            method
        );
        return field == null ? IntPtr.Zero : (IntPtr)field.GetValue(null);
    }
}
