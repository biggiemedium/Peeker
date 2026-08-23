using Peeker.HUD;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module
{
    /// <summary>
    /// Base for anything that puts a line on the notification banner. Subclasses
    /// implement detection only: call <see cref="Notify(string)"/> and
    /// <see cref="NotificationManager"/> handles queueing, merging, rate limiting
    /// and the HUD call. Category is fixed, so subclasses don't pass one.
    /// </summary>
    public abstract class NotificationModule : Module
    {
        private float _enabledAt;

        protected NotificationModule(string name, Key keybind = Key.None, bool startToggled = false)
            : base(name, keybind, ModuleCategory.Notifications, startToggled)
        {
            // The same two Setting instances go into every notification module,
            // so the banner's shared knobs are editable from any of them.
            RegisterAll(NotificationManager.Cooldown, NotificationManager.MaxPerPopup);
        }

        /// <summary>Prefix for this module's lines, e.g. "Spawned" -> "Spawned: Bracken x2".</summary>
        protected abstract string NotificationLabel { get; }

        /// <summary>
        /// Seconds since this module was ready to report — the later of "the
        /// module was enabled" and "the ship landed". Grace periods measure
        /// against this so enabling mid-round doesn't dump a backlog.
        /// </summary>
        protected float TimeSinceReady
        {
            get
            {
                NotificationManager manager = Peeker.Notifications;
                float start = manager != null ? Mathf.Max(_enabledAt, manager.LevelStartedAt) : _enabledAt;
                return Time.time - start;
            }
        }

        // Sealed so a subclass can't forget to attach to / detach from the
        // manager. Update is deliberately left open — the manager pumps itself
        // from Peeker.Update, so subclasses are free to override it normally.
        protected sealed override void OnEnabled()
        {
            _enabledAt = Time.time;
            Peeker.Notifications?.Attach(this);
            OnNotificationsEnabled();
        }

        protected sealed override void OnDisabled()
        {
            OnNotificationsDisabled();

            NotificationManager manager = Peeker.Notifications;
            if (manager == null)
                return;

            manager.ClearLabel(NotificationLabel);
            manager.Detach(this);
        }

        protected virtual void OnNotificationsEnabled() { }

        protected virtual void OnNotificationsDisabled() { }

        /// <summary>Called on landing and on returning to orbit. Drop per-level state here.</summary>
        protected internal virtual void OnLevelChanged() { }

        // ---- Pushing --------------------------------------------------------

        protected NotificationEntry Notify(string text) => Notify(text, text);

        /// <param name="key">Merge key. Pushes sharing a key collapse to "text xN".</param>
        /// <param name="distance">Metres from the local player, or negative to omit.</param>
        protected NotificationEntry Notify(string key, string text, float distance = -1f)
        {
            NotificationManager manager = Peeker.Notifications;
            return manager?.Push(NotificationLabel, key, text, distance);
        }
    }
}