using UnityEngine;
using Framework.Log;

namespace Framework.Log
{
    /// <summary>
    /// 日志类型忽略功能测试脚本
    /// 用于验证被忽略的日志类型不受全局禁用影响
    /// </summary>
    public class LogTypeIgnoreTest : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private bool runTestOnStart = false;
        [SerializeField] private float testInterval = 2f;
        
        private float _lastTestTime;

        private void Start()
        {
            if (runTestOnStart)
            {
                RunLogTypeIgnoreTest();
            }
        }

        private void Update()
        {
            if (runTestOnStart && Time.time - _lastTestTime >= testInterval)
            {
                RunLogTypeIgnoreTest();
                _lastTestTime = Time.time;
            }
        }

        /// <summary>
        /// 运行日志类型忽略测试
        /// </summary>
        [ContextMenu("运行日志类型忽略测试")]
        public void RunLogTypeIgnoreTest()
        {
            Debug.Log("[LogTypeIgnoreTest] 开始测试日志类型忽略功能...");
            
            // 获取LogController实例
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogTypeIgnoreTest] LogController未初始化，无法进行测试");
                return;
            }

            // 显示当前配置状态
            Debug.Log($"[LogTypeIgnoreTest] 当前配置: {logController.GetConfigSummary()}");
            
            // 测试Info日志
            Debug.Log("[LogTypeIgnoreTest] 这是一条Info日志 - 测试Info类型忽略功能");
            
            // 测试Warning日志
            Debug.LogWarning("[LogTypeIgnoreTest] 这是一条Warning日志 - 测试Warning类型忽略功能");
            
            // 测试Error日志
            Debug.LogError("[LogTypeIgnoreTest] 这是一条Error日志 - 测试Error类型忽略功能");
            
            Debug.Log("[LogTypeIgnoreTest] 日志类型忽略测试完成");
        }

        /// <summary>
        /// 测试配置更改
        /// </summary>
        [ContextMenu("测试配置更改")]
        public void TestConfigurationChanges()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogTypeIgnoreTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogTypeIgnoreTest] 开始测试配置更改...");
            
            // 测试禁用当前脚本路径
            string currentPath = GetCurrentScriptPath();
            Debug.Log($"[LogTypeIgnoreTest] 当前脚本路径: {currentPath}");
            
            // 禁用当前路径
            logController.DisablePath(currentPath);
            Debug.Log("[LogTypeIgnoreTest] 已禁用当前脚本路径，现在测试各种日志类型:");
            
            // 在禁用状态下测试各种日志类型
            Debug.Log("[LogTypeIgnoreTest] Info日志 - 应该根据忽略配置决定是否显示");
            Debug.LogWarning("[LogTypeIgnoreTest] Warning日志 - 应该根据忽略配置决定是否显示");
            Debug.LogError("[LogTypeIgnoreTest] Error日志 - 应该根据忽略配置决定是否显示");
            
            // 重新启用当前路径
            logController.EnablePath(currentPath);
            Debug.Log("[LogTypeIgnoreTest] 已重新启用当前脚本路径");
        }

        /// <summary>
        /// 获取当前脚本的文件路径
        /// </summary>
        private string GetCurrentScriptPath()
        {
            // 这是一个简化的路径获取方法
            // 在实际使用中，LogController会通过调用栈自动获取路径
            return "Assets/Scripts/Framework/Log/LogTypeIgnoreTest.cs";
        }

        /// <summary>
        /// 重置测试配置
        /// </summary>
        [ContextMenu("重置测试配置")]
        public void ResetTestConfiguration()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogTypeIgnoreTest] LogController未初始化");
                return;
            }

            // 重置为默认配置
            logController.SetIgnoreInfoLogs(false);
            logController.SetIgnoreWarningLogs(false);
            logController.SetIgnoreErrorLogs(true); // Error默认被忽略
            
            Debug.Log("[LogTypeIgnoreTest] 已重置为默认配置 - Error日志被忽略，Info和Warning不被忽略");
        }

        /// <summary>
        /// 测试模式切换功能
        /// </summary>
        [ContextMenu("测试模式切换功能")]
        public void TestModeSwitching()
        {
            Debug.Log("[LogTypeIgnoreTest] 开始测试模式切换功能...");
            
            var logController = LogController.Instance;
            if (logController == null)
            {
                Debug.LogError("[LogTypeIgnoreTest] LogController实例为null");
                return;
            }

            Debug.Log($"[LogTypeIgnoreTest] 当前模式: {(Application.isPlaying ? "Runtime" : "Editor")}");
            Debug.Log($"[LogTypeIgnoreTest] LogController状态: IsInitialized={logController.IsInitialized}");
            Debug.Log($"[LogTypeIgnoreTest] 配置摘要: {logController.GetConfigSummary()}");
            
            // 测试各种日志类型
            Debug.Log("[LogTypeIgnoreTest] Info日志测试 - 模式切换后");
            Debug.LogWarning("[LogTypeIgnoreTest] Warning日志测试 - 模式切换后");
            Debug.LogError("[LogTypeIgnoreTest] Error日志测试 - 模式切换后");
            
            Debug.Log("[LogTypeIgnoreTest] 模式切换测试完成");
        }

        /// <summary>
        /// 强制重新初始化LogController
        /// </summary>
        [ContextMenu("强制重新初始化LogController")]
        public void ForceReinitializeLogController()
        {
            Debug.Log("[LogTypeIgnoreTest] 开始强制重新初始化LogController...");
            
            var logController = LogController.Instance;
            if (logController != null)
            {
                logController.ForceReinitialize();
                Debug.Log("[LogTypeIgnoreTest] LogController已强制重新初始化");
                Debug.Log($"[LogTypeIgnoreTest] 新状态: IsInitialized={logController.IsInitialized}");
            }
            else
            {
                Debug.LogError("[LogTypeIgnoreTest] LogController实例为null，无法重新初始化");
            }
        }
    }
}