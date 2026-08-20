using System;

namespace Peeker.Module
{
    /// <summary>
    /// Tag a module class with this to mark it work-in-progress.
    /// ModuleManager sorts these to the front, ahead of the alphabetical list.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DevelopmentAttribute : Attribute
    {
    }
}