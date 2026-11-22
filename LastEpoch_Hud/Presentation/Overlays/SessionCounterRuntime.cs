using System;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.UI;

// Registered alongside the other persistent mod components.
// Session accounting must keep running when the configuration HUD is closed.
[RegisterTypeInIl2Cpp]
public sealed class SessionCounterRuntime : MonoBehaviour
{
    public SessionCounterRuntime(IntPtr pointer)
        : base(pointer) { }

    void Update() => SessionGainCounters.Tick();

    void OnGUI() => SessionGainCounters.Draw();
}
