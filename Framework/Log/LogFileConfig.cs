using System;
using System.Collections.Generic;
using System.IO;
using Framework.Res;
using Framework.Util;
using UnityEngine;
#if !UNITY_EDITOR && UNITY_WEBGL
using Path = Framework.WebGLIO.WebGLPath;
#endif

namespace Framework.Log
{
    /// <summary>
    /// 本地日志文件配置管理类
    /// 负责管理目录和文件级别的日志文件输出控制
    /// </summary>
    [Serializable]
    public class LogFileConfig
    {
        [SerializeField] private string version = "1.0";
        [SerializeField] private string logRootPath = ""; // 日志根目录，空则使用默认路径
        [SerializeField] private List<string> enabledPaths = new List<string>();
        [SerializeField] private List<string> disabledPaths = new List<string>();

        // 日志类型文件输出配置
        [SerializeField] private bool writeInfoLogs = true;
        [SerializeField] private bool writeWarningLogs = true;
        [SerializeField] private bool writeErrorLogs = true;

        // 文件管理配置
        [SerializeField] private int maxFileSizeMB = 10; // 单个日志文件最大大小(MB)
        [SerializeField] private int maxFileCount = 30; // 最大保留文件数量
        [SerializeField] private bool enableDailyRotation = true; // 是否启用按日期分文件

        // 缓存匹配结果，提高性能
        private Dictionary<string, bool> _pathCache = new Dictionary<string, bool>();

        public string Version => version;
        public string LogRootPath => logRootPath;
        public List<string> EnabledPaths => enabledPaths;
        public List<string> DisabledPaths => disabledPaths;

        // 日志类型文件输出配置的公共属性
        public bool WriteInfoLogs => writeInfoLogs;
        public bool WriteWarningLogs => writeWarningLogs;
        public bool WriteErrorLogs => writeErrorLogs;

        // 文件管理配置的公共属性
        public int MaxFileSizeMB => maxFileSizeMB;
        public int MaxFileCount => maxFileCount;
        public bool EnableDailyRotation => enableDailyRotation;

        /// <summary>
        /// 获取实际的日志根目录路径
        /// </summary>
        /// <returns>日志根目录的完整路径</returns>
        public string GetActualLogRootPath()
        {
            if (!string.IsNullOrEmpty(logRootPath) && Path.IsPathRooted(logRootPath))
            {
                return logRootPath;
            }

            if (!AssetBundleIndex.AssetBundleModel())
            {
                // 默认路径：项目根目录下的Logs文件夹
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectRoot, "Logs");
            }

            return $"{PathUtil.PersistentDataPath}/Logs";
        }

        /// <summary>
        /// 检查指定文件路径的日志是否应该写入文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否允许写入日志文件</returns>
        public bool IsFileLogEnabled(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return true;

            string normalizedPath = NormalizePath(filePath);

            // 检查缓存
            if (_pathCache.TryGetValue(normalizedPath, out bool cachedResult))
                return cachedResult;

            bool result = CalculateFileLogEnabled(normalizedPath);

            // 缓存结果
            _pathCache[normalizedPath] = result;

            return result;
        }

        /// <summary>
        /// 检查指定日志类型是否应该写入文件
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <returns>是否允许写入该类型的日志</returns>
        public bool IsLogTypeEnabled(LogType logType)
        {
            switch (logType)
            {
                case LogType.Log:
                    return writeInfoLogs;
                case LogType.Warning:
                    return writeWarningLogs;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return writeErrorLogs;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 计算指定路径的日志文件输出是否启用
        /// </summary>
        private bool CalculateFileLogEnabled(string normalizedPath)
        {
            // 首先检查精确匹配的禁用路径
            if (disabledPaths.Contains(normalizedPath))
                return false;

            // 检查精确匹配的启用路径
            if (enabledPaths.Contains(normalizedPath))
                return true;

            // 检查父目录的禁用规则（从最具体到最一般）
            foreach (string disabledPath in disabledPaths)
            {
                if (IsPathUnder(normalizedPath, disabledPath))
                    return false;
            }

            // 检查父目录的启用规则（从最具体到最一般）
            foreach (string enabledPath in enabledPaths)
            {
                if (IsPathUnder(normalizedPath, enabledPath))
                    return true;
            }

            // 如果没有匹配的规则，返回默认值
            return true;
        }

        /// <summary>
        /// 启用指定路径的日志文件输出
        /// </summary>
        /// <param name="path">文件或目录路径</param>
        public void EnablePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string normalizedPath = NormalizePath(path);

            // 从禁用列表中移除
            disabledPaths.Remove(normalizedPath);

            // 添加到启用列表（如果不存在）
            if (!enabledPaths.Contains(normalizedPath))
            {
                enabledPaths.Add(normalizedPath);
            }

            // 清除缓存
            ClearCache();
        }

        /// <summary>
        /// 禁用指定路径的日志文件输出
        /// </summary>
        /// <param name="path">文件或目录路径</param>
        public void DisablePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string normalizedPath = NormalizePath(path);

            // 从启用列表中移除
            enabledPaths.Remove(normalizedPath);

            // 添加到禁用列表（如果不存在）
            if (!disabledPaths.Contains(normalizedPath))
            {
                disabledPaths.Add(normalizedPath);
            }

            // 清除缓存
            ClearCache();
        }

        /// <summary>
        /// 设置日志根目录路径
        /// </summary>
        /// <param name="path">日志根目录路径</param>
        public void SetLogRootPath(string path)
        {
            logRootPath = path ?? "";
        }

        /// <summary>
        /// 设置Scripts目录文件日志启用状态
        /// 直接控制Scripts目录的文件日志启用或禁用，并清空所有其他设置
        /// </summary>
        /// <param name="enabled">Scripts目录文件日志是否启用</param>
        public void SetDefaultEnabled(bool enabled)
        {
            // 清空所有现有设置
            enabledPaths.Clear();
            disabledPaths.Clear();

            if (enabled)
            {
                // 启用Scripts目录文件日志
                enabledPaths.Add("Scripts");
            }
            else
            {
                // 禁用Scripts目录文件日志
                disabledPaths.Add("Scripts");
            }

            ClearCache();
        }

        /// <summary>
        /// 设置日志类型的文件输出配置
        /// </summary>
        public void SetLogTypeFileOutput(bool info, bool warning, bool error)
        {
            writeInfoLogs = info;
            writeWarningLogs = warning;
            writeErrorLogs = error;
        }

        /// <summary>
        /// 设置文件管理配置
        /// </summary>
        public void SetFileManagementConfig(int maxSizeMB, int maxCount, bool dailyRotation)
        {
            maxFileSizeMB = Mathf.Max(1, maxSizeMB);
            maxFileCount = Mathf.Max(1, maxCount);
            enableDailyRotation = dailyRotation;
        }

        /// <summary>
        /// 标准化文件路径
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            // 转换为Unix风格的路径分隔符
            string normalized = path.Replace('\\', '/');

            // 处理绝对路径：提取Assets/Scripts之后的相对路径
            int assetsIndex = normalized.LastIndexOf("/Assets/");
            if (assetsIndex >= 0)
            {
                // 从Assets/开始截取
                normalized = normalized.Substring(assetsIndex + 1);
            }

            // 移除开头的Assets/如果存在
            if (normalized.StartsWith("Assets/"))
            {
                normalized = normalized.Substring(7);
            }

            // 移除末尾的路径分隔符（除非是根目录）
            if (normalized.Length > 1 && normalized.EndsWith("/"))
            {
                normalized = normalized.TrimEnd('/');
            }

            return normalized;
        }

        /// <summary>
        /// 检查文件路径是否在指定目录下
        /// </summary>
        private bool IsPathUnder(string filePath, string directoryPath)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(directoryPath))
                return false;

            string dir = directoryPath.Replace('\\', '/');
            if (!dir.EndsWith("/"))
                dir += "/";

            string file = filePath.Replace('\\', '/');
            return file.StartsWith(dir);
        }

        /// <summary>
        /// 清除路径匹配缓存
        /// </summary>
        public void ClearCache()
        {
            _pathCache.Clear();
        }

        /// <summary>
        /// 检查Scripts目录是否启用（用于Editor显示状态）
        /// </summary>
        /// <returns>Scripts目录是否启用</returns>
        public bool IsScriptsEnabled()
        {
            return enabledPaths.Contains("Scripts");
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        /// <returns>配置摘要字符串</returns>
        public string GetConfigSummary()
        {
            return $"文件日志配置 - 启用路径: {enabledPaths.Count}, 禁用路径: {disabledPaths.Count}, " +
                   $"日志根目录: {(string.IsNullOrEmpty(logRootPath) ? "默认" : logRootPath)}, " +
                   $"按日期分文件: {enableDailyRotation}";
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            logRootPath = "";
            enabledPaths.Clear();
            disabledPaths.Clear();
            writeInfoLogs = true;
            writeWarningLogs = true;
            writeErrorLogs = true;
            maxFileSizeMB = 10;
            maxFileCount = 30;
            enableDailyRotation = true;
            ClearCache();
        }
    }
}