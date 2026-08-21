using System;
using GameNetcodeStuff;
using HarmonyLib;
using Peeker.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module.Impl
{
    public class SpeedModule : Module
    {
        internal static bool Active;

        // The Harmony hook is static, so it needs a way back to the live
        // instance's settings. Assigned in the ctor; the module registry only
        // ever constructs one of these.
        private static SpeedModule _instance;

        // ---- Cached vanilla state ------------------------------------------
        // Everything written in Apply is snapshotted here first so Restore can
        // put the player back exactly as the game left it. Apply always derives
        // from these values rather than from the live field, otherwise the
        // multiplier would compound every frame.
        private static PlayerControllerB _patched;
        private static float _baseSpeed;
        private static float _baseClimbSpeed;
        private static float _baseHinderedMultiplier;
        private static bool _hasCache;

        private readonly Setting<float> _multiplier;
        private readonly Setting<bool> _sprintOnly;
        private readonly Setting<bool> _ignoreWeight;
        private readonly Setting<bool> _ignoreHindrance;
        private readonly Setting<bool> _infiniteStamina;
        private readonly Setting<float> _climbMultiplier;
        private readonly Setting<bool> _clearCheatFlag;

        public SpeedModule() : base("Speed", Key.Z, ModuleCategory.Movement)
        {
            _multiplier = Register(Setting.Range("Multiplier", 2f, 1f, 10f)
                .WithDescription("Scales the player's base movement speed."));

            _sprintOnly = Register(Setting.Of("Sprint Only", false)
                .WithDescription("Only boost while sprint is held; walk at vanilla speed."));

            _ignoreWeight = Register(Setting.Of("Ignore Weight", true)
                .WithDescription("Cancels the slowdown from carried loot."));

            _ignoreHindrance = Register(Setting.Of("Ignore Hindrance", false)
                .WithDescription("Ignores slowdown sources such as mud or being grabbed."));

            _infiniteStamina = Register(Setting.Of("Infinite Stamina", true)
                .WithDescription("Keeps the sprint meter full and clears exhaustion."));

            _climbMultiplier = Register(Setting.Range("Climb Speed", 1f, 1f, 5f)
                .WithDescription("Ladder climb speed multiplier."));

            _clearCheatFlag = Register(Setting.Of("Clear Cheat Flag", true)
                .WithDescription("Resets the vanilla isSpeedCheating flag every frame."));

            _instance = this;
        }

        // Tells Module which patch container to apply on enable / unpatch on
        // disable. Without this the postfix below is never installed.
        protected override Type[] PatchTypes => new[] { typeof(SpeedModulePatch) };

        protected override void OnEnabled()
        {
            Active = true;
        }

        protected override void OnDisabled()
        {
            Active = false;
            Restore();
        }

        public override void Update()
        {
            // the patch drives everything so the writes
            // land in the same frame the game reads them
        }

        // runs every game frame ?
        internal static void Apply(PlayerControllerB player)
        {
            SpeedModule self = _instance;
            if (self == null || player == null)
                return;

            // Re-snapshot when the local player object changes (rejoin, respawn,
            // lobby reload). Comparing against a destroyed object is safe here:
            // Unity's == overload makes it compare equal to null.
            if (!_hasCache || _patched != player)
            {
                _patched = player;
                _baseSpeed = player.movementSpeed;
                _baseClimbSpeed = player.climbSpeed;
                _baseHinderedMultiplier = player.hinderedMultiplier;
                _hasCache = true;
            }

            bool boosting = !self._sprintOnly.Value || player.isSprinting;
            float speed = boosting ? _baseSpeed * self._multiplier.Value : _baseSpeed;

            // The movement solver divides base speed by carryWeight, so folding
            // carryWeight back in cancels the loot penalty exactly, without
            // touching carryWeight itself (that field is accumulated by
            // grab/discard and can't be safely restored).
            if (self._ignoreWeight.Value)
                speed *= Mathf.Max(1f, player.carryWeight);

            player.movementSpeed = speed;
            player.climbSpeed = _baseClimbSpeed * self._climbMultiplier.Value;

            // isMovementHindered is a refcount owned by the game, so only the
            // multiplier is overwritten; the count is left alone.
            if (self._ignoreHindrance.Value && player.isMovementHindered > 0)
                player.hinderedMultiplier = 1f;

            if (self._infiniteStamina.Value)
            {
                player.sprintMeter = 1f;
                player.isExhausted = false;
            }

            // Vanilla flags implausible movement on the owning client; clearing
            // it each frame stops the client-side reaction to that flag.
            if (self._clearCheatFlag.Value)
                player.isSpeedCheating = false;
        }

        private static void Restore()
        {
            if (!_hasCache)
                return;

            if (_patched != null)
            {
                _patched.movementSpeed = _baseSpeed;
                _patched.climbSpeed = _baseClimbSpeed;
                _patched.hinderedMultiplier = _baseHinderedMultiplier;
            }

            _patched = null;
            _hasCache = false;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class SpeedModulePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        private static void Postfix(PlayerControllerB __instance)
        {
            if (!SpeedModule.Active)
                return;

            StartOfRound round = StartOfRound.Instance;
            if (round == null || __instance != round.localPlayerController)
                return;

            SpeedModule.Apply(__instance);
        }
    }
}