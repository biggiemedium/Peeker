using System;
using GameNetcodeStuff;
using HarmonyLib;
using Peeker.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module.Impl.Combat
{
    /// <summary>
    /// Keeps the local player alive and clean of status effects.
    ///
    /// Damage and death are cut off at the source rather than papered over after
    /// the fact: <c>PlayerControllerB.DamagePlayer</c> and
    /// <c>PlayerControllerB.KillPlayer</c> are both prefixed, so the health drop,
    /// the audio, the HUD flash and the ragdoll never happen at all. The
    /// per-frame pass only handles the accumulating effects — sinking, drunkness,
    /// insanity, bleeding — which nothing calls a method for.
    ///
    /// Everything here is client side. Other players still see whatever the
    /// server thinks happened, which is why the death block is scoped to the
    /// local player: blocking KillPlayer for everyone would leave remote corpses
    /// walking around.
    /// </summary>
    public class SurvivalModule : Module
    {
        internal static bool Active;

        // The Harmony hooks are static, so they need a way back to the live
        // instance's settings. The registry only ever constructs one of these.
        private static SurvivalModule _instance;

        private const int MaxHealth = 100;

        private readonly Setting<bool> _blockDamage;
        private readonly Setting<bool> _noFallDamage;
        private readonly Setting<bool> _blockDeath;
        private readonly Setting<bool> _autoHeal;
        private readonly Setting<int> _healBelow;
        private readonly Setting<bool> _clearInjury;
        private readonly Setting<bool> _stopBleeding;
        private readonly Setting<bool> _stopSinking;
        private readonly Setting<bool> _clearDrunkness;
        private readonly Setting<bool> _clearInsanity;

        public SurvivalModule() : base("Survival", Key.H, ModuleCategory.Combat)
        {
            _blockDamage = Register(Setting.Of("Block Damage", true)
                .WithDescription("Drops every incoming damage call before it touches your health."));

            // Redundant while Block Damage is on, so it only shows when the
            // broader switch is off and the distinction actually matters.
            _noFallDamage = Register(Setting.Of("No Fall Damage", true)
                .WithVisibility(_ => !_blockDamage.Value)
                .WithDescription("Drops damage the game tagged as a fall, and leaves everything else alone."));

            _blockDeath = Register(Setting.Of("Block Death", true)
                .WithDescription("Cancels the kill itself, including instant kills that skip damage entirely."));

            _autoHeal = Register(Setting.Of("Auto Heal", true)
                .WithDescription("Tops your health back up every frame. Catches you up if you enable this while already hurt."));

            _healBelow = Register(Setting.Range("Heal Below", 100, 1, 100)
                .WithVisibility(_ => _autoHeal.Value)
                .WithDescription("Only heal once health has dropped under this."));

            _clearInjury = Register(Setting.Of("Clear Injury", true)
                .WithDescription("Cancels the critically injured state and the limp that comes with it."));

            _stopBleeding = Register(Setting.Of("Stop Bleeding", true)
                .WithDescription("Stops the heavy bleeding that drops blood decals behind you."));

            _stopSinking = Register(Setting.Of("Stop Sinking", true)
                .WithDescription("Clears quicksand and mud sinking, locally."));

            _clearDrunkness = Register(Setting.Of("Clear Drunkness", true)
                .WithDescription("Zeroes the screen distortion from apparatus fumes and similar sources."));

            _clearInsanity = Register(Setting.Of("Clear Insanity", true)
                .WithDescription("Holds your fear level at zero, so the sanity effects never build up."));

            _instance = this;
        }

        // Tells Module which patch container to apply on enable / unpatch on
        // disable. Without this none of the hooks below are installed.
        protected override Type[] PatchTypes => new[] { typeof(SurvivalPatch) };

        protected override void OnEnabled()
        {
            Active = true;
            Plugin.Log.LogInfo("Survival on");
        }

        protected override void OnDisabled()
        {
            Active = false;
            Plugin.Log.LogInfo("Survival off");
        }

        public override void Update()
        {
            // the patch drives everything so the writes land in the same frame
            // the game reads them
        }

        // ---- Hook bodies ----------------------------------------------------

        /// <summary>True when this damage call should be dropped entirely.</summary>
        internal static bool ShouldBlockDamage(bool fallDamage)
        {
            SurvivalModule self = _instance;
            if (self == null)
                return false;

            return self._blockDamage.Value || (fallDamage && self._noFallDamage.Value);
        }

        internal static bool ShouldBlockDeath()
        {
            SurvivalModule self = _instance;
            return self != null && self._blockDeath.Value;
        }

        // runs every game frame
        internal static void Sustain(PlayerControllerB player)
        {
            SurvivalModule self = _instance;
            if (self == null || player == null)
                return;

            // Once the player is actually dead the game owns the screen; writing
            // health back would leave a corpse with a full health bar.
            if (player.isPlayerDead)
                return;

            if (self._autoHeal.Value && player.health < self._healBelow.Value)
            {
                player.health = MaxHealth;

                // health is just a field — the bar only redraws when something
                // tells it to, and nothing will now that DamagePlayer is skipped.
                if (HUDManager.Instance != null)
                    HUDManager.Instance.UpdateHealthUI(MaxHealth, false);
            }

            // Guarded because this plays audio and rebuilds the injury effects;
            // calling it unconditionally would retrigger them 60 times a second.
            if (self._clearInjury.Value && player.criticallyInjured)
                player.MakeCriticallyInjured(false);

            if (self._stopBleeding.Value && player.bleedingHeavily)
                player.bleedingHeavily = false;

            if (self._stopSinking.Value && (player.isSinking || player.sinkingValue > 0f))
            {
                // sourcesCausingSinking is a refcount the game increments from
                // trigger volumes, so it has to be cleared too — otherwise the
                // next frame turns sinking straight back on.
                player.sourcesCausingSinking = 0;
                player.isSinking = false;
                player.sinkingValue = 0f;
            }

            if (self._clearDrunkness.Value && player.drunkness > 0f)
            {
                player.drunkness = 0f;
                player.drunknessInertia = 0f;
            }

            if (self._clearInsanity.Value && player.insanityLevel > 0f)
                player.insanityLevel = 0f;

            if (self._blockDamage.Value || self._noFallDamage.Value)
                player.takingFallDamage = false;
        }

        /// <summary>
        /// DamagePlayer and KillPlayer are prefixed rather than postfixed: a
        /// postfix would run after the health write, the death animation and the
        /// RPCs have already gone out.
        /// </summary>
        [HarmonyPatch(typeof(PlayerControllerB))]
        private static class SurvivalPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("DamagePlayer")]
            private static bool DamagePlayerPrefix(PlayerControllerB __instance, bool fallDamage)
            {
                if (!Active || !IsLocal(__instance))
                    return true;

                return !ShouldBlockDamage(fallDamage);
            }

            [HarmonyPrefix]
            [HarmonyPatch("KillPlayer")]
            private static bool KillPlayerPrefix(PlayerControllerB __instance)
            {
                if (!Active || !IsLocal(__instance))
                    return true;

                return !ShouldBlockDeath();
            }

            [HarmonyPostfix]
            [HarmonyPatch("Update")]
            private static void UpdatePostfix(PlayerControllerB __instance)
            {
                if (!Active || !IsLocal(__instance))
                    return;

                Sustain(__instance);
            }

            // KillPlayer also runs for remote players from the kill RPC, so the
            // block has to be scoped or everyone else stops dying on our screen.
            private static bool IsLocal(PlayerControllerB player)
            {
                StartOfRound round = StartOfRound.Instance;
                return round != null && player == round.localPlayerController;
            }
        }
    }
}
