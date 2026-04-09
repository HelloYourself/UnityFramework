using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Framework.Log
{
    /// <summary>
    /// 日志配置管理类
    /// 负责管理目录和文件级别的日志输出控制
    /// </summary>
    [Serializable]
    public class LogConfig
    {
        [SerializeField] private string version = "1.0";
        [SerializeField] private List<string> enabledPaths = new List<string>();
        [SerializeField] private List<string> disabledPaths = new List<string>();
        
        // 日志类型忽略配置 - 被忽略的日志类型不受全局禁用影响
        [SerializeField] private bool ignoreInfoLogs = false;
        [SerializeField] private bool ignoreWarningLogs = false;
        [SerializeField] private bool ignoreErrorLogs = true; // 默认Error日志被忽略
        
        // 缓存匹配结果，提高性能
        private Dictionary<string, bool> _pathCache = new Dictionary<string, bool>();
        
        public string Version => version;
        public List<string> EnabledPaths => enabledPaths;
        public List<string> DisabledPaths => disabledPaths;
        
        // 日志类型忽略配置的公共属性
        public bool IgnoreInfoLogs => ignoreInfoLogs;
        public bool IgnoreWarningLogs => ignoreWarningLogs;
        public bool IgnoreErrorLogs => ignoreErrorLogs;

        /// <summary>
        /// 检查指定文件路径的日志是否应该输出
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否允许输出日志</returns>
        public bool IsLogEnabled(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return true;

            // 标准化路径
            string normalizedPath = NormalizePath(filePath);
            
            // 检查缓存
            if (_pathCache.TryGetValue(normalizedPath, out bool cachedResult))
                return cachedResult;

            bool result = CalculateLogEnabled(normalizedPath);
            
            // 缓存结果
            _pathCache[normalizedPath] = result;
            
            return result;
        }

        /// <summary>
        /// 计算指定路径的日志是否启用
        /// </summary>
        private bool CalculateLogEnabled(string normalizedPath)
        {
            // 首先检查精确匹配的禁用路径
            if (disabledPaths.Contains(normalizedPath))
                return false;

            // 检查精确匹配的启用路径
            if (enabledPaths.Contains(normalizedPath))
                return true;

            // 检查父目录的启用规则（从最具体到最一般）
            foreach (string enabledPath in enabledPaths)
            {
                if (IsPathUnder(normalizedPath, enabledPath))
                    return true;
            }
            
            // 检查父目录的禁用规则（从最具体到最一般）
            foreach (string disabledPath in disabledPaths)
            {
                if (IsPathUnder(normalizedPath, disabledPath))
                    return false;
            }

            // 如果没有匹配的规则，返回默认值
            return true;
        }

        /// <summary>
        /// 启用指定路径的日志输出
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
        /// 禁用指定路径的日志输出
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
        /// 移除指定路径的配置（恢复为继承父目录的设置）
        /// </summary>
        /// <param name="path">文件或目录路径</param>
        public void RemovePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string normalizedPath = NormalizePath(path);
            
            enabledPaths.Remove(normalizedPath);
            disabledPaths.Remove(normalizedPath);
            
            // 清除缓存
            ClearCache();
        }

        /// <summary>
        /// 设置Scripts目录启用状态
        /// 直接控制Scripts目录的启用或禁用，并清空所有其他设置
        /// </summary>
        /// <param name="enabled">Scripts目录是否启用</param>
        public void SetDefaultEnabled(bool enabled)
        {
            // 清空所有现有设置
            enabledPaths.Clear();
            disabledPaths.Clear();
            
            if (enabled)
            {
                enabledPaths.Add("Scripts");
            }
            else
            {
                disabledPaths.Add("Scripts");
            }
            
            ClearCache();
        }

        /// <summary>
        /// 清除所有配置
        /// </summary>
        public void Clear()
        {
            enabledPaths.Clear();
            disabledPaths.Clear();
            ClearCache();
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
        /// 清除路径匹配缓存
        /// </summary>
        public void ClearCache()
        {
            _pathCache.Clear();
        }

        /// <summary>
        /// 标准化文件路径，确保路径格式一致性
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // 转换为统一的路径分隔符
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
        /// 检查targetPath是否在parentPath目录下
        /// </summary>
        private bool IsPathUnder(string targetPath, string parentPath)
        {
            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(parentPath))
                return false;

            string p = parentPath.Replace('\\', '/');
            if (!p.EndsWith("/"))
                p += "/";

            string t = targetPath.Replace('\\', '/');
            return t.StartsWith(p);
        }

        /// <summary>
        /// 设置Info日志忽略状态
        /// </summary>
        /// <param name="ignore">是否忽略Info日志</param>
        public void SetIgnoreInfoLogs(bool ignore)
        {
            ignoreInfoLogs = ignore;
            ClearCache();
        }

        /// <summary>
        /// 设置Warning日志忽略状态
        /// </summary>
        /// <param name="ignore">是否忽略Warning日志</param>
        public void SetIgnoreWarningLogs(bool ignore)
        {
            ignoreWarningLogs = ignore;
            ClearCache();
        }

        /// <summary>
        /// 设置Error日志忽略状态
        /// </summary>
        /// <param name="ignore">是否忽略Error日志</param>
        public void SetIgnoreErrorLogs(bool ignore)
        {
            ignoreErrorLogs = ignore;
            ClearCache();
        }

        /// <summary>
        /// 检查指定日志类型是否被忽略
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <returns>是否被忽略</returns>
        public bool IsLogTypeIgnored(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Info:
                    return ignoreInfoLogs;
                case LogLevel.Warning:
                    return ignoreWarningLogs;
                case LogLevel.Error:
                    return ignoreErrorLogs;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取配置统计信息
        /// </summary>
        public string GetConfigSummary()
        {
            string ignoredTypes = "";
            var ignoredList = new List<string>();
            if (ignoreInfoLogs) ignoredList.Add("Info");
            if (ignoreWarningLogs) ignoredList.Add("Warning");
            if (ignoreErrorLogs) ignoredList.Add("Error");
            
            if (ignoredList.Count > 0)
            {
                ignoredTypes = $", 忽略类型: {string.Join(", ", ignoredList)}";
            }
            
            return $"启用路径: {enabledPaths.Count}, " +
                   $"禁用路径: {disabledPaths.Count}" +
                   ignoredTypes;
        }
    }
}
