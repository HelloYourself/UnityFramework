using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.App;
using Framework.Res;
using Framework.Util;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Framework.Log
{
    /// <summary>
    /// 日志控制器核心类
    /// 负责拦截和过滤Unity的Debug.Log输出，并支持本地文件写入
    /// </summary>
    public class LogController : ILogHandler
    {
        private static LogController _instance;
        private static readonly object _lock = new object();

        private LogConfig _config;
        private LogFileConfig _fileConfig;
        private LogFileWriter _fileWriter;
        private bool _isInitialized = false;
        private bool _isInitializing = false;
        private bool _isIntercepting = false;
        public ILogHandler OriginalLogHandler { get; private set; }

        // Type.FullName -> 归一化脚本资产路径（Scripts/...）映射
        private Dictionary<string, string> _typeToScriptPathIndex = new Dictionary<string, string>();
        private const string ScriptIndexRelPath = "ScriptPathIndex.txt";
        public event Action<string, string, int, string> ExtraErrorHandle;

        // 用于跟踪当前运行模式
        private static bool _lastIsPlaying = false;

        // 用于解析调用栈的正则表达式
        private static readonly Regex StackTraceRegex = new Regex(
            @"at\s+.*\s+in\s+(.+):line\s+\d+",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static LogController Instance
        {
            get
            {
                // 检查是否发生了Editor/Runtime模式切换
                bool currentIsPlaying = Application.isPlaying;
                if (_instance != null && _lastIsPlaying != currentIsPlaying)
                {
                    // 模式切换时重置实例
                    lock (_lock)
                    {
                        if (_instance != null)
                        {
                            _instance.Dispose();
                            _instance = null;
                        }
                    }
                }

                _lastIsPlaying = currentIsPlaying;

                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LogController();
                        }
                    }
                }

                return _instance;
            }
        }

        public LogConfig Config => _config;
        public LogFileConfig FileConfig => _fileConfig;
        public bool IsInitialized => _isInitialized;
        public bool IsIntercepting => _isIntercepting;

        private LogController()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化日志控制器
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized || _isInitializing)
                return;

            _isInitializing = true;
            OriginalLogHandler = Debug.unityLogger.logHandler;
            _config = LogConfigPersistence.CreateDefaultConfig();
            _fileConfig = LogFileConfigPersistence.CreateDefaultConfig();
            InitializeAsync(CancellationToken.None).Forget();
        }

        private async UniTaskVoid InitializeAsync(CancellationToken ct)
        {
            try
            {
                _config = await LogConfigPersistence.LoadConfigAsync(ct);
                _fileConfig = await LogFileConfigPersistence.LoadConfigAsync(ct);

                if (_fileConfig != null)
                    _fileWriter = new LogFileWriter(_fileConfig);

                if (AssetBundleIndex.AssetBundleModel())
                {
                    // 真机默认启用 Scripts 路径的文件日志（仅当没有任何规则且默认禁用时）
                    if (_fileConfig != null)
                    {
                        bool hasRules = (_fileConfig.EnabledPaths != null && _fileConfig.EnabledPaths.Count > 0) ||
                                        (_fileConfig.DisabledPaths != null && _fileConfig.DisabledPaths.Count > 0);
                        if (!hasRules)
                        {
                            _fileConfig.EnablePath("Scripts");
                        }
                    }
                    
                    // 加载脚本路径索引（用于在栈信息缺少源文件时做类型到路径映射）
                    LoadScriptPathIndexAsync().Forget();
                }

                // 开始拦截日志
                StartInterception();

                _isInitialized = true;

                // 使用原始日志处理器输出初始化信息（避免被拦截）
                OriginalLogHandler.LogFormat(LogType.Log, null, "[LogController] 日志控制器已初始化");
                OriginalLogHandler.LogFormat(LogType.Log, null,
                    $"[LogController] 控制台输出配置: {_config.GetConfigSummary()}");
                OriginalLogHandler.LogFormat(LogType.Log, null,
                    $"[LogController] 文件输出配置: {_fileConfig.GetConfigSummary()}");
            }
            catch (Exception ex)
            {
                if (OriginalLogHandler != null)
                {
                    OriginalLogHandler.LogFormat(LogType.Error, null, $"[LogController] 初始化失败: {ex.Message}");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[LogController] 初始化失败: {ex.Message}");
                }
            }
            finally
            {
                _isInitialized = true;
                _isInitializing = false;
            }
        }

        /// <summary>
        /// 开始拦截日志
        /// </summary>
        public void StartInterception()
        {
            if (_isIntercepting)
                return;

            try
            {
                // 替换Unity的日志处理器
                Debug.unityLogger.logHandler = this;
                _isIntercepting = true;

                if (OriginalLogHandler != null)
                {
                    OriginalLogHandler.LogFormat(LogType.Log, null, "[LogController] 日志拦截已启动");
                }
            }
            catch (Exception ex)
            {
                if (OriginalLogHandler != null)
                {
                    OriginalLogHandler.LogFormat(LogType.Error, null, $"[LogController] 启动日志拦截失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 停止拦截日志
        /// </summary>
        public void StopInterception()
        {
            if (!_isIntercepting)
                return;

            try
            {
                // 恢复原始的日志处理器
                if (OriginalLogHandler != null)
                {
                    Debug.unityLogger.logHandler = OriginalLogHandler;
                }

                _isIntercepting = false;

                if (OriginalLogHandler != null)
                {
                    OriginalLogHandler.LogFormat(LogType.Log, null, "[LogController] 日志拦截已停止");
                }
            }
            catch (Exception ex)
            {
                if (OriginalLogHandler != null)
                {
                    OriginalLogHandler.LogFormat(LogType.Error, null, $"[LogController] 停止日志拦截失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ILogHandler.LogFormat 实现
        /// </summary>
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            try
            {
                if (format != null && (format.Contains("[LogController]") || format.Contains("[LogFileWriter]")))
                {
                    OriginalLogHandler?.LogFormat(logType, context, format, args);
                    return;
                }

                // 格式化消息
                string message = string.Format(format, args);

                // 获取调用栈
                string stackTrace = new System.Diagnostics.StackTrace(true).ToString();
                (string sourceFilePath, int fileFrame) = ExtractSourceFileFromStackTrace(stackTrace);

                // 检查控制台输出
                bool shouldLogToConsole = ShouldLogToConsole(logType, sourceFilePath);

                // 检查文件输出
                bool shouldLogToFile = ShouldLogToFile(logType, sourceFilePath);

                // 输出到控制台
                if (shouldLogToConsole)
                {
                    OriginalLogHandler?.LogFormat(logType, context, format, args);
                }

                // 输出到文件
                if (shouldLogToFile)
                {
                    _fileWriter?.WriteLog(logType, message, sourceFilePath);
                }

                //外部对错误日志处理
                if (logType == LogType.Error && ExtraErrorHandle != null)
                {
                    ExtraErrorHandle.Invoke(stackTrace, sourceFilePath, fileFrame, message);
                }
            }
            catch (Exception ex)
            {
                // 避免在日志处理中出现异常导致的无限循环
                OriginalLogHandler?.LogFormat(LogType.Error, null, $"[LogController] 处理日志消息时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// ILogHandler.LogException 实现
        /// </summary>
        public void LogException(Exception exception, UnityEngine.Object context)
        {
            try
            {
                // 异常日志被视为Error级别
                LogLevel logLevel = LogLevel.Error;

                // 获取调用栈
                (string sourceFilePath, int fileFrame) = ExtractSourceFileFromStackTrace(exception.StackTrace);

                // 检查控制台输出
                bool shouldLogToConsole = ShouldLogToConsole(LogType.Error, sourceFilePath);

                // 检查文件输出
                bool shouldLogToFile = ShouldLogToFile(LogType.Error, sourceFilePath);

                // 输出到控制台
                if (shouldLogToConsole)
                {
                    OriginalLogHandler?.LogException(exception, context);
                }

                // 输出到文件
                if (shouldLogToFile)
                {
                    string exceptionMessage =
                        $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}\ncontext:{context?.name}";
                    _fileWriter?.WriteLog(LogType.Error, exceptionMessage, sourceFilePath);
                }

                //外部对错误日志处理
                if (ExtraErrorHandle != null)
                {
                    string exceptionMessage =
                        $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}\ncontext:{context?.name}";
                    ExtraErrorHandle.Invoke(exceptionMessage, sourceFilePath, fileFrame, exceptionMessage);
                }
            }
            catch (Exception ex)
            {
                // 避免在日志处理中出现异常导致的无限循环
                OriginalLogHandler?.LogFormat(LogType.Error, null, $"[LogController] 处理异常日志时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否应该输出到控制台
        /// </summary>
        private bool ShouldLogToConsole(LogType logType, string sourceFilePath)
        {
            // 将LogType转换为LogLevel
            LogLevel logLevel = ConvertLogTypeToLogLevel(logType);

            // 检查该日志类型是否被忽略
            bool isLogTypeIgnored = _config.IsLogTypeIgnored(logLevel);

            // 如果日志类型被忽略，直接输出，不受路径控制影响
            if (isLogTypeIgnored)
            {
                return true;
            }

            // 检查路径控制
            if (string.IsNullOrEmpty(sourceFilePath))
                return true;
            return _config.IsLogEnabled(sourceFilePath);
        }

        /// <summary>
        /// 检查是否应该输出到文件
        /// </summary>
        public bool ShouldLogToFile(LogType logType, string sourceFilePath)
        {
            // 仅调试模式下才输出文件日志
            if (!AppConst.IS_DEBUG)
                return false;

            if (_fileConfig == null)
                return false;

            // 检查该日志类型是否启用文件输出
            if (!_fileConfig.IsLogTypeEnabled(logType))
                return false;
            // 检查路径控制
            if (string.IsNullOrEmpty(sourceFilePath))
                return true;
            return _fileConfig.IsFileLogEnabled(sourceFilePath);
        }

        /// <summary>
        /// 将LogType转换为LogLevel
        /// </summary>
        private LogLevel ConvertLogTypeToLogLevel(LogType logType)
        {
            switch (logType)
            {
                case LogType.Log:
                    return LogLevel.Info;
                case LogType.Warning:
                    return LogLevel.Warning;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return LogLevel.Error;
                default:
                    return LogLevel.Info;
            }
        }

        /// <summary>
        /// 从调用栈中提取源文件路径
        /// </summary>
        private (string, int) ExtractSourceFileFromStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return (null, 0);

            try
            {
                var lines = stackTrace.Split('\n');
                // 遍历调用栈帧
                for (int i = 0; i < lines.Length; i++)
                {
                    var stackInfo = lines[i];
                    if (string.IsNullOrEmpty(stackInfo))
                        continue;
                    var containsInfo =
                        StackTraceFileLineParser.TryParseFileAndLine(stackInfo, out string path, out string fileName,
                            out int line);
                    if (!containsInfo)
                        continue;

                    // 过滤掉Unity引擎和编辑器的文件
                    if (IsUserScript(path))
                        return (path, line);
                }


                // 如果没有找到用户脚本，尝试解析字符串形式的调用栈
                return (ExtractSourceFileAlternative(stackTrace), 0);
            }
            catch (Exception ex)
            {
                return (null, 0);
            }
        }


        /// <summary>
        /// 备用的源文件路径提取方法
        /// </summary>
        private string ExtractSourceFileAlternative(string stackTrace)
        {
            try
            {
                // 如果字符串调用栈中未包含物理文件路径，尝试使用类型名称映射到脚本路径（仅非Editor启用）
                if (AssetBundleIndex.AssetBundleModel())
                {
                    string mapped = MapSourceFileFromStackTrace(stackTrace);
                    if (!string.IsNullOrEmpty(mapped))
                    {
                        return mapped;
                    }
                }
            }
            catch (Exception ex)
            {
                OriginalLogHandler?.LogFormat(LogType.Error, null, $"[LogController] 备用解析方法失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 判断是否为用户脚本（非Unity引擎文件）
        /// </summary>
        private bool IsUserScript(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            // 排除Unity引擎和编辑器文件
            if (ContainsIgnoreCase(filePath, "Scripts\\Framework\\Log") ||
                ContainsIgnoreCase(filePath, "Scripts/Framework/Log") ||
                ContainsIgnoreCase(filePath, "UnityEngine") ||
                ContainsIgnoreCase(filePath, "UnityEditor") ||
                ContainsIgnoreCase(filePath, "Unity.") ||
                ContainsIgnoreCase(filePath, "System.") ||
                ContainsIgnoreCase(filePath, "mscorlib") ||
                ContainsIgnoreCase(filePath, "netstandard"))
            {
                return false;
            }

            // 只处理项目中的脚本文件
            return filePath.Contains("Assets") &&
                   (filePath.EndsWith(".cs") || filePath.Contains("Scripts"));
        }

        private bool ContainsIgnoreCase(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
                return false;
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // 解析调用栈中的类型名，并根据 Type→Path 索引映射到脚本路径
        private string MapSourceFileFromStackTrace(string stackTraceText)
        {
            if (string.IsNullOrEmpty(stackTraceText) || _typeToScriptPathIndex == null ||
                _typeToScriptPathIndex.Count == 0)
                return null;

            try
            {
                string[] lines = stackTraceText.Split('\n');
                foreach (var raw in lines)
                {
                    var typeName = GetRuntimeTypeName(raw.Trim());
                    if (string.IsNullOrEmpty(typeName))
                        continue;
                    if (_typeToScriptPathIndex.TryGetValue(typeName, out var path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                OriginalLogHandler?.LogFormat(LogType.Error, null, $"[LogController] 类型到路径映射失败: {ex.Message}");
            }

            return null;
        }

        //获取真机堆栈类名
        private string GetRuntimeTypeName(string line)
        {
            // 捕获“类型部分”，直到后面跟着  ":" 或 "." + 方法名 + "("
            var m = Regex.Match(line,
                @"\bat\s+(?<type>[^(\r\n]+?)(?=[:\.][^\s:(]+\s*\()|^(?<type>[^(\r\n]+?)(?=[:\.][^\s:(]+\s*\()");

            if (!m.Success) return null;

            string typeFull = m.Groups["type"].Value.Trim();
            typeFull = typeFull.Replace('+', '.');
            // 去掉每个片段上的泛型标记
            typeFull = Regex.Replace(typeFull, @"`[0-9]+(\[[^\]]+\])?", "");
            return typeFull;
        }

        // 从 StreamingAssets 配置目录加载脚本路径索引
        private async UniTaskVoid LoadScriptPathIndexAsync()
        {
            try
            {
                TextAsset txt = await ResUtil.LoadAssetAsync<TextAsset>(ScriptIndexRelPath, AssetCachePolicy.Persistent);
                if (txt == null)
                {
                    OriginalLogHandler?.LogFormat(LogType.Log, null, "[LogController] 未找到脚本路径索引文件，类型映射功能未启用");
                    return;
                }

                var data = JsonUtility.FromJson<ScriptPathIndexModel>(txt.text);
                if (data?.entries == null)
                    return;

                _typeToScriptPathIndex.Clear();
                foreach (var e in data.entries)
                {
                    if (e == null) continue;
                    if (string.IsNullOrEmpty(e.type) || string.IsNullOrEmpty(e.path)) continue;
                    var key = e.type.Replace('+', '.');
                    _typeToScriptPathIndex[key] = e.path;
                }

                OriginalLogHandler?.LogFormat(LogType.Log, null,
                    $"[LogController] 脚本路径索引已加载，条目数: {_typeToScriptPathIndex.Count}");
            }
            catch (Exception ex)
            {
                OriginalLogHandler?.LogFormat(LogType.Error, null, $"[LogController] 加载脚本路径索引失败: {ex.Message}");
            }
        }

        [Serializable]
        private class ScriptPathIndexModel
        {
            public int version;
            public string generatedAt;
            public List<ScriptPathEntryModel> entries;
        }

        [Serializable]
        private class ScriptPathEntryModel
        {
            public string type;
            public string path;
        }

        /// <summary>
        /// 保存当前配置
        /// </summary>
        public void SaveConfig()
        {
            if (_config != null)
            {
                LogConfigPersistence.SaveConfig(_config);
            }
        }

        /// <summary>
        /// 保存文件配置
        /// </summary>
        public void SaveFileConfig()
        {
            if (_fileConfig != null)
            {
                LogFileConfigPersistence.SaveConfig(_fileConfig);
            }
        }

        /// <summary>
        /// 保存所有配置
        /// </summary>
        public void SaveAllConfigs()
        {
            SaveConfig();
            SaveFileConfig();
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfig()
        {
            ReloadConfigAsync(CancellationToken.None).Forget();
        }

        private async UniTaskVoid ReloadConfigAsync(CancellationToken ct)
        {
            try
            {
                _config = await LogConfigPersistence.LoadConfigAsync(ct);
                _config.ClearCache();
                OriginalLogHandler?.LogFormat(LogType.Log, null, "[LogController] 控制台输出配置已重新加载");
            }
            catch (Exception ex)
            {
                OriginalLogHandler?.LogFormat(LogType.Error, null, $"[LogController] 重新加载控制台输出配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重新加载文件配置
        /// </summary>
        public void ReloadFileConfig()
        {
            ReloadFileConfigAsync(CancellationToken.None).Forget();
        }

        private async UniTaskVoid ReloadFileConfigAsync(CancellationToken ct)
        {
            try
            {
                _fileConfig = await LogFileConfigPersistence.LoadConfigAsync(ct);
                _fileConfig.ClearCache();

                if (_fileWriter != null)
                    _fileWriter.UpdateConfig(_fileConfig);

                OriginalLogHandler?.LogFormat(LogType.Log, null, "[LogController] 文件输出配置已重新加载");
            }
            catch (Exception ex)
            {
                OriginalLogHandler?.LogFormat(LogType.Error, null, $"[LogController] 重新加载文件输出配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重新加载所有配置
        /// </summary>
        public void ReloadAllConfigs()
        {
            ReloadConfig();
            ReloadFileConfig();
        }

        /// <summary>
        /// 启用指定路径的控制台日志
        /// </summary>
        public void EnablePath(string path)
        {
            _config?.EnablePath(path);
        }

        /// <summary>
        /// 禁用指定路径的控制台日志
        /// </summary>
        public void DisablePath(string path)
        {
            _config?.DisablePath(path);
        }

        /// <summary>
        /// 启用指定路径的文件日志
        /// </summary>
        public void EnableFilePath(string path)
        {
            _fileConfig?.EnablePath(path);
        }

        /// <summary>
        /// 禁用指定路径的文件日志
        /// </summary>
        public void DisableFilePath(string path)
        {
            _fileConfig?.DisablePath(path);
        }

        /// <summary>
        /// 检查指定路径的控制台日志是否启用
        /// </summary>
        public bool IsPathEnabled(string path)
        {
            return _config?.IsLogEnabled(path) ?? false;
        }

        /// <summary>
        /// 检查指定路径的文件日志是否启用
        /// </summary>
        public bool IsFilePathEnabled(string path)
        {
            return _fileConfig?.IsFileLogEnabled(path) ?? false;
        }

        /// <summary>
        /// 获取当前日志文件路径
        /// </summary>
        public string GetCurrentLogFilePath()
        {
            return _fileWriter?.GetCurrentLogFilePath() ?? "";
        }

        /// <summary>
        /// 获取日志根目录路径
        /// </summary>
        public string GetLogRootPath()
        {
            return _fileWriter?.GetLogRootPath() ?? "";
        }

        /// <summary>
        /// 刷新文件日志
        /// </summary>
        public void FlushFileLog()
        {
            _fileWriter?.Flush();
        }

        /// <summary>
        /// 设置日志根目录路径
        /// </summary>
        public void SetLogRootPath(string path)
        {
            _fileConfig?.SetLogRootPath(path);
            if (_fileWriter != null && _fileConfig != null)
            {
                _fileWriter.UpdateConfig(_fileConfig);
            }
        }

        /// <summary>
        /// 临时关闭当前日志文件（用于文件操作如删除）
        /// </summary>
        public void TemporaryCloseCurrentLogFile()
        {
            _fileWriter?.TemporaryCloseCurrentFile();
        }

        /// <summary>
        /// 重新打开日志文件（在临时关闭后使用）
        /// </summary>
        public void ReopenCurrentLogFile()
        {
            _fileWriter?.ReopenCurrentFile();
        }

        /// <summary>
        /// 设置Info日志忽略状态
        /// </summary>
        public void SetIgnoreInfoLogs(bool ignore)
        {
            _config?.SetIgnoreInfoLogs(ignore);
        }

        /// <summary>
        /// 设置Warning日志忽略状态
        /// </summary>
        public void SetIgnoreWarningLogs(bool ignore)
        {
            _config?.SetIgnoreWarningLogs(ignore);
        }

        /// <summary>
        /// 设置Error日志忽略状态
        /// </summary>
        public void SetIgnoreErrorLogs(bool ignore)
        {
            _config?.SetIgnoreErrorLogs(ignore);
        }

        /// <summary>
        /// 检查指定日志类型是否被忽略
        /// </summary>
        public bool IsLogTypeIgnored(LogLevel logLevel)
        {
            return _config?.IsLogTypeIgnored(logLevel) ?? false;
        }

        /// <summary>
        /// 设置文件日志默认启用状态
        /// </summary>
        public void SetFileLogDefaultEnabled(bool enabled)
        {
            _fileConfig?.SetDefaultEnabled(enabled);
        }

        /// <summary>
        /// 获取配置统计信息
        /// </summary>
        public string GetConfigSummary()
        {
            string consoleSummary = _config?.GetConfigSummary() ?? "控制台配置未初始化";
            string fileSummary = _fileConfig?.GetConfigSummary() ?? "文件配置未初始化";
            return $"控制台: {consoleSummary}\n文件: {fileSummary}";
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                StopInterception();
                SaveAllConfigs();

                // 清理文件写入器
                if (_fileWriter != null)
                {
                    _fileWriter.Dispose();
                    _fileWriter = null;
                }
            }
            catch (Exception ex)
            {
                // 在清理过程中出现异常时，仍然要重置状态
                UnityEngine.Debug.LogError($"[LogController] 清理资源时出错: {ex.Message}");
            }
            finally
            {
                // 确保状态被重置
                _isInitialized = false;
                _isIntercepting = false;
                _config = null;
                _fileConfig = null;
                OriginalLogHandler = null;
            }
        }

        /// <summary>
        /// 强制重新初始化（用于模式切换时）
        /// </summary>
        public void ForceReinitialize()
        {
            // 清理现有资源
            if (_fileWriter != null)
            {
                _fileWriter.Dispose();
                _fileWriter = null;
            }

            _isInitialized = false;
            _isIntercepting = false;
            _config = null;
            _fileConfig = null;
            OriginalLogHandler = null;

            // 重新初始化
            Initialize();
        }
    }
}
