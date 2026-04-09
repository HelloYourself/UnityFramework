using System.ComponentModel;

namespace Framework.Log
{
    public enum LogLevel
    {
        [Description("Debug")] Debug,
        [Description("Info")] Info,
        [Description("Warning")] Warning,
        [Description("Error")] Error,
        [Description("Off")] Off,
    }
}