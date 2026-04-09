using System;
using System.Reflection;
using UnityEngine;

namespace Framework.Log
{
    /// <summary>
    /// 日志路径匹配测试类
    /// 用于调试和验证路径匹配逻辑
    /// </summary>
    public class LogPathTest
    {
        [ContextMenu("Test Path Matching")]
        public void TestPathMatching()
        {
            LogConfig config = new LogConfig();
            
            // 模拟点击"全部启用"按钮
            config.SetDefaultEnabled(true);
            
            // 测试各种路径
            string[] testPaths = new string[]
            {
                "D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\Framework\\Log\\LogController.cs",
                "D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\UI\\UIManager.cs",
                "D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\Game\\Player\\PlayerManager.cs",
                "Assets/Scripts/Framework/Log/LogController.cs",
                "Scripts/Framework/Log/LogController.cs",
                "scripts/framework/log/logcontroller.cs"
            };
            
            Debug.Log("=== 路径匹配测试 ===");
            Debug.Log($"EnabledPaths 包含: {string.Join(", ", config.EnabledPaths)}");
            Debug.Log($"DisabledPaths 包含: {string.Join(", ", config.DisabledPaths)}");
            
            foreach (string path in testPaths)
            {
                bool isEnabled = config.IsLogEnabled(path);
                Debug.Log($"路径: {path}");
                Debug.Log($"  是否启用: {isEnabled}");
                Debug.Log("");
            }
            
            // 测试禁用状态
            Debug.Log("=== 测试禁用状态 ===");
            config.SetDefaultEnabled(false);
            Debug.Log($"禁用后 EnabledPaths: {string.Join(", ", config.EnabledPaths)}");
            Debug.Log($"禁用后 DisabledPaths: {string.Join(", ", config.DisabledPaths)}");
            
            bool isEnabledAfterDisable = config.IsLogEnabled("D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\Framework\\Log\\LogController.cs");
            Debug.Log($"禁用后脚本路径启用状态: {isEnabledAfterDisable}");
        }
        
        [ContextMenu("Test Normalize Path")]
        public void TestNormalizePath()
        {
            LogConfig config = new LogConfig();
            
            // 使用反射调用私有方法
            var normalizeMethod = typeof(LogConfig).GetMethod("NormalizePath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            string[] testPaths = new string[]
            {
                "D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\Framework\\Log\\LogController.cs",
                "D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\UI\\UIManager.cs",
                "Assets/Scripts/Framework/Log/LogController.cs",
                "Scripts/Framework/Log/LogController.cs",
                "scripts/framework/log/logcontroller.cs"
            };
            
            Debug.Log("=== 路径标准化测试 ===");
            foreach (string path in testPaths)
            {
                string normalized = (string)normalizeMethod.Invoke(config, new object[] { path });
                Debug.Log($"原始: {path}");
                Debug.Log($"标准化: {normalized}");
                Debug.Log("");
            }
        }
        
        [ContextMenu("Test IsPathUnder")]
        public void TestIsPathUnder()
        {
            LogConfig config = new LogConfig();
            
            // 使用反射调用私有方法
            var isPathUnderMethod = typeof(LogConfig).GetMethod("IsPathUnder", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // 测试用例
            var testCases = new[]
            {
                ("scripts/framework/log/logcontroller.cs", "scripts"),
                ("scripts/ui/uimanager.cs", "scripts"),
                ("scripts", "scripts"),
                ("assets/scripts/test.cs", "scripts"),
                ("framework/log/test.cs", "scripts")
            };
            
            Debug.Log("=== IsPathUnder 测试 ===");
            foreach (var (targetPath, parentPath) in testCases)
            {
                bool result = (bool)isPathUnderMethod.Invoke(config, new object[] { targetPath, parentPath });
                Debug.Log($"IsPathUnder(\"{targetPath}\", \"{parentPath}\") = {result}");
                
                // 手动验证逻辑
                bool manualResult = targetPath.StartsWith(parentPath) && 
                                   targetPath.Length > parentPath.Length && 
                                   (targetPath[parentPath.Length] == '/' || targetPath[parentPath.Length] == '\\');
                Debug.Log($"  手动验证: {manualResult}");
                Debug.Log("");
            }
        }
        
        [ContextMenu("Test CalculateLogEnabled")]
        public void TestCalculateLogEnabled()
        {
            LogConfig config = new LogConfig();
            config.SetDefaultEnabled(true);
            
            // 使用反射调用私有方法
            var calculateMethod = typeof(LogConfig).GetMethod("CalculateLogEnabled", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var normalizeMethod = typeof(LogConfig).GetMethod("NormalizePath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            string testPath = "D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\Framework\\Log\\LogController.cs";
            string normalizedPath = (string)normalizeMethod.Invoke(config, new object[] { testPath });
            
            Debug.Log("=== CalculateLogEnabled 测试 ===");
            Debug.Log($"原始路径: {testPath}");
            Debug.Log($"标准化路径: {normalizedPath}");
            Debug.Log($"EnabledPaths: {string.Join(", ", config.EnabledPaths)}");
            Debug.Log($"DisabledPaths: {string.Join(", ", config.DisabledPaths)}");
            
            bool result = (bool)calculateMethod.Invoke(config, new object[] { normalizedPath });
            Debug.Log($"CalculateLogEnabled(\"{normalizedPath}\") = {result}");
        }
        
        [ContextMenu("Test Cache Behavior")]
        public void TestCacheBehavior()
        {
            LogConfig config = new LogConfig();
            config.SetDefaultEnabled(true);
            
            string testPath = "D:\\karma4\\client\\Karma_dev\\Assets\\Scripts\\Framework\\Log\\LogController.cs";
            
            Debug.Log("=== 缓存行为测试 ===");
            
            // 第一次调用
            bool result1 = config.IsLogEnabled(testPath);
            Debug.Log($"第一次调用: {result1}");
            
            // 第二次调用（应该使用缓存）
            bool result2 = config.IsLogEnabled(testPath);
            Debug.Log($"第二次调用: {result2}");
            
            // 清除缓存后调用
            config.ClearCache();
            bool result3 = config.IsLogEnabled(testPath);
            Debug.Log($"清除缓存后调用: {result3}");
            
            // 修改配置后调用
            config.SetDefaultEnabled(false);
            bool result4 = config.IsLogEnabled(testPath);
            Debug.Log($"修改配置后调用: {result4}");
        }
    }
}