using System;
using GameNetcodeStuff;
using HarmonyLib;
using Peeker;
using Peeker.Module;
using UnityEngine;
using UnityEngine.InputSystem;

public class NightVisionModule : Module
{
    // Vanilla nightVision sits around range 12 / intensity 366.
    private const float Range     = 10000f;
    private const float Intensity = 1000f;
    private static readonly Color Tint = new Color(0.82f, 0.90f, 1f);

    internal static bool Active;

    private static Light _light;
    private static bool  _cached;
    private static float _origRange;
    private static float _origIntensity;
    private static Color _origColour;
    private static LightShadows _origShadows;

    public NightVisionModule() : base("NightVision", Key.N, ModuleCategory.Visual) { }

    protected override Type[] PatchTypes => new[] { typeof(NightVisionPatch) };

    protected override void OnEnabled()
    {
        Active = true;
        Plugin.Log.LogInfo("NightVision on");
    }

    protected override void OnDisabled()
    {
        Active = false;
        Restore();
        Plugin.Log.LogInfo("NightVision off");
    }

    internal static void Apply(PlayerControllerB p)
    {
        var nv = p.nightVision;
        if (nv == null) return;

        // Re-cache if the light instance changed (scene reload, respawn).
        if (!_cached || _light != nv)
        {
            _light         = nv;
            _origRange     = nv.range;
            _origIntensity = nv.intensity;
            _origColour    = nv.color;
            _origShadows   = nv.shadows;
            _cached        = true;
        }

        // Only write when the value actually differs — every setter on Light
        // marks the HDRP light data dirty, and this runs every frame.
        if (!nv.enabled) nv.enabled = true;
        if (nv.range != Range) nv.range = Range;
        if (nv.intensity != Intensity) nv.intensity = Intensity;
        if (nv.color != Tint) nv.color = Tint;
        if (nv.shadows != LightShadows.None) nv.shadows = LightShadows.None;
    }

    private static void Restore()
    {
        if (!_cached || _light == null) { _cached = false; return; }

        _light.range     = _origRange;
        _light.intensity = _origIntensity;
        _light.color     = _origColour;
        _light.shadows   = _origShadows;
        // Leave `enabled` alone — PlayerControllerB.Update owns it and will
        // set it correctly on the next tick.

        _cached = false;
        _light  = null;
    }
}

[HarmonyPatch(typeof(PlayerControllerB))]
internal static class NightVisionPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    private static void Postfix(PlayerControllerB __instance)
    {
        if (!NightVisionModule.Active) return;

        var round = StartOfRound.Instance;
        if (round == null || __instance != round.localPlayerController) return;

        NightVisionModule.Apply(__instance);
    }
}