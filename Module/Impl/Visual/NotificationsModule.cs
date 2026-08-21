using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GameNetcodeStuff;
using HarmonyLib;
using Peeker.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module.Impl.Visual
{
    public enum SpawnFilter
    {
        All,
        InsideOnly,
        OutsideOnly
    }

    public class NotificationsModule : Module
    {
        internal static bool Active;

        private static NotificationsModule _instance;

        // How long the vanilla notification animation occupies the banner.
        private const float VanillaNotificationLength = 3f;

        private const int MaxNamesPerPopup = 3;

        private struct PendingSpawn
        {
            public string Name;
            public int Count;
            public float Distance;
        }

        private static readonly List<PendingSpawn> _pending = new List<PendingSpawn>();

        // EnemyAI.Start can fire more than once for the same object in edge
        // cases (re-enable, pooled reuse), so spawns are deduped by instance id.
        private static readonly HashSet<int> _announced = new HashSet<int>();

        private static float _lastShown;
        private static float _lastVanillaNotification;
        private static float _levelStartedAt;
        private static bool _wasInShipPhase = true;
        private static bool _showingOwnNotification;

        // HUDManager.DisplayGlobalNotification is private, so it's bound once
        // as an open-instance delegate instead of reflected per call.
        private static Action<HUDManager, string> _displayGlobal;
        private static bool _displayGlobalResolved;

        private readonly Setting<SpawnFilter> _filter;
        private readonly Setting<bool> _daytimeEnemies;
        private readonly Setting<bool> _showDistance;
        private readonly Setting<bool> _limitRange;
        private readonly Setting<float> _range;
        private readonly Setting<float> _cooldown;
        private readonly Setting<float> _gracePeriod;

        public NotificationsModule() : base("Notifications", Key.None, ModuleCategory.Visual)
        {
            _filter = Register(Setting.Of("Filter", SpawnFilter.All)
                .WithDescription("Which spawns to announce, by where the entity spawned."));

            _daytimeEnemies = Register(Setting.Of("Daytime Entities", false)
                .WithDescription("Include harmless daytime spawns such as manticoils."));

            _showDistance = Register(Setting.Of("Show Distance", true)
                .WithDescription("Append the distance from you at the moment of the spawn."));

            _limitRange = Register(Setting.Of("Limit Range", false)
                .WithDescription("Ignore spawns further away than the range below."));

            // Visibility is a predicate over this setting's own value, so the
            // parameter is ignored and the sibling toggle is closed over.
            _range = Register(Setting.Range("Range", 150f, 10f, 500f)
                .WithVisibility(_ => _limitRange.Value)
                .WithDescription("Maximum spawn distance to announce, in metres."));

            _cooldown = Register(Setting.Range("Cooldown", 1.5f, 0.25f, 10f)
                .WithDescription("Minimum seconds between popups. Spawns inside the window are merged."));

            _gracePeriod = Register(Setting.Range("Grace Period", 5f, 0f, 60f)
                .WithDescription("Seconds after landing or joining where spawns are ignored, to skip the initial wave."));

            _instance = this;
        }

        protected override Type[] PatchTypes => new[]
        {
            typeof(EnemySpawnPatch),
            typeof(NotificationPumpPatch),
            typeof(VanillaNotificationPatch)
        };

        protected override void OnEnabled()
        {
            // Stamped here so enabling mid-round doesn't dump a backlog and so
            // the grace period is meaningful when joining a game in progress.
            _levelStartedAt = Time.time;
            _lastShown = 0f;
            _pending.Clear();
            _announced.Clear();
            Active = true;
        }

        protected override void OnDisabled()
        {
            Active = false;
            _pending.Clear();
            _announced.Clear();
        }

        // ---- Called from EnemyAI.Start ------------------------------------

        internal static void OnEnemySpawned(EnemyAI enemy)
        {
            NotificationsModule self = _instance;
            if (self == null || enemy == null)
                return;

            StartOfRound round = StartOfRound.Instance;
            if (round == null || round.inShipPhase)
                return;

            if (Time.time - _levelStartedAt < self._gracePeriod.Value)
                return;

            if (!_announced.Add(enemy.GetInstanceID()))
                return;

            EnemyType type = enemy.enemyType;

            if (type != null && type.isDaytimeEnemy && !self._daytimeEnemies.Value)
                return;

            switch (self._filter.Value)
            {
                case SpawnFilter.InsideOnly:
                    if (enemy.isOutside) return;
                    break;
                case SpawnFilter.OutsideOnly:
                    if (!enemy.isOutside) return;
                    break;
            }

            // enemyType is a ScriptableObject reference that can still be null
            // very early in the spawn, so the component name is the fallback.
            string name = type != null && !string.IsNullOrEmpty(type.enemyName)
                ? type.enemyName
                : enemy.GetType().Name;

            PlayerControllerB local = round.localPlayerController;
            float distance = local != null
                ? Vector3.Distance(local.transform.position, enemy.transform.position)
                : -1f;

            if (self._limitRange.Value && distance >= 0f && distance > self._range.Value)
                return;

            Enqueue(name, distance);
        }

        private static void Enqueue(string name, float distance)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (!string.Equals(_pending[i].Name, name, StringComparison.Ordinal))
                    continue;

                PendingSpawn merged = _pending[i];
                merged.Count++;

                // Keep the closest of the merged group; that's the one worth knowing about.
                if (distance >= 0f && (merged.Distance < 0f || distance < merged.Distance))
                    merged.Distance = distance;

                _pending[i] = merged;
                return;
            }

            _pending.Add(new PendingSpawn { Name = name, Count = 1, Distance = distance });
        }

        // ---- Called every frame from the local player's Update -------------

        internal static void Pump()
        {
            NotificationsModule self = _instance;
            if (self == null)
                return;

            StartOfRound round = StartOfRound.Instance;
            bool inShipPhase = round == null || round.inShipPhase;

            if (inShipPhase != _wasInShipPhase)
            {
                _wasInShipPhase = inShipPhase;
                _pending.Clear();
                _announced.Clear();

                if (!inShipPhase)
                    _levelStartedAt = Time.time;
            }

            if (_pending.Count == 0)
                return;

            if (Time.time - _lastShown < self._cooldown.Value)
                return;

            // Don't stomp a vanilla popup mid-animation; the banner is shared.
            if (Time.time - _lastVanillaNotification < VanillaNotificationLength)
                return;

            Show(Compose(self._showDistance.Value));
            _pending.Clear();
            _lastShown = Time.time;
        }

        private static string Compose(bool withDistance)
        {
            StringBuilder builder = new StringBuilder("Spawned: ");
            int shown = Mathf.Min(_pending.Count, MaxNamesPerPopup);

            for (int i = 0; i < shown; i++)
            {
                PendingSpawn spawn = _pending[i];

                if (i > 0)
                    builder.Append(", ");

                builder.Append(spawn.Name);

                if (spawn.Count > 1)
                    builder.Append(" x").Append(spawn.Count);

                if (withDistance && spawn.Distance >= 0f)
                    builder.Append(" (").Append(Mathf.RoundToInt(spawn.Distance)).Append("m)");
            }

            if (_pending.Count > shown)
                builder.Append(" +").Append(_pending.Count - shown).Append(" more");

            return builder.ToString();
        }

        private static void Show(string text)
        {
            HUDManager hud = HUDManager.Instance;
            if (hud == null)
                return;

            if (!_displayGlobalResolved)
            {
                _displayGlobalResolved = true;

                MethodInfo method = AccessTools.Method(
                    typeof(HUDManager), "DisplayGlobalNotification", new[] { typeof(string) });

                if (method != null)
                    _displayGlobal = AccessTools.MethodDelegate<Action<HUDManager, string>>(method);
            }

            // Guards the vanilla-notification tracker below against counting
            // our own popup as a vanilla one and locking us out for 3 seconds.
            _showingOwnNotification = true;
            try
            {
                if (_displayGlobal != null)
                    _displayGlobal(hud, text);
                else
                    hud.DisplayTip("Entity spawned", text);
            }
            finally
            {
                _showingOwnNotification = false;
            }
        }

        internal static void OnVanillaNotification()
        {
            if (!_showingOwnNotification)
                _lastVanillaNotification = Time.time;
        }
    }

    [HarmonyPatch(typeof(EnemyAI), "Start")]
    internal static class EnemySpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyAI __instance)
        {
            if (!NotificationsModule.Active)
                return;

            NotificationsModule.OnEnemySpawned(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    internal static class NotificationPumpPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerControllerB __instance)
        {
            if (!NotificationsModule.Active)
                return;

            StartOfRound round = StartOfRound.Instance;
            if (round == null || __instance != round.localPlayerController)
                return;

            NotificationsModule.Pump();
        }
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayGlobalNotification")]
    internal static class VanillaNotificationPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!NotificationsModule.Active)
                return;

            NotificationsModule.OnVanillaNotification();
        }
    }
}