
namespace Framework.Log
{
    internal class LogInfo
    {
        public LogLevel m_minLevel = LogLevel.Debug;
        public LogLevel m_defaultLevel = LogLevel.Debug;

        public string m_name;
        public bool m_filePrinting = false;
        public bool m_consolePrinting = false;
        public bool m_screenPrinting = false;
    }
}