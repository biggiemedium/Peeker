using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using Peeker.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module.Impl.Notifications
{
    public enum SpawnFilter
    {
        All,
        InsideOnly,
        OutsideOnly
    }

    public class SpawnNotificationModule : NotificationModule
    {
        internal static SpawnNotificationModule Instance;

        // EnemyAI.Start can fire more than once for the same object in edge
        // cases, so spawns are deduped by instance id for the current level.
        private readonly HashSet<int> _announced = new HashSet<int>();

        private readonly Setting<SpawnFilter> _filter;
        private readonly Setting<bool> _daytimeEntities;
        private readonly Setting<bool> _showDistance;
        private readonly Setting<bool> _limitRange;
        private readonly Setting<float> _range;
        private readonly Setting<float> _gracePeriod;

        public SpawnNotificationModule() : base("Spawns")
        {
            _filter = Register(Setting.Of("Filter", SpawnFilter.All)
                .WithDescription("Which spawns to announce, by where the entity spawned."));

            _daytimeEntities = Register(Setting.Of("Daytime Entities", false)
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

            _gracePeriod = Register(Setting.Range("Grace Period", 5f, 0f, 60f)
                .WithDescription("Seconds after landing or enabling where spawns are ignored, to skip the initial wave."));

            Instance = this;
        }

        protected override string NotificationLabel => "Spawned";

        protected override Type[] PatchTypes => new[] { typeof(EnemySpawnPatch) };

        protected override void OnNotificationsEnabled() => _announced.Clear();

        protected override void OnNotificationsDisabled() => _announced.Clear();

        protected internal override void OnLevelChanged() => _announced.Clear();

        internal void HandleSpawn(EnemyAI enemy)
        {
            if (!Toggled || enemy == null)
                return;

            StartOfRound round = StartOfRound.Instance;
            if (round == null || round.inShipPhase)
                return;

            if (TimeSinceReady < _gracePeriod.Value)
                return;

            if (!_announced.Add(enemy.GetInstanceID()))
                return;

            EnemyType type = enemy.enemyType;

            if (type != null && type.isDaytimeEnemy && !_daytimeEntities.Value)
                return;

            switch (_filter.Value)
            {
                case SpawnFilter.InsideOnly:
                    if (enemy.isOutside) return;
                    break;
                case SpawnFilter.OutsideOnly:
                    if (!enemy.isOutside) return;
                    break;
            }

            PlayerControllerB local = round.localPlayerController;
            float distance = local != null
                ? Vector3.Distance(local.transform.position, enemy.transform.position)
                : -1f;

            if (_limitRange.Value && distance >= 0f && distance > _range.Value)
                return;

            // enemyType is a ScriptableObject reference that can still be null
            // very early in the spawn, so the component name is the fallback.
            string name = type != null && !string.IsNullOrEmpty(type.enemyName)
                ? type.enemyName
                : enemy.GetType().Name;

            Notify(name, name, _showDistance.Value ? distance : -1f);
        }
    }

    [HarmonyPatch(typeof(EnemyAI), "Start")]
    internal static class EnemySpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyAI __instance)
        {
            SpawnNotificationModule module = SpawnNotificationModule.Instance;
            if (module == null)
                return;

            module.HandleSpawn(__instance);
        }
    }
}