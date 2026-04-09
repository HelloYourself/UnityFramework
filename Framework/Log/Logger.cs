using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Framework.App;
using Framework.Util;
using UnityEngine;
#if !UNITY_EDITOR && UNITY_WEBGL
using Directory = Framework.WebGLIO.WebGLDirectory;
using File = Framework.WebGLIO.WebGLFile;
#endif

namespace Framework.Log
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    public class Logger
    {
        const string OUTPUT_DIRECTORY_NAME = "Logs";
        const string OUTPUT_FILE_EXTENSION = "log";

        string m_name;
        bool m_fileWriterInitialized;
#if !UNITY_EDITOR && UNITY_WEBGL
        string m_filePath;
#else
        StreamWriter m_fileWriter;
#endif

        private static bool _logConsoleFlag = false;

        public static bool logConsoleFlag
        {
            get { return _logConsoleFlag; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">日志名称</param>
        public Logger(string name)
        {
            m_name = name;
        }

        ~Logger()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            m_filePath = null;
#else
            if (m_fileWriter == null) return;
            try
            {
                m_fileWriter.Close();
                m_fileWriter.Dispose();
                m_fileWriter = null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }
#endif
        }

        /// <summary>
        /// 输出调试信息
        /// </summary>
        /// <param name="format">复合格式字符串。</param>
        /// <param name="args">包含零个或多个要格式化的对象的 Object 数组。</param>
        [System.Diagnostics.Conditional(nameof(AppEnv.Dev))]
        public void Debug(string format, params object[] args)
        {
            Print(LogLevel.Debug, format, args);
        }

        /// <summary>
        /// 输出普通信息
        /// </summary>
        /// <param name="format">复合格式字符串。</param>
        /// <param name="args">包含零个或多个要格式化的对象的 Object 数组。</param>
        public void Info(string format, params object[] args)
        {
            Print(LogLevel.Info, format, args);
        }

        /// <summary>
        /// 输出警告信息
        /// </summary>
        /// <param name="format">复合格式字符串。</param>
        /// <param name="args">包含零个或多个要格式化的对象的 Object 数组。</param>
        public void Warning(string format, params object[] args)
        {
            Print(LogLevel.Warning, format, args);
        }

        /// <summary>
        /// 输出错误信息
        /// </summary>
        /// <param name="format">复合格式字符串。</param>
        /// <param name="args">包含零个或多个要格式化的对象的 Object 数组。</param>
        public void Error(string format, params object[] args)
        {
            Print(LogLevel.Error, format, args);
        }

        public void Exception(Exception ex)
        {
            var exception = ex;
            if (ex is System.Reflection.TargetInvocationException)
            {
                if (ex.InnerException != null)
                    exception = ex.InnerException;
            }

            Print(LogLevel.Error, "{0}: {1}{2}{3}", exception.GetType().Name, exception.Message, Environment.NewLine,
                exception.StackTrace);
        }

        void Print(LogLevel level, string format, params object[] args)
        {
            LogInfo logInfo = Log.GetLogInfo(m_name);
            if (logInfo == null || level < logInfo.m_minLevel)
                return;

            string message = GeneralUtils.SafeFormat(format, args);

            if (logInfo.m_filePrinting)
                FilePrint(level, message);
            if (logInfo.m_consolePrinting || level >= LogLevel.Warning)
                ConsolePrint(level, message);
        }

        void FilePrint(LogLevel level, string message)
        {
#if UNITY_STANDALONE_WIN
            //解决windows多开，日志文件冲突
            if (true) return;
#endif
            InitFileWriter();
#if !UNITY_EDITOR && UNITY_WEBGL
            if (string.IsNullOrEmpty(m_filePath))
                return;
#else
            if (m_fileWriter == null)
                return;
#endif

            StringBuilder stringBuilder = new StringBuilder();
            switch (level)
            {
                case LogLevel.Debug:
                    stringBuilder.Append("D ");
                    break;
                case LogLevel.Info:
                    stringBuilder.Append("I ");
                    break;
                case LogLevel.Warning:
                    stringBuilder.Append("W ");
                    break;
                case LogLevel.Error:
                    stringBuilder.Append("E ");
                    break;
            }

            stringBuilder.Append(DateTime.Now.TimeOfDay.ToString());
            stringBuilder.Append(" ");
            stringBuilder.Append(message);
#if !UNITY_EDITOR && UNITY_WEBGL
            stringBuilder.Append('\n');
            File.AppendAllText(m_filePath, stringBuilder.ToString(), Encoding.UTF8);
#else
            m_fileWriter.WriteLine(stringBuilder.ToString());
            m_fileWriter.Flush();
#endif
        }

        void ConsolePrint(LogLevel level, string message)
        {
            _logConsoleFlag = true;
            string str = string.Format("[{0}] {1}", m_name, message);
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(str);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(str);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(str);
                    break;
            }

            _logConsoleFlag = false;
        }


        void InitFileWriter()
        {
            if (m_fileWriterInitialized || !Application.isPlaying)
                return;
            string folder = Application.isMobilePlatform
                ? string.Format("{0}/{1}", PathUtil.PersistentDataPath, OUTPUT_DIRECTORY_NAME)
                : OUTPUT_DIRECTORY_NAME;
            if (!Directory.Exists(folder))
            {
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }

            string logPath = string.Format("{0}/{1}.{2}", folder, m_name, OUTPUT_FILE_EXTENSION);
            try
            {
#if !UNITY_EDITOR && UNITY_WEBGL
                m_filePath = logPath;
                if (!File.Exists(m_filePath))
                {
                    File.WriteAllText(m_filePath, string.Empty, Encoding.UTF8);
                }
#else
                m_fileWriter = new StreamWriter(logPath, true);
#endif
            }
            catch (Exception ex)
            {
#if !UNITY_EDITOR && UNITY_WEBGL
                m_filePath = null;
#endif
                UnityEngine.Debug.LogException(ex);
            }

            m_fileWriterInitialized = true;
        }
    }
}
