using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Peeker.Module;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Settings
{
    /// <summary>
    /// Reads and writes <c>BepInEx/config/Peeker.json</c>: every module's toggle
    /// state, keybind and setting values. Written by the menu's SAVE CONFIG button,
    /// read once while <see cref="Peeker"/> is being constructed.
    ///
    /// The file is meant to be hand-editable, so loading is deliberately forgiving:
    /// unknown modules and settings are skipped, malformed values are logged and
    /// ignored, and numbers are clamped into their declared range.
    /// </summary>
    public static class PeekerConfig
    {
        public const int Version = 1;
        public const string FileName = "Peeker.json";

        public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

        // ---------------------------------------------------------------- save

        public static bool Save(ModuleManager modules, ManualLogSource log)
        {
            if (modules == null) return false;

            try
            {
                var moduleMap = new Dictionary<string, object>();

                foreach (Module.Module module in modules)
                {
                    var settingMap = new Dictionary<string, object>();
                    foreach (Setting setting in module.Settings)
                        settingMap[setting.Name] = Encode(setting);

                    moduleMap[module.Name] = new Dictionary<string, object>
                    {
                        { "enabled", module.Toggled },
                        { "keybind", module.Keybind.ToString() },
                        { "settings", settingMap },
                    };
                }

                var root = new Dictionary<string, object>
                {
                    { "version", Version },
                    { "modules", moduleMap },
                };

                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

                // Write beside the target then move, so a crash mid-write can't
                // leave a truncated config behind.
                string temp = path + ".tmp";
                File.WriteAllText(temp, MiniJson.Serialize(root));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);

                log?.LogInfo("[Peeker] Saved config to " + path);
                return true;
            }
            catch (Exception ex)
            {
                log?.LogError("[Peeker] Failed to save config: " + ex);
                return false;
            }
        }

        private static object Encode(Setting setting)
        {
            object value = setting.BoxedValue;
            if (value == null) return null;

            if (setting.IsColorSetting)
                return "#" + ColorUtility.ToHtmlStringRGBA(ToColor(value));

            Type type = Nullable.GetUnderlyingType(setting.ValueType) ?? setting.ValueType;

            if (type.IsEnum) return value.ToString();
            if (type == typeof(bool)) return (bool)value;
            if (type == typeof(string)) return (string)value;
            if (type == typeof(char)) return value.ToString();

            if (setting.IsNumberSetting)
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        // ---------------------------------------------------------------- load

        public static bool Load(ModuleManager modules, ManualLogSource log)
        {
            if (modules == null) return false;

            string path = FilePath;
            if (!File.Exists(path))
            {
                log?.LogInfo("[Peeker] No config at " + path + " — using defaults.");
                return false;
            }

            try
            {
                if (!(MiniJson.Parse(File.ReadAllText(path)) is Dictionary<string, object> root))
                {
                    log?.LogWarning("[Peeker] Config root is not a JSON object — ignoring it.");
                    return false;
                }

                if (root.TryGetValue("version", out object rawVersion) && rawVersion is double fileVersion
                    && (int)fileVersion != Version)
                {
                    log?.LogWarning($"[Peeker] Config is version {(int)fileVersion}, expected {Version}. " +
                                    "Reading what still matches.");
                }

                root.TryGetValue("modules", out object rawModules);
                if (!(rawModules is Dictionary<string, object> moduleMap))
                {
                    log?.LogWarning("[Peeker] Config has no 'modules' object — nothing to apply.");
                    return false;
                }

                foreach (KeyValuePair<string, object> entry in moduleMap)
                {
                    Module.Module module = modules.GetModuleByName(entry.Key);
                    if (module == null)
                    {
                        log?.LogWarning("[Peeker] Config mentions unknown module '" + entry.Key + "' — skipped.");
                        continue;
                    }

                    if (entry.Value is Dictionary<string, object> body)
                        ApplyModule(module, body, log);
                }

                log?.LogInfo("[Peeker] Loaded config from " + path);
                return true;
            }
            catch (Exception ex)
            {
                log?.LogError("[Peeker] Failed to load config (leaving defaults in place): " + ex);
                return false;
            }
        }

        private static void ApplyModule(Module.Module module, Dictionary<string, object> body, ManualLogSource log)
        {
            if (body.TryGetValue("keybind", out object rawKey) && rawKey is string keyName)
            {
                if (Enum.TryParse(keyName, true, out Key key)) module.SetKeybind(key);
                else log?.LogWarning($"[Peeker] {module.Name}: '{keyName}' is not a UnityEngine.InputSystem.Key.");
            }

            if (body.TryGetValue("settings", out object rawSettings) && rawSettings is Dictionary<string, object> settings)
            {
                foreach (Setting setting in module.Settings)
                {
                    if (!settings.TryGetValue(setting.Name, out object value)) continue;

                    try { Decode(setting, value); }
                    catch (Exception ex)
                    {
                        log?.LogWarning($"[Peeker] {module.Name}.{setting.Name}: could not apply saved value ({ex.Message}).");
                    }
                }
            }

            // Last, so OnEnabled sees the restored settings rather than the defaults.
            if (body.TryGetValue("enabled", out object rawEnabled) && rawEnabled is bool enabled)
                module.SetToggled(enabled);
        }

        private static void Decode(Setting setting, object value)
        {
            if (value == null) return;

            Type type = Nullable.GetUnderlyingType(setting.ValueType) ?? setting.ValueType;

            if (setting.IsColorSetting)
            {
                if (value is string hex && ColorUtility.TryParseHtmlString(hex, out Color color))
                    setting.BoxedValue = type == typeof(Color32) ? (object)(Color32)color : color;
                return;
            }

            if (type.IsEnum)
            {
                if (value is string name) setting.BoxedValue = Enum.Parse(type, name, true);
                else if (value is double index) setting.BoxedValue = Enum.ToObject(type, (long)index);
                return;
            }

            if (type == typeof(bool))
            {
                if (value is bool b) setting.BoxedValue = b;
                return;
            }

            if (type == typeof(string))
            {
                setting.BoxedValue = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
                return;
            }

            if (type == typeof(char))
            {
                string s = value as string;
                if (!string.IsNullOrEmpty(s)) setting.BoxedValue = s[0];
                return;
            }

            if (setting.IsNumberSetting && value is double number)
            {
                // A hand-edited file can easily put a slider outside its range.
                if (setting.BoxedMin != null) number = Math.Max(number, Convert.ToDouble(setting.BoxedMin, CultureInfo.InvariantCulture));
                if (setting.BoxedMax != null) number = Math.Min(number, Convert.ToDouble(setting.BoxedMax, CultureInfo.InvariantCulture));
                setting.BoxedValue = Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
            }
        }

        private static Color ToColor(object boxed)
        {
            if (boxed is Color c) return c;
            if (boxed is Color32 c32) return c32;
            return Color.white;
        }
    }
}
