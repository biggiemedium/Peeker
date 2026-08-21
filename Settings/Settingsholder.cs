using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Peeker.Settings;

namespace Peeker.Module
{
    /// <summary>
    /// Anything that owns a list of settings. Modules extend this, but so can
    /// config screens, HUD elements, or the client core itself.
    /// </summary>
    public abstract class SettingsHolder
    {
        private readonly List<Setting> _settings = new List<Setting>();

        public IReadOnlyList<Setting> Settings => _settings;

        /// <summary>Settings whose visibility predicate currently passes.</summary>
        public IEnumerable<Setting> VisibleSettings => _settings.Where(s => s.IsVisible);

        /// <summary>
        /// Registers a setting and hands it back, so readonly fields can be
        /// assigned in one line from the constructor body:
        /// <c>_range = Register(Setting.Range("Range", 3f, 0f, 6f));</c>
        /// (Field initializers can't be used — they can't call instance methods.)
        /// </summary>
        protected TSetting Register<TSetting>(TSetting setting) where TSetting : Setting
        {
            if (setting == null)
                throw new ArgumentNullException(nameof(setting));

            if (_settings.Any(s => string.Equals(s.Name, setting.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    $"{GetType().Name} already has a setting named '{setting.Name}'.");

            _settings.Add(setting);
            return setting;
        }

        protected void RegisterAll(params Setting[] settings)
        {
            if (settings == null)
                return;

            foreach (Setting setting in settings)
                Register(setting);
        }

        /// <summary>
        /// Optional convenience: reflects over this instance's fields and
        /// properties and registers every <see cref="Setting"/> it finds that
        /// isn't registered yet. Call at the end of your constructor if you'd
        /// rather not call Register on each one.
        /// </summary>
        protected void RegisterDeclaredSettings()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo field in GetType().GetFields(flags))
            {
                if (typeof(Setting).IsAssignableFrom(field.FieldType))
                    TryRegister(field.GetValue(this) as Setting);
            }

            foreach (PropertyInfo property in GetType().GetProperties(flags))
            {
                if (typeof(Setting).IsAssignableFrom(property.PropertyType) && property.CanRead
                    && property.GetIndexParameters().Length == 0)
                {
                    TryRegister(property.GetValue(this) as Setting);
                }
            }
        }

        private void TryRegister(Setting setting)
        {
            if (setting != null && !_settings.Contains(setting))
                Register(setting);
        }

        public Setting GetSetting(string name)
            => _settings.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        public Setting<T> GetSetting<T>(string name) => GetSetting(name) as Setting<T>;

        public bool TryGetSetting(string name, out Setting setting)
        {
            setting = GetSetting(name);
            return setting != null;
        }

        public void ResetSettings()
        {
            foreach (Setting setting in _settings)
                setting.ResetToDefault();
        }
    }
}