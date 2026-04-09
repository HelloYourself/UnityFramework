using System.Diagnostics;
using UnityEngine;
using Framework.Log;

namespace Framework.Log
{
    /// <summary>
    /// 日志路径调试器
    /// 用于调试路径标准化和匹配问题
    /// </summary>
    public class LogPathDebugger : MonoBehaviour
    {
        [ContextMenu("调试路径匹配")]
        public void DebugPathMatching()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                UnityEngine.Debug.LogError("[LogPathDebugger] LogController未初始化");
                return;
            }

            // 获取当前调用栈
            StackTrace stackTrace = new StackTrace(true);
            string sourceFilePath = ExtractSourceFileFromStackTrace(stackTrace);
            
            UnityEngine.Debug.Log("=== 路径匹配调试信息 ===");
            UnityEngine.Debug.Log($"原始源文件路径: {sourceFilePath}");
            
            if (!string.IsNullOrEmpty(sourceFilePath))
            {
                // 测试路径标准化
                string normalizedPath = NormalizePathForTest(sourceFilePath);
                UnityEngine.Debug.Log($"标准化后路径: {normalizedPath}");
                
                // 检查控制台输出配置
                bool consoleEnabled = logController.IsPathEnabled(sourceFilePath);
                UnityEngine.Debug.Log($"控制台输出启用: {consoleEnabled}");
                
                // 检查文件输出配置
                bool fileEnabled = logController.IsFilePathEnabled(sourceFilePath);
                UnityEngine.Debug.Log($"文件输出启用: {fileEnabled}");
                
                // 检查配置列表
                if (logController.Config != null)
                {
                    UnityEngine.Debug.Log($"控制台启用路径数量: {logController.Config.EnabledPaths.Count}");
                    UnityEngine.Debug.Log($"控制台禁用路径数量: {logController.Config.DisabledPaths.Count}");
                    foreach (var path in logController.Config.EnabledPaths)
                    {
                        UnityEngine.Debug.Log($"  控制台启用路径: '{path}'");
                    }
                    foreach (var path in logController.Config.DisabledPaths)
                    {
                        UnityEngine.Debug.Log($"  控制台禁用路径: '{path}'");
                    }
                }
                
                if (logController.FileConfig != null)
                {
                    UnityEngine.Debug.Log($"文件启用路径数量: {logController.FileConfig.EnabledPaths.Count}");
                    UnityEngine.Debug.Log($"文件禁用路径数量: {logController.FileConfig.DisabledPaths.Count}");
                    foreach (var path in logController.FileConfig.EnabledPaths)
                    {
                        UnityEngine.Debug.Log($"  文件启用路径: '{path}'");
                    }
                    foreach (var path in logController.FileConfig.DisabledPaths)
                    {
                        UnityEngine.Debug.Log($"  文件禁用路径: '{path}'");
                    }
                }
                
                // 测试具体的路径匹配
                UnityEngine.Debug.Log("--- 路径匹配测试 ---");
                string[] testPaths = { "scripts", "Scripts", "framework", "Framework", "framework/log", "Framework/Log" };
                foreach (string testPath in testPaths)
                {
                    bool consoleMatch = logController.IsPathEnabled(testPath);
                    bool fileMatch = logController.IsFilePathEnabled(testPath);
                    UnityEngine.Debug.Log($"测试路径 '{testPath}': 控制台={consoleMatch}, 文件={fileMatch}");
                }
            }
            
            UnityEngine.Debug.Log("=== 调试信息结束 ===");
        }

        [ContextMenu("测试日志输出")]
        public void TestLogOutput()
        {
            UnityEngine.Debug.Log("[LogPathDebugger] 这是一条测试Info日志");
            UnityEngine.Debug.LogWarning("[LogPathDebugger] 这是一条测试Warning日志");
            UnityEngine.Debug.LogError("[LogPathDebugger] 这是一条测试Error日志");
        }

        /// <summary>
        /// 从调用栈中提取源文件路径（复制LogController的逻辑）
        /// </summary>
        private string ExtractSourceFileFromStackTrace(StackTrace stackTrace)
        {
            if (stackTrace == null)
                return null;

            try
            {
                // 遍历调用栈帧
                for (int i = 0; i < stackTrace.FrameCount; i++)
                {
                    StackFrame frame = stackTrace.GetFrame(i);
                    if (frame == null)
                        continue;

                    string fileName = frame.GetFileName();
                    if (string.IsNullOrEmpty(fileName))
                        continue;

                    // 过滤掉Unity引擎和编辑器的文件
                    if (IsUserScript(fileName))
                    {
                        return fileName;
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[LogPathDebugger] 解析调用栈失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 判断是否为用户脚本（复制LogController的逻辑）
        /// </summary>
        private bool IsUserScript(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
            
            // 排除Unity引擎和编辑器文件
            if (filePath.Contains("UnityEngine") ||
                filePath.Contains("UnityEditor") ||
                filePath.Contains("Unity.") ||
                filePath.Contains("system.") ||
                filePath.Contains("mscorlib") ||
                filePath.Contains("netstandard"))
            {
                return false;
            }

            // 只处理项目中的脚本文件
            return filePath.Contains("Assets") && 
                   (filePath.EndsWith(".cs") || filePath.Contains("Scripts"));
        }

        /// <summary>
        /// 测试路径标准化（复制LogFileConfig的逻辑）
        /// </summary>
        private string NormalizePathForTest(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            // 转换为Unix风格的路径分隔符
            string normalized = path.Replace('\\', '/');
            
            // 移除开头的Assets/如果存在
            if (normalized.StartsWith("Assets/"))
            {
                normalized = normalized.Substring(7);
            }
            
            return normalized;
        }
    }
}