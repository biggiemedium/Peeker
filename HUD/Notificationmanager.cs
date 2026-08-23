using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using Peeker.Module;
using Peeker.Settings;
using UnityEngine;

namespace Peeker.HUD
{
    /// <summary>
    /// One queued line. Returned by <see cref="NotificationManager.Push"/> so the
    /// caller can decorate it after the fact (<see cref="Suffix"/>) without the
    /// manager needing to know anything about the notification's subject.
    /// </summary>
    public sealed class NotificationEntry
    {
        public string Label;
        public string Key;
        public string Text;

        /// <summary>Appended after the count, e.g. " [vent]".</summary>
        public string Suffix = string.Empty;

        public int Count = 1;

        /// <summary>Negative means "no distance"; merging keeps the nearest.</summary>
        public float Distance = -1f;

        public float PushedAt;
    }

    /// <summary>
    /// The game has exactly one notification banner, so every notification
    /// module funnels through here. Owned by <see cref="Peeker"/> and pumped
    /// from its Update, so the queue drains on a real frame tick rather than
    /// depending on which modules happen to be toggled on.
    /// </summary>
    public class NotificationManager
    {
        // Static because NotificationModule registers these into its settings
        // list from its constructor, which runs while ModuleManager is being
        // built. Keeping them static removes any dependency on this manager
        // already existing at that point.
        public static readonly Setting<float> Cooldown =
            Setting.Range("Notification Cooldown", 1.5f, 0.25f, 10f)
                .WithDescription("Minimum seconds between popups. Notifications inside the window are merged.");

        public static readonly Setting<int> MaxPerPopup =
            Setting.Range("Max Per Popup", 3, 1, 8)
                .WithDescription("How many entries fit in one popup before it collapses to '+N more'.");

        // Roughly how long the vanilla banner animation owns the screen.
        private const float VanillaNotificationLength = 3f;

        // Safety valve: if the HUD never appears, the queue must not grow forever.
        private const int MaxQueued = 32;

        private readonly List<NotificationEntry> _pending = new List<NotificationEntry>();
        private readonly List<NotificationModule> _attached = new List<NotificationModule>();
        private readonly ManualLogSource _logger;

        private Harmony _harmony;
        private float _lastShown;
        private float _lastVanilla;
        private float _levelStartedAt;
        private bool _wasInShipPhase = true;
        private bool _showingOwnNotification;

        // HUDManager.DisplayGlobalNotification is private, so it's bound once as
        // an open-instance delegate rather than reflected on every call.
        private Action<HUDManager, string> _displayGlobal;
        private bool _displayGlobalResolved;

        public NotificationManager(ManualLogSource logger)
        {
            _logger = logger;
        }

        /// <summary>Time.time of the most recent landing.</summary>
        public float LevelStartedAt => _levelStartedAt;

        public int AttachedCount => _attached.Count;

        // ---- Module lifecycle ----------------------------------------------

        /// <summary>
        /// Called by <see cref="NotificationModule"/> on enable. The vanilla
        /// notification hook is refcounted so nothing is patched while every
        /// notification module is off.
        /// </summary>
        public void Attach(NotificationModule module)
        {
            if (module == null || _attached.Contains(module))
                return;

            _attached.Add(module);

            if (_attached.Count != 1)
                return;

            _lastShown = 0f;
            _harmony = new Harmony("dev.px.peeker.notifications");
            _harmony.CreateClassProcessor(typeof(VanillaNotificationPatch)).Patch();
        }

        public void Detach(NotificationModule module)
        {
            if (module == null || !_attached.Remove(module))
                return;

            if (_attached.Count > 0)
                return;

            _pending.Clear();
            _harmony?.UnpatchSelf();
            _harmony = null;
        }

        // ---- Pushing --------------------------------------------------------

        /// <summary>Queue a line. Entries sharing a label and key merge into "text xN".</summary>
        public NotificationEntry Push(string label, string key, string text, float distance = -1f)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                NotificationEntry existing = _pending[i];

                if (!string.Equals(existing.Label, label, StringComparison.Ordinal)
                    || !string.Equals(existing.Key, key, StringComparison.Ordinal))
                {
                    continue;
                }

                existing.Count++;

                // Keep the closest of the merged group; that's the useful one.
                if (distance >= 0f && (existing.Distance < 0f || distance < existing.Distance))
                    existing.Distance = distance;

                return existing;
            }

            NotificationEntry entry = new NotificationEntry
            {
                Label = label,
                Key = key,
                Text = text,
                Distance = distance,
                PushedAt = Time.time
            };

            if (_pending.Count >= MaxQueued)
                _pending.RemoveAt(0);

            _pending.Add(entry);
            return entry;
        }

        public void ClearLabel(string label)
        {
            _pending.RemoveAll(e => string.Equals(e.Label, label, StringComparison.Ordinal));
        }

        // ---- Draining -------------------------------------------------------

        /// <summary>Pumped once per frame from <see cref="Peeker.Update"/>.</summary>
        public void Update()
        {
            StartOfRound round = StartOfRound.Instance;
            bool inShipPhase = round == null || round.inShipPhase;

            // Tracked even with nothing attached, so LevelStartedAt is already
            // correct for a module enabled halfway through a round.
            if (inShipPhase != _wasInShipPhase)
            {
                _wasInShipPhase = inShipPhase;
                _pending.Clear();

                if (!inShipPhase)
                    _levelStartedAt = Time.time;

                for (int i = 0; i < _attached.Count; i++)
                    _attached[i].OnLevelChanged();
            }

            if (_pending.Count == 0)
                return;

            if (Time.time - _lastShown < Cooldown.Value)
                return;

            // Don't stomp a vanilla popup mid-animation; the banner is shared.
            if (Time.time - _lastVanilla < VanillaNotificationLength)
                return;

            Flush();
        }

        private void Flush()
        {
            // One label per popup, oldest first, so unrelated notification types
            // never get mashed into the same sentence.
            string label = _pending[0].Label;
            int max = Mathf.Max(1, MaxPerPopup.Value);

            StringBuilder builder = new StringBuilder(label).Append(": ");
            int written = 0;
            int skipped = 0;

            for (int i = 0; i < _pending.Count; i++)
            {
                NotificationEntry entry = _pending[i];

                if (!string.Equals(entry.Label, label, StringComparison.Ordinal))
                    continue;

                if (written >= max)
                {
                    skipped++;
                    continue;
                }

                if (written > 0)
                    builder.Append(", ");

                builder.Append(entry.Text);

                if (entry.Count > 1)
                    builder.Append(" x").Append(entry.Count);

                if (entry.Distance >= 0f)
                    builder.Append(" (").Append(Mathf.RoundToInt(entry.Distance)).Append("m)");

                if (!string.IsNullOrEmpty(entry.Suffix))
                    builder.Append(entry.Suffix);

                written++;
            }

            if (skipped > 0)
                builder.Append(" +").Append(skipped).Append(" more");

            ClearLabel(label);
            Show(builder.ToString());
            _lastShown = Time.time;
        }

        private void Show(string text)
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
                else
                    _logger?.LogWarning("HUDManager.DisplayGlobalNotification not found; falling back to DisplayTip.");
            }

            // Stops the tracker below from counting our own popup as a vanilla
            // one and locking the banner for three seconds.
            _showingOwnNotification = true;
            try
            {
                if (_displayGlobal != null)
                    _displayGlobal(hud, text);
                else
                    hud.DisplayTip("Peeker", text);
            }
            finally
            {
                _showingOwnNotification = false;
            }
        }

        internal void OnVanillaNotification()
        {
            if (!_showingOwnNotification)
                _lastVanilla = Time.time;
        }
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayGlobalNotification")]
    internal static class VanillaNotificationPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            Peeker.Notifications?.OnVanillaNotification();
        }
    }
}