using UnityEngine;
using Framework.Log;

namespace Framework.Log
{
    /// <summary>
    /// 日志修复测试脚本
    /// 用于验证路径匹配修复是否有效
    /// </summary>
    public class LogFixTest : MonoBehaviour
    {
        void Start()
        {
            // 等待一帧确保LogController初始化完成
            Invoke(nameof(RunTest), 0.1f);
        }

        private void RunTest()
        {
            Debug.Log("[LogFixTest] 开始测试日志路径匹配修复...");
            
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogFixTest] LogController未初始化");
                return;
            }

            // 输出当前配置状态
            Debug.Log($"[LogFixTest] 控制台配置: {logController.Config?.GetConfigSummary()}");
            Debug.Log($"[LogFixTest] 文件配置: {logController.FileConfig?.GetConfigSummary()}");

            // 测试日志输出
            Debug.Log("[LogFixTest] 这是一条Info级别的测试日志");
            Debug.LogWarning("[LogFixTest] 这是一条Warning级别的测试日志");
            Debug.LogError("[LogFixTest] 这是一条Error级别的测试日志");

            // 检查当前脚本的路径匹配状态
            string currentPath = "Scripts/Framework/Log/LogFixTest.cs";
            bool consoleEnabled = logController.IsPathEnabled(currentPath);
            bool fileEnabled = logController.IsFilePathEnabled(currentPath);
            
            Debug.Log($"[LogFixTest] 当前脚本路径: {currentPath}");
            Debug.Log($"[LogFixTest] 控制台输出启用: {consoleEnabled}");
            Debug.Log($"[LogFixTest] 文件输出启用: {fileEnabled}");

            Debug.Log("[LogFixTest] 测试完成");
        }

        [ContextMenu("手动运行测试")]
        public void ManualTest()
        {
            RunTest();
        }
    }
}