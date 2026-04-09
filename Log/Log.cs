using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Util;
#if !UNITY_EDITOR && UNITY_WEBGL
using File = Framework.WebGLIO.WebGLFile;
#endif

namespace Framework.Log
{
    /// <summary>
    /// 日志管理类
    /// </summary>
    public class Log
    {
        /// <summary>
        /// 默认日志
        /// </summary>
        public static Logger System = new Logger("System");
        
        public static Logger Config = new Logger("Config");
        public static Logger Update = new Logger("Update");
        public static Logger Res = new Logger("Res");
        public static Logger Build = new Logger("Build");

        public static Logger Cache = new Logger("Cache");
        private const string CONFIG_FILE_NAME = "Asset/log.config";
        private static Dictionary<string, LogInfo> m_logInfos = new Dictionary<string, LogInfo>();


        /// <summary>
        /// 初始化日志配置
        /// </summary>
        static Log()
        {
            Load();
        }

        private static void Load()
        {
            m_logInfos.Clear();
            m_logInfos.Add("System", new LogInfo()
            {
                m_name = "System",
                m_consolePrinting = true,
                m_filePrinting = true,
                m_screenPrinting = true
            });
            m_logInfos.Add("Config", new LogInfo()
            {
                m_name = "Config",
                m_consolePrinting = true,
                m_filePrinting = true, 
                m_screenPrinting = true
            });

            string path = $"{PathUtil.PersistentDataPath}/{CONFIG_FILE_NAME}";
            if (!File.Exists(path))
                path = CONFIG_FILE_NAME;

            LoadConfigAsync(path, CancellationToken.None).Forget();
        }

        private static async UniTaskVoid LoadConfigAsync(string path, CancellationToken ct)
        {
            ConfigFile configFile = new ConfigFile();
            if (!await configFile.LightLoadAsync(path, ct))
                return;

            List<ConfigFile.Line> lines = configFile.GetLines();
            for (int i = 0, imax = lines.Count; i < imax; ++i)
            {
                string key = lines[i].m_sectionName;
                string lineKey = lines[i].m_lineKey;
                string lineValue = lines[i].m_value;

                LogInfo logInfo;
                if (!m_logInfos.TryGetValue(key, out logInfo))
                {
                    logInfo = new LogInfo()
                    {
                        m_name = key
                    };
                    m_logInfos.Add(logInfo.m_name, logInfo);
                }

                if (lineKey.Equals("ConsolePrinting", StringComparison.OrdinalIgnoreCase))
                    logInfo.m_consolePrinting = GeneralUtils.ForceBool(lineValue);
                else if (lineKey.Equals("ScreenPrinting", StringComparison.OrdinalIgnoreCase))
                    logInfo.m_screenPrinting = GeneralUtils.ForceBool(lineValue);
                else if (lineKey.Equals("FilePrinting", StringComparison.OrdinalIgnoreCase))
                    logInfo.m_filePrinting = GeneralUtils.ForceBool(lineValue);
                else if (lineKey.Equals("MinLevel", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        logInfo.m_minLevel = EnumUtils.GetEnum<LogLevel>(lineValue, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (ArgumentException ex)
                    {
                        Log.Config.Warning("Log.LoadConfig - Failed to read \"{0}\" at \"{1}\". Exception={2}",
                            lineValue, key, ex.Message);
                    }
                }
                else if (lineKey.Equals("DefaultLevel", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        logInfo.m_defaultLevel =
                            EnumUtils.GetEnum<LogLevel>(lineValue, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (ArgumentException ex)
                    {
                        Log.Config.Warning("Log.LoadConfig - Failed to read \"{0}\" at \"{1}\". Exception={2}",
                            lineValue, key, ex.Message);
                    }
                }
            }

            Config.Info("log.config location: " + path);
        }

        public static string GetException(Exception e)
        {
            var exception = e;
            if (e is System.Reflection.TargetInvocationException)
            {
                if (e.InnerException != null)
                {
                    exception = e.InnerException;
                }
            }

            return string.Format("{0}{1}{2}", exception.Message, Environment.NewLine, exception.StackTrace);
        }

        internal static LogInfo GetLogInfo(string name)
        {
            LogInfo logInfo = null;
            m_logInfos.TryGetValue(name, out logInfo);
            return logInfo;
        }

        //获取第一层调用堆栈
        public static string GetCaller()
        {
            StackTrace st = new StackTrace(1, true);
            StackFrame[] sfArray = st.GetFrames();

            return string.Join(" -> ",
                sfArray.Select(r =>
                    $"{r.GetMethod().Name} in {r.GetFileName()} line:{r.GetFileLineNumber()} column:{r.GetFileColumnNumber()}"));
        }
    }
}
