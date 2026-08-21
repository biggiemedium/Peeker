using System;

namespace Peeker.HUD
{
    /// <summary>
    /// C# equivalent of the Java @ElementInterface annotation.
    /// Carries the static metadata for an element: display name, default position,
    /// default size and whether it may be dragged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HudElementAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; set; } = "No description.";

        /// <summary>Default X in GUI space (pixels, origin top-left).</summary>
        public float X { get; set; } = 4f;

        /// <summary>Default Y in GUI space (pixels, origin top-left).</summary>
        public float Y { get; set; } = 4f;

        /// <summary>Initial size. Overridden every frame by <see cref="HudElement.Measure"/>.</summary>
        public float Width { get; set; } = 100f;
        public float Height { get; set; } = 20f;

        /// <summary>Locked elements are drawn but cannot be dragged in edit mode.</summary>
        public bool Locked { get; set; }

        public bool EnabledByDefault { get; set; } = true;

        public HudElementAttribute(string name)
        {
            Name = name;
        }
    }
}