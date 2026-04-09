using UnityEngine;
using Framework.Log;

namespace Framework.Log
{
    /// <summary>
    /// 日志系统测试脚本
    /// 用于验证控制台输出和本地文件落地功能是否相互独立
    /// </summary>
    public class LogSystemTest : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private bool enableConsoleTest = true;
        [SerializeField] private bool enableFileTest = true;
        [SerializeField] private float testInterval = 2.0f;
        
        private float lastTestTime = 0f;
        private int testCounter = 0;

        void Start()
        {
            Debug.Log("[LogSystemTest] 日志系统测试开始");
            Debug.Log($"[LogSystemTest] 控制台测试: {enableConsoleTest}, 文件测试: {enableFileTest}");
            
            // 输出当前配置信息
            var logController = LogController.Instance;
            if (logController != null && logController.IsInitialized)
            {
                Debug.Log($"[LogSystemTest] 日志控制器配置: {logController.GetConfigSummary()}");
                Debug.Log($"[LogSystemTest] 当前日志文件: {logController.GetCurrentLogFilePath()}");
                Debug.Log($"[LogSystemTest] 日志根目录: {logController.GetLogRootPath()}");
            }
            else
            {
                Debug.LogWarning("[LogSystemTest] 日志控制器未初始化");
            }
        }

        void Update()
        {
            if (Time.time - lastTestTime >= testInterval)
            {
                lastTestTime = Time.time;
                testCounter++;
                
                if (enableConsoleTest)
                {
                    TestConsoleOutput();
                }
                
                if (enableFileTest)
                {
                    TestFileOutput();
                }
            }
        }

        /// <summary>
        /// 测试控制台输出
        /// </summary>
        private void TestConsoleOutput()
        {
            Debug.Log($"[LogSystemTest] 控制台测试 #{testCounter} - Info日志");
            Debug.LogWarning($"[LogSystemTest] 控制台测试 #{testCounter} - Warning日志");
            Debug.LogError($"[LogSystemTest] 控制台测试 #{testCounter} - Error日志");
        }

        /// <summary>
        /// 测试文件输出
        /// </summary>
        private void TestFileOutput()
        {
            Debug.Log($"[LogSystemTest] 文件测试 #{testCounter} - 这条日志应该写入文件");
            Debug.LogWarning($"[LogSystemTest] 文件测试 #{testCounter} - Warning级别的文件日志");
            Debug.LogError($"[LogSystemTest] 文件测试 #{testCounter} - Error级别的文件日志");
        }

        /// <summary>
        /// 手动触发日志刷新
        /// </summary>
        [ContextMenu("刷新日志到文件")]
        public void FlushLogs()
        {
            var logController = LogController.Instance;
            if (logController != null)
            {
                logController.FlushFileLog();
                Debug.Log("[LogSystemTest] 日志已刷新到文件");
            }
        }

        /// <summary>
        /// 测试大量日志输出
        /// </summary>
        [ContextMenu("测试大量日志")]
        public void TestBulkLogs()
        {
            Debug.Log("[LogSystemTest] 开始大量日志测试...");
            
            for (int i = 0; i < 100; i++)
            {
                Debug.Log($"[LogSystemTest] 批量测试日志 #{i} - Info");
                if (i % 10 == 0)
                {
                    Debug.LogWarning($"[LogSystemTest] 批量测试日志 #{i} - Warning");
                }
                if (i % 20 == 0)
                {
                    Debug.LogError($"[LogSystemTest] 批量测试日志 #{i} - Error");
                }
            }
            
            Debug.Log("[LogSystemTest] 大量日志测试完成");
        }

        /// <summary>
        /// 测试异常日志
        /// </summary>
        [ContextMenu("测试异常日志")]
        public void TestExceptionLogs()
        {
            try
            {
                // 故意触发一个异常
                throw new System.Exception("这是一个测试异常");
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 显示当前配置信息
        /// </summary>
        [ContextMenu("显示配置信息")]
        public void ShowConfigInfo()
        {
            var logController = LogController.Instance;
            if (logController != null && logController.IsInitialized)
            {
                Debug.Log("=== 日志系统配置信息 ===");
                Debug.Log($"控制器状态: 已初始化, 拦截中: {logController.IsIntercepting}");
                Debug.Log($"配置摘要: {logController.GetConfigSummary()}");
                Debug.Log($"当前日志文件: {logController.GetCurrentLogFilePath()}");
                Debug.Log($"日志根目录: {logController.GetLogRootPath()}");
                
                // 测试路径检查
                string testPath = "Scripts/Framework/Log/LogSystemTest.cs";
                bool consoleEnabled = logController.IsPathEnabled(testPath);
                bool fileEnabled = logController.IsFilePathEnabled(testPath);
                Debug.Log($"测试路径 '{testPath}' - 控制台: {consoleEnabled}, 文件: {fileEnabled}");
            }
            else
            {
                Debug.LogWarning("日志控制器未初始化");
            }
        }

        void OnDestroy()
        {
            Debug.Log("[LogSystemTest] 日志系统测试结束");
        }
    }
}