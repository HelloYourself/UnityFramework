using UnityEngine;
using Framework.Log;

namespace Framework.Log
{
    /// <summary>
    /// 日志UI优化测试脚本
    /// 用于验证新的Scripts目录控制逻辑是否正确工作
    /// </summary>
    public class LogUIOptimizationTest : MonoBehaviour
    {
        [ContextMenu("测试控制台Scripts目录启用")]
        public void TestConsoleScriptsEnable()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogUIOptimizationTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogUIOptimizationTest] 测试控制台Scripts目录启用...");

            // 启用Scripts目录
            logController.Config.SetDefaultEnabled(true);
            logController.SaveConfig();

            // 检查配置状态
            bool scriptsEnabled = logController.Config.IsScriptsEnabled();
            Debug.Log($"[LogUIOptimizationTest] Scripts目录启用状态: {scriptsEnabled}");
            Debug.Log($"[LogUIOptimizationTest] 启用路径数量: {logController.Config.EnabledPaths.Count}");
            Debug.Log($"[LogUIOptimizationTest] 禁用路径数量: {logController.Config.DisabledPaths.Count}");
            
            foreach (var path in logController.Config.EnabledPaths)
            {
                Debug.Log($"[LogUIOptimizationTest] 启用路径: {path}");
            }

            // 测试日志输出
            Debug.Log("[LogUIOptimizationTest] 这条日志应该能在控制台看到");
        }

        [ContextMenu("测试控制台Scripts目录禁用")]
        public void TestConsoleScriptsDisable()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogUIOptimizationTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogUIOptimizationTest] 测试控制台Scripts目录禁用...");

            // 禁用Scripts目录
            logController.Config.SetDefaultEnabled(false);
            logController.SaveConfig();

            // 检查配置状态
            bool scriptsEnabled = logController.Config.IsScriptsEnabled();
            Debug.Log($"[LogUIOptimizationTest] Scripts目录启用状态: {scriptsEnabled}");
            Debug.Log($"[LogUIOptimizationTest] 启用路径数量: {logController.Config.EnabledPaths.Count}");
            Debug.Log($"[LogUIOptimizationTest] 禁用路径数量: {logController.Config.DisabledPaths.Count}");
            
            foreach (var path in logController.Config.DisabledPaths)
            {
                Debug.Log($"[LogUIOptimizationTest] 禁用路径: {path}");
            }

            // 测试日志输出（这些日志可能不会显示在控制台）
            Debug.Log("[LogUIOptimizationTest] 这条日志可能不会在控制台显示");
        }

        [ContextMenu("测试文件日志Scripts目录启用")]
        public void TestFileLogScriptsEnable()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogUIOptimizationTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogUIOptimizationTest] 测试文件日志Scripts目录启用...");

            // 启用Scripts目录文件日志
            logController.SetFileLogDefaultEnabled(true);
            logController.SaveFileConfig();

            // 检查配置状态
            bool scriptsFileEnabled = logController.FileConfig.IsScriptsEnabled();
            Debug.Log($"[LogUIOptimizationTest] Scripts目录文件日志启用状态: {scriptsFileEnabled}");
            Debug.Log($"[LogUIOptimizationTest] 文件启用路径数量: {logController.FileConfig.EnabledPaths.Count}");
            Debug.Log($"[LogUIOptimizationTest] 文件禁用路径数量: {logController.FileConfig.DisabledPaths.Count}");

            foreach (var path in logController.FileConfig.EnabledPaths)
            {
                Debug.Log($"[LogUIOptimizationTest] 文件启用路径: {path}");
            }

            // 测试文件日志输出
            for (int i = 0; i < 5; i++)
            {
                Debug.Log($"[LogUIOptimizationTest] 文件日志测试 #{i} - 应该写入文件");
            }

            logController.FlushFileLog();
            Debug.Log($"[LogUIOptimizationTest] 当前日志文件: {logController.GetCurrentLogFilePath()}");
        }

        [ContextMenu("测试文件日志Scripts目录禁用")]
        public void TestFileLogScriptsDisable()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogUIOptimizationTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogUIOptimizationTest] 测试文件日志Scripts目录禁用...");

            // 禁用Scripts目录文件日志
            logController.SetFileLogDefaultEnabled(false);
            logController.SaveFileConfig();

            // 检查配置状态
            bool scriptsFileEnabled = logController.FileConfig.IsScriptsEnabled();
            Debug.Log($"[LogUIOptimizationTest] Scripts目录文件日志启用状态: {scriptsFileEnabled}");
            Debug.Log($"[LogUIOptimizationTest] 文件启用路径数量: {logController.FileConfig.EnabledPaths.Count}");
            Debug.Log($"[LogUIOptimizationTest] 文件禁用路径数量: {logController.FileConfig.DisabledPaths.Count}");

            foreach (var path in logController.FileConfig.DisabledPaths)
            {
                Debug.Log($"[LogUIOptimizationTest] 文件禁用路径: {path}");
            }

            // 测试文件日志输出（这些日志不应该写入文件）
            for (int i = 0; i < 5; i++)
            {
                Debug.Log($"[LogUIOptimizationTest] 文件日志禁用测试 #{i} - 不应该写入文件");
            }

            logController.FlushFileLog();
        }

        [ContextMenu("显示当前所有配置")]
        public void ShowAllConfigs()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogUIOptimizationTest] LogController未初始化");
                return;
            }

            Debug.Log("=== 控制台配置 ===");
            Debug.Log($"Scripts目录启用: {logController.Config.IsScriptsEnabled()}");
            Debug.Log($"启用路径: [{string.Join(", ", logController.Config.EnabledPaths)}]");
            Debug.Log($"禁用路径: [{string.Join(", ", logController.Config.DisabledPaths)}]");

            Debug.Log("=== 文件日志配置 ===");
            Debug.Log($"Scripts目录文件日志启用: {logController.FileConfig.IsScriptsEnabled()}");
            Debug.Log($"启用路径: [{string.Join(", ", logController.FileConfig.EnabledPaths)}]");
            Debug.Log($"禁用路径: [{string.Join(", ", logController.FileConfig.DisabledPaths)}]");

            Debug.Log("=== 其他信息 ===");
            Debug.Log($"当前日志文件: {logController.GetCurrentLogFilePath()}");
            Debug.Log($"日志根目录: {logController.GetLogRootPath()}");
            
            Debug.Log("=== 逻辑验证 ===");
            Debug.Log($"控制台日志测试路径启用: {logController.IsPathEnabled("Scripts/Framework/Log/LogUIOptimizationTest.cs")}");
            Debug.Log($"文件日志测试路径启用: {logController.IsFilePathEnabled("Scripts/Framework/Log/LogUIOptimizationTest.cs")}");
        }

        [ContextMenu("重置所有配置")]
        public void ResetAllConfigs()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogUIOptimizationTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogUIOptimizationTest] 重置所有配置...");

            // 重置控制台配置
            logController.Config.Clear();
            logController.SaveConfig();

            // 重置文件日志配置
            logController.FileConfig.ResetToDefault();
            logController.SaveFileConfig();

            Debug.Log("[LogUIOptimizationTest] 所有配置已重置");
        }

        [ContextMenu("快速验证逻辑")]
        public void QuickVerifyLogic()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogUIOptimizationTest] LogController未初始化");
                return;
            }

            Debug.Log("=== 快速验证新逻辑 ===");

            // 测试1：启用Scripts控制台输出
            Debug.Log("1. 启用Scripts控制台输出...");
            logController.Config.SetDefaultEnabled(true);
            Debug.Log($"   Scripts启用: {logController.Config.IsScriptsEnabled()}");
            Debug.Log($"   启用路径: [{string.Join(", ", logController.Config.EnabledPaths)}]");

            // 测试2：禁用Scripts控制台输出
            Debug.Log("2. 禁用Scripts控制台输出...");
            logController.Config.SetDefaultEnabled(false);
            Debug.Log($"   Scripts启用: {logController.Config.IsScriptsEnabled()}");
            Debug.Log($"   禁用路径: [{string.Join(", ", logController.Config.DisabledPaths)}]");

            // 测试3：启用Scripts文件日志
            Debug.Log("3. 启用Scripts文件日志...");
            logController.FileConfig.SetDefaultEnabled(true);
            Debug.Log($"   Scripts文件日志启用: {logController.FileConfig.IsScriptsEnabled()}");
            Debug.Log($"   启用路径: [{string.Join(", ", logController.FileConfig.EnabledPaths)}]");

            Debug.Log("=== 验证完成 ===");
        }
    }
}