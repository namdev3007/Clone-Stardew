using Plugins.Scriptable_Object_Updater.Enums;
using System;

namespace Plugins.Scriptable_Object_Updater.Attribute
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UpdateScriptableObjectAttribute : System.Attribute
    {
        public EEventType eventType { get; set; }
        public float Delay { get; set; }
        public float tickDelay { get; set; }
        public int ExecutionOrder { get; set; }
        public bool editorOnly { get; set; }
    }
}
