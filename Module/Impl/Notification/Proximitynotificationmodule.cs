using System.Collections.Generic;
using GameNetcodeStuff;
using Peeker.Settings;
using Peeker.Tracking;
using Peeker.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module.Impl.Notifications
{
    /// <summary>
    /// Announces entities that get close to you, off the back of whatever
    /// <see cref="EntityManager"/> already has tracked — no scene sweep of its
    /// own, and no Harmony patch either: <see cref="NotificationModule"/> owns
    /// the banner hook and only installs it while a notification module is on.
    ///
    /// This is the counterpart to <see cref="SpawnNotificationModule"/>, which
    /// fires once when something is created. This one fires when something
    /// arrives, so it also catches the enemy that spawned across the map an hour
    /// ago and has just walked into the room with you.
    /// </summary>
    public class ProximityNotificationModule : NotificationModule
    {
        // Time.time of the last announcement, keyed by transform instance id.
        // Not by TrackedEntity: the manager throws its entity objects away and
        // rebuilds them on every rescan, so the identity has to come from the
        // underlying object.
        private readonly Dictionary<int, float> _lastAlert = new Dictionary<int, float>();

        // Reused across scans so the prune below doesn't allocate every tick.
        private readonly List<int> _expired = new List<int>();

        private readonly Setting<float> _range;
        private readonly Setting<bool> _includePlayers;
        private readonly Setting<bool> _includePassive;
        private readonly Setting<bool> _lineOfSightOnly;
        private readonly Setting<bool> _showDistance;
        private readonly Setting<float> _reAlertAfter;
        private readonly Setting<float> _gracePeriod;
        private readonly Setting<float> _scanInterval;

        private float _nextScan;

        public ProximityNotificationModule() : base("Proximity", Key.P)
        {
            _range = Register(Setting.Range("Range", 30f, 5f, 200f)
                .WithDescription("How close something has to get before it's announced, in metres."));

            _includePlayers = Register(Setting.Of("Include Players", false)
                .WithDescription("Also announce teammates who come near you."));

            _includePassive = Register(Setting.Of("Include Passive", false)
                .WithDescription("Also announce harmless daytime entities such as manticoils."));

            _lineOfSightOnly = Register(Setting.Of("Line Of Sight Only", false)
                .WithDescription("Only announce entities you could actually see, ignoring anything behind a wall."));

            _showDistance = Register(Setting.Of("Show Distance", true)
                .WithDescription("Append how far away it was at the moment of the alert."));

            // Doubles as the memory: an entity is forgotten once this has
            // elapsed, so it can announce itself again.
            _reAlertAfter = Register(Setting.Range("Re-alert After", 30f, 5f, 300f)
                .WithDescription("Seconds before the same entity is allowed to announce itself again."));

            _gracePeriod = Register(Setting.Range("Grace Period", 3f, 0f, 60f)
                .WithDescription("Seconds after landing or enabling where alerts are ignored, to skip the backlog of things already near you."));

            _scanInterval = Register(Setting.Range("Scan Interval", 0.35f, 0.1f, 2f)
                .WithDescription("Seconds between range checks. Higher is cheaper but slower to react."));
        }

        protected override string NotificationLabel => "Nearby";

        // No PatchTypes: everything here reads state that the entity manager has
        // already collected, so there's nothing to hook.

        protected override void OnNotificationsEnabled()
        {
            _lastAlert.Clear();
            _nextScan = 0f;
        }

        protected override void OnNotificationsDisabled() => _lastAlert.Clear();

        protected internal override void OnLevelChanged()
        {
            _lastAlert.Clear();
            _nextScan = 0f;
        }

        public override void Update()
        {
            if (Time.time < _nextScan)
                return;

            _nextScan = Time.time + _scanInterval.Value;
            Scan();
        }

        private void Scan()
        {
            StartOfRound round = StartOfRound.Instance;
            if (round == null || round.inShipPhase)
                return;

            if (TimeSinceReady < _gracePeriod.Value)
                return;

            EntityManager entities = global::Peeker.Peeker.Entities;
            if (entities == null || !entities.InLevel)
                return;

            PlayerControllerB local = entities.LocalPlayer;
            if (local == null || local.isPlayerDead)
                return;

            float now = Time.time;
            float range = _range.Value;
            float memory = _reAlertAfter.Value;

            Prune(now, memory);

            foreach (TrackedEntity entity in entities.All)
            {
                if (!Eligible(entity))
                    continue;

                float distance = entities.DistanceTo(entity);
                if (distance > range)
                    continue;

                int id = entity.Transform.GetInstanceID();

                // Prune already dropped anything old enough to speak again, so a
                // hit here always means "announced recently".
                if (_lastAlert.ContainsKey(id))
                    continue;

                // Deliberately last: the linecast is the expensive part, and by
                // here it only runs for something that was actually going to be
                // announced.
                if (_lineOfSightOnly.Value && !Sight.Clear(local, ChestOf(entity)))
                    continue;

                _lastAlert[id] = now;

                // Keyed on the name so three Brackens collapse into "Bracken x3"
                // rather than filling the banner with a line each.
                Notify(entity.Name, entity.Name, _showDistance.Value ? distance : -1f);
            }
        }

        private bool Eligible(TrackedEntity entity)
        {
            if (entity == null || !entity.Alive)
                return false;

            switch (entity.Kind)
            {
                case EntityKind.Hostile:
                    return true;
                case EntityKind.Passive:
                    return _includePassive.Value;
                case EntityKind.Player:
                    return _includePlayers.Value;
                default:
                    return false;   // scrap doesn't sneak up on anyone
            }
        }

        /// <summary>
        /// Drops entries old enough that the entity is allowed to announce
        /// itself again. That's the same test the scan would make, so doing it
        /// here keeps the dictionary from growing for the whole round.
        /// </summary>
        private void Prune(float now, float memory)
        {
            _expired.Clear();

            foreach (KeyValuePair<int, float> pair in _lastAlert)
                if (now - pair.Value >= memory)
                    _expired.Add(pair.Key);

            for (int i = 0; i < _expired.Count; i++)
                _lastAlert.Remove(_expired[i]);
        }

        // Aiming the sight test at the feet would fail against every lip and
        // step in the interior, so it goes at the middle of the entity.
        private static Vector3 ChestOf(TrackedEntity entity)
        {
            return entity.Position + Vector3.up * (entity.Height * 0.5f);
        }
    }
}
