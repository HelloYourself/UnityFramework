using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
#if !UNITY_EDITOR && UNITY_WEBGL
using File = Framework.WebGLIO.WebGLFile;
using Directory = Framework.WebGLIO.WebGLDirectory;
using Path = Framework.WebGLIO.WebGLPath;
using FileStream = Framework.WebGLIO.WebGLFileStream;
#endif

namespace Framework.Log
{
    /// <summary>
    /// 文件日志写入器。
    /// WebGL/小游戏平台禁用后台线程，改为调用线程同步写入。
    /// </summary>
    public class LogFileWriter : IDisposable
    {
        private LogFileConfig _config;
        private string _currentLogFilePath;
        private DateTime _currentLogDate;
        private FileStream _fileStream;
        private StreamWriter _streamWriter;
        private readonly object _writeLock = new object();
        private readonly object _queueLock = new object();
        private bool _disposed;

        private readonly Queue<LogEntry> _logQueue = new Queue<LogEntry>();

#if !UNITY_EDITOR && UNITY_WEBGL
        // WebGL/小游戏环境不支持稳定多线程写盘，不创建后台写线程。
#else
        private Thread _writeThread;
        private bool _isWriting;
#endif

        private readonly string productName;
        private readonly string version;
        private readonly string unityVersion;
        private readonly RuntimePlatform platform;

        private struct LogEntry
        {
            public DateTime Timestamp;
            public LogType LogType;
            public string Message;
            public string SourceFilePath;
        }

        public LogFileWriter(LogFileConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            productName = Application.productName;
            version = Application.version;
            unityVersion = Application.unityVersion;
            platform = Application.platform;
            Initialize();
        }

        /// <summary>
        /// 初始化日志目录与当前日志文件。
        /// </summary>
        private void Initialize()
        {
            try
            {
                string logRootPath = _config.GetActualLogRootPath();
                if (!Directory.Exists(logRootPath))
                {
                    Directory.CreateDirectory(logRootPath);
                }

                _currentLogDate = DateTime.Now.Date;
                UpdateCurrentLogFile();

#if !UNITY_EDITOR && UNITY_WEBGL
                // WebGL/小游戏：同步写入，不起线程。
#else
                StartWriteThread();
#endif

                Debug.Log($"[LogFileWriter] 日志写入器初始化完成，目录: {logRootPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogFileWriter] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入一条日志。
        /// </summary>
        public void WriteLog(LogType logType, string message, string sourceFilePath)
        {
            if (_disposed || _config == null)
                return;

            try
            {
                if (!_config.IsLogTypeEnabled(logType))
                    return;

                if (!string.IsNullOrEmpty(sourceFilePath) && !_config.IsFileLogEnabled(sourceFilePath))
                    return;

                LogEntry logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    LogType = logType,
                    Message = message ?? string.Empty,
                    SourceFilePath = sourceFilePath ?? string.Empty,
                };

#if !UNITY_EDITOR && UNITY_WEBGL
                // WebGL/小游戏平台直接同步写盘，规避线程限制。
                WriteLogEntries(new List<LogEntry>(1) { logEntry });
#else
                lock (_queueLock)
                {
                    _logQueue.Enqueue(logEntry);
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogFileWriter] 写入日志失败: {ex.Message}");
            }
        }

#if !UNITY_EDITOR && UNITY_WEBGL
        // WebGL 平台无后台写线程。
#else
        /// <summary>
        /// 启动后台写线程。
        /// </summary>
        private void StartWriteThread()
        {
            if (_writeThread != null && _writeThread.IsAlive)
                return;

            _isWriting = true;
            _writeThread = new Thread(WriteThreadLoop)
            {
                Name = "LogFileWriter",
                IsBackground = true,
            };
            _writeThread.Start();
        }

        /// <summary>
        /// 停止后台写线程。
        /// </summary>
        private void StopWriteThread()
        {
            _isWriting = false;

            if (_writeThread != null && _writeThread.IsAlive)
            {
                _writeThread.Join(1000);
            }
        }

        /// <summary>
        /// 后台写线程循环。
        /// </summary>
        private void WriteThreadLoop()
        {
            while (_isWriting && !_disposed)
            {
                try
                {
                    List<LogEntry> entriesToWrite = new List<LogEntry>();
                    lock (_queueLock)
                    {
                        while (_logQueue.Count > 0)
                        {
                            entriesToWrite.Add(_logQueue.Dequeue());
                        }
                    }

                    if (entriesToWrite.Count > 0)
                    {
                        WriteLogEntries(entriesToWrite);
                    }

                    Thread.Sleep(100);
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LogFileWriter] 写线程异常: {ex.Message}\n{ex.StackTrace}");
                    Thread.Sleep(1000);

                    if (_disposed)
                    {
                        break;
                    }
                }
            }

            Debug.Log("[LogFileWriter] 写线程已退出");
        }
#endif

        /// <summary>
        /// 批量写入日志条目。
        /// </summary>
        private void WriteLogEntries(List<LogEntry> entries)
        {
            if (_disposed || entries == null || entries.Count == 0)
                return;

            lock (_writeLock)
            {
                try
                {
                    if (_streamWriter == null || _fileStream == null)
                    {
                        UpdateCurrentLogFile();
                        if (_streamWriter == null)
                        {
                            Debug.LogWarning("[LogFileWriter] 无法初始化日志文件，跳过本次写入");
                            return;
                        }
                    }

                    foreach (var entry in entries)
                    {
                        if (_disposed)
                            break;

                        try
                        {
                            CheckAndRotateLogFile(entry.Timestamp);
                            _streamWriter?.WriteLine(FormatLogMessage(entry));
                        }
                        catch (Exception entryEx)
                        {
                            Debug.LogError($"[LogFileWriter] 单条日志写入失败: {entryEx.Message}");
                        }
                    }

                    _streamWriter?.Flush();
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("[LogFileWriter] 文件流已释放，停止写入");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LogFileWriter] 批量写入失败: {ex.Message}");
                    try
                    {
                        CloseCurrentLogFile();
                        UpdateCurrentLogFile();
                    }
                    catch (Exception reinitEx)
                    {
                        Debug.LogError($"[LogFileWriter] 重建文件流失败: {reinitEx.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 检查是否需要滚动日志文件。
        /// </summary>
        private void CheckAndRotateLogFile(DateTime timestamp)
        {
            bool needRotate = false;

            if (_config.EnableDailyRotation && timestamp.Date != _currentLogDate)
            {
                needRotate = true;
                _currentLogDate = timestamp.Date;
            }

            if (!needRotate && _fileStream != null)
            {
                long fileSizeBytes = _fileStream.Length;
                long maxSizeBytes = _config.MaxFileSizeMB * 1024 * 1024;
                if (fileSizeBytes >= maxSizeBytes)
                {
                    needRotate = true;
                }
            }

            if (needRotate)
            {
                CloseCurrentLogFile();
                UpdateCurrentLogFile();
                CleanupOldLogFiles();
            }
        }

        /// <summary>
        /// 读取日志文件大小（兼容 WebGLIO 路径语义）。
        /// </summary>
        private long GetFileLength(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return 0L;

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return stream.Length;
            }
        }

        /// <summary>
        /// 更新当前日志文件句柄。
        /// </summary>
        private void UpdateCurrentLogFile()
        {
            try
            {
                string logRootPath = _config.GetActualLogRootPath();
                string fileName = _config.EnableDailyRotation
                    ? $"game_{_currentLogDate:yyyyMMdd}.log"
                    : $"game_{DateTime.Now:yyyyMMdd_HHmmss}.log";

                _currentLogFilePath = Path.Combine(logRootPath, fileName);

                if (File.Exists(_currentLogFilePath))
                {
                    long maxSizeBytes = _config.MaxFileSizeMB * 1024 * 1024;
                    if (GetFileLength(_currentLogFilePath) >= maxSizeBytes)
                    {
                        int index = 1;
                        string baseFileName = Path.GetFileNameWithoutExtension(fileName);
                        string extension = Path.GetExtension(fileName);

                        do
                        {
                            fileName = $"{baseFileName}_{index:D3}{extension}";
                            _currentLogFilePath = Path.Combine(logRootPath, fileName);
                            index++;
                        } while (File.Exists(_currentLogFilePath) &&
                                 GetFileLength(_currentLogFilePath) >= maxSizeBytes);
                    }
                }

                _fileStream = new FileStream(_currentLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                _streamWriter = new StreamWriter(_fileStream, Encoding.UTF8);

                if (_fileStream.Length == 0)
                {
                    _streamWriter.WriteLine($"# Unity Log File - Created at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    _streamWriter.WriteLine($"# Application: {productName} v{version}");
                    _streamWriter.WriteLine($"# Unity Version: {unityVersion}");
                    _streamWriter.WriteLine($"# Platform: {platform}");
                    _streamWriter.WriteLine("# =====================================");
                    _streamWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                if (LogController.Instance?.OriginalLogHandler != null)
                {
                    LogController.Instance.OriginalLogHandler.LogFormat(
                        LogType.Error,
                        null,
                        $"[LogFileWriter] 更新日志文件失败: {ex.Message}");
                }
                else
                {
                    Debug.LogError($"[LogFileWriter] 更新日志文件失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 关闭当前日志文件。
        /// </summary>
        private void CloseCurrentLogFile()
        {
            try
            {
                _streamWriter?.Dispose();
                _fileStream?.Dispose();
                _streamWriter = null;
                _fileStream = null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogFileWriter] 关闭日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理超出数量上限的旧日志文件。
        /// </summary>
        private void CleanupOldLogFiles()
        {
            try
            {
                string logRootPath = _config.GetActualLogRootPath();
                if (!Directory.Exists(logRootPath))
                    return;

                string[] logFiles = Directory.GetFiles(logRootPath, "*.log");
                if (logFiles.Length <= _config.MaxFileCount)
                    return;

                Array.Sort(logFiles, (x, y) => File.GetLastWriteTime(x).CompareTo(File.GetLastWriteTime(y)));

                int filesToDelete = logFiles.Length - _config.MaxFileCount;
                for (int i = 0; i < filesToDelete; i++)
                {
                    try
                    {
                        File.Delete(logFiles[i]);
                        Debug.Log($"[LogFileWriter] 已删除旧日志文件: {Path.GetFileName(logFiles[i])}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LogFileWriter] 删除旧日志失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogFileWriter] 清理旧日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化日志行。
        /// </summary>
        private string FormatLogMessage(LogEntry entry)
        {
            string logTypeStr = entry.LogType.ToString().ToUpper();
            string timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string sourceFile = string.IsNullOrEmpty(entry.SourceFilePath)
                ? "Unknown"
                : Path.GetFileName(entry.SourceFilePath);

            return $"[{timestamp}] [{logTypeStr}] [{sourceFile}] {entry.Message}";
        }

        /// <summary>
        /// 更新配置，必要时切换日志目录。
        /// </summary>
        public void UpdateConfig(LogFileConfig newConfig)
        {
            if (newConfig == null)
                return;

            lock (_writeLock)
            {
                _config = newConfig;

                string newLogRootPath = _config.GetActualLogRootPath();
                string currentLogRootPath = Path.GetDirectoryName(_currentLogFilePath);

                if (!string.Equals(newLogRootPath, currentLogRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    CloseCurrentLogFile();

                    if (!Directory.Exists(newLogRootPath))
                    {
                        Directory.CreateDirectory(newLogRootPath);
                    }

                    UpdateCurrentLogFile();
                }
            }
        }

        public string GetCurrentLogFilePath()
        {
            return _currentLogFilePath;
        }

        public string GetLogRootPath()
        {
            return _config?.GetActualLogRootPath() ?? string.Empty;
        }

        /// <summary>
        /// 强制刷新文件缓冲。
        /// </summary>
        public void Flush()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            // WebGL 下为同步写入模式，不需要等待后台线程队列。
#else
            int maxWaitTime = 5000;
            int waitTime = 0;
            while (waitTime < maxWaitTime)
            {
                lock (_queueLock)
                {
                    if (_logQueue.Count == 0)
                        break;
                }

                Thread.Sleep(50);
                waitTime += 50;
            }
#endif
            lock (_writeLock)
            {
                _streamWriter?.Flush();
                _fileStream?.Flush();
            }
        }

        /// <summary>
        /// 临时关闭当前日志文件。
        /// </summary>
        public void TemporaryCloseCurrentFile()
        {
            lock (_writeLock)
            {
                Flush();
                CloseCurrentLogFile();
            }
        }

        /// <summary>
        /// 重新打开当前日志文件。
        /// </summary>
        public void ReopenCurrentFile()
        {
            lock (_writeLock)
            {
                UpdateCurrentLogFile();
            }
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

#if !UNITY_EDITOR && UNITY_WEBGL
            // WebGL 下没有写线程。
#else
            StopWriteThread();
#endif

            Flush();
            CloseCurrentLogFile();
        }
    }
}
