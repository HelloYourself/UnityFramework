using UnityEngine;
using Framework.Log;

namespace Framework.Log
{
    /// <summary>
    /// 日志Bug修复测试脚本
    /// 用于验证线程异常和日志禁用修复是否有效
    /// </summary>
    public class LogBugFixTest : MonoBehaviour
    {
        [ContextMenu("测试日志禁用功能")]
        public void TestLogDisabling()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogBugFixTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogBugFixTest] 开始测试日志禁用功能...");

            // 显示当前配置
            Debug.Log($"[LogBugFixTest] 文件配置摘要: {logController.FileConfig?.GetConfigSummary()}");

            // 测试禁用文件日志
            Debug.Log("[LogBugFixTest] 禁用文件日志默认状态...");
            logController.SetFileLogDefaultEnabled(false);
            logController.SaveFileConfig();

            // 输出测试日志
            for (int i = 0; i < 5; i++)
            {
                Debug.Log($"[LogBugFixTest] 禁用状态测试日志 #{i} - 这些日志不应该写入文件");
            }

            Debug.Log("[LogBugFixTest] 如果文件日志被正确禁用，上述日志不会写入文件");
        }

        [ContextMenu("测试日志启用功能")]
        public void TestLogEnabling()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogBugFixTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogBugFixTest] 开始测试日志启用功能...");

            // 启用文件日志
            Debug.Log("[LogBugFixTest] 启用文件日志默认状态...");
            logController.SetFileLogDefaultEnabled(true);
            logController.SaveFileConfig();

            // 输出测试日志
            for (int i = 0; i < 5; i++)
            {
                Debug.Log($"[LogBugFixTest] 启用状态测试日志 #{i} - 这些日志应该写入文件");
            }

            // 刷新到文件
            logController.FlushFileLog();

            Debug.Log("[LogBugFixTest] 如果文件日志被正确启用，上述日志会写入文件");
        }

        [ContextMenu("测试线程稳定性")]
        public void TestThreadStability()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogBugFixTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogBugFixTest] 开始测试线程稳定性...");

            // 启用文件日志
            logController.SetFileLogDefaultEnabled(true);
            logController.SaveFileConfig();

            // 快速输出大量日志测试线程稳定性
            for (int i = 0; i < 100; i++)
            {
                Debug.Log($"[LogBugFixTest] 线程稳定性测试 #{i}");
                Debug.LogWarning($"[LogBugFixTest] Warning测试 #{i}");
                
                if (i % 10 == 0)
                {
                    Debug.LogError($"[LogBugFixTest] Error测试 #{i}");
                }
            }

            Debug.Log("[LogBugFixTest] 线程稳定性测试完成，检查是否有线程异常");
        }

        [ContextMenu("显示当前配置")]
        public void ShowCurrentConfig()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogBugFixTest] LogController未初始化");
                return;
            }

            Debug.Log("=== 当前日志配置 ===");
            Debug.Log($"控制器初始化: {logController.IsInitialized}");
            Debug.Log($"控制器拦截中: {logController.IsIntercepting}");
            Debug.Log($"控制台配置: {logController.Config?.GetConfigSummary()}");
            Debug.Log($"文件配置: {logController.FileConfig?.GetConfigSummary()}");
            Debug.Log($"当前日志文件: {logController.GetCurrentLogFilePath()}");
            Debug.Log($"日志根目录: {logController.GetLogRootPath()}");
            Debug.Log("=== 配置信息结束 ===");
        }

        [ContextMenu("重置文件日志配置")]
        public void ResetFileLogConfig()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogBugFixTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogBugFixTest] 重置文件日志配置...");
            
            // 重置为默认配置（默认禁用）
            logController.FileConfig?.ResetToDefault();
            logController.SaveFileConfig();
            
            Debug.Log("[LogBugFixTest] 文件日志配置已重置为默认值（默认禁用）");
            Debug.Log($"[LogBugFixTest] 新配置: {logController.FileConfig?.GetConfigSummary()}");
        }
    }
}