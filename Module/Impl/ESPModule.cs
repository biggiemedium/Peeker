using System;
using GameNetcodeStuff;
using HarmonyLib;
using Peeker;
using Peeker.Module;
using UnityEngine;
using UnityEngine.InputSystem;

public class ESPModule : Module
{
    public ESPModule() : base("ESP", Key.G, ModuleCategory.Visual)
    {
        SetToggled(true); // on by default -> ApplyPatches runs here
    }

    protected override Type[] PatchTypes => new[] { typeof(ESPPatches) };

    protected override void OnEnabled()
    {
        Plugin.Log.LogInfo("ESP enabled");
    }

    protected override void OnDisabled()
    {
        Plugin.Log.LogInfo("ESP disabled");
    }
}

[HarmonyPatch(typeof(PlayerControllerB), "Update")]
public static class ESPPatches
{
    [HarmonyPostfix]
    public static void PlayerUpdate()
    {
        
    }
}