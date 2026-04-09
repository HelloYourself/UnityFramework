using UnityEngine;
using Framework.Log;

namespace Framework.Log
{
    /// <summary>
    /// 日志控制器测试脚本
    /// 用于验证日志控制功能是否正常工作
    /// </summary>
    public class LogControllerTest : MonoBehaviour
    {
        [Header("测试设置")]
        [SerializeField] private bool enableTestOnStart = true;
        [SerializeField] private float testInterval = 2f;
        
        private float _lastTestTime;

        private void Start()
        {
            if (enableTestOnStart)
            {
                // 等待LogController初始化完成
                Invoke(nameof(RunInitialTest), 1f);
            }
        }

        private void Update()
        {
            if (enableTestOnStart && Time.time - _lastTestTime > testInterval)
            {
                _lastTestTime = Time.time;
                RunPeriodicTest();
            }
        }

        /// <summary>
        /// 运行初始测试
        /// </summary>
        private void RunInitialTest()
        {
            Debug.Log("[LogControllerTest] 开始测试日志控制器功能");
            
            var controller = LogController.Instance;
            if (controller.IsInitialized)
            {
                Debug.Log("[LogControllerTest] 日志控制器已初始化");
                Debug.Log($"[LogControllerTest] 配置状态: {controller.GetConfigSummary()}");
                
                // 测试当前文件的日志状态
                string currentFilePath = GetCurrentFilePath();
                bool isEnabled = controller.IsPathEnabled(currentFilePath);
                Debug.Log($"[LogControllerTest] 当前文件 ({currentFilePath}) 日志状态: {(isEnabled ? "启用" : "禁用")}");
            }
            else
            {
                Debug.LogError("[LogControllerTest] 日志控制器未初始化");
            }
        }

        /// <summary>
        /// 运行周期性测试
        /// </summary>
        private void RunPeriodicTest()
        {
            // 这些日志会根据配置决定是否显示
            Debug.Log($"[LogControllerTest] 周期性测试日志 - 时间: {Time.time:F2}");
            Debug.LogWarning($"[LogControllerTest] 测试警告日志 - 帧数: {Time.frameCount}");
            
            // 每10秒输出一次详细信息
            if (Mathf.FloorToInt(Time.time) % 10 == 0 && Time.time - _lastTestTime < 0.1f)
            {
                Debug.Log($"[LogControllerTest] 详细测试信息 - FPS: {1f / Time.deltaTime:F1}");
            }
        }

        /// <summary>
        /// 手动测试方法（可在Inspector中调用）
        /// </summary>
        [ContextMenu("运行手动测试")]
        public void RunManualTest()
        {
            Debug.Log("[LogControllerTest] === 手动测试开始 ===");
            
            var controller = LogController.Instance;
            string currentFilePath = GetCurrentFilePath();
            
            Debug.Log($"[LogControllerTest] 当前文件路径: {currentFilePath}");
            Debug.Log($"[LogControllerTest] 日志状态: {(controller.IsPathEnabled(currentFilePath) ? "启用" : "禁用")}");
            Debug.Log($"[LogControllerTest] 配置摘要: {controller.GetConfigSummary()}");
            
            // 测试不同类型的日志
            Debug.Log("[LogControllerTest] 这是一条普通日志");
            Debug.LogWarning("[LogControllerTest] 这是一条警告日志");
            Debug.LogError("[LogControllerTest] 这是一条错误日志");
            
            Debug.Log("[LogControllerTest] === 手动测试结束 ===");
        }

        /// <summary>
        /// 测试配置更改
        /// </summary>
        [ContextMenu("测试配置更改")]
        public void TestConfigChange()
        {
            var controller = LogController.Instance;
            string currentFilePath = GetCurrentFilePath();
            
            bool currentState = controller.IsPathEnabled(currentFilePath);
            
            Debug.Log($"[LogControllerTest] 当前状态: {(currentState ? "启用" : "禁用")}");
            
            if (currentState)
            {
                controller.DisablePath(currentFilePath);
                Debug.Log("[LogControllerTest] 已禁用当前文件的日志");
            }
            else
            {
                controller.EnablePath(currentFilePath);
                Debug.Log("[LogControllerTest] 已启用当前文件的日志");
            }
            
            // 保存配置
            controller.SaveConfig();
            
            Debug.Log("[LogControllerTest] 配置已保存");
            Debug.Log("[LogControllerTest] 这条日志应该根据新配置显示或隐藏");
        }

        /// <summary>
        /// 获取当前文件路径
        /// </summary>
        private string GetCurrentFilePath()
        {
            // 这是一个简化的实现，实际路径可能需要根据项目结构调整
            return System.IO.Path.Combine(Application.dataPath, "Scripts", "Framework", "Log", "LogControllerTest.cs")
                .Replace('\\', '/');
        }

        /// <summary>
        /// 测试大量日志输出
        /// </summary>
        [ContextMenu("测试大量日志")]
        public void TestMassiveLogging()
        {
            Debug.Log("[LogControllerTest] 开始大量日志测试...");
            
            for (int i = 0; i < 100; i++)
            {
                Debug.Log($"[LogControllerTest] 批量日志 #{i}");
                
                if (i % 10 == 0)
                {
                    Debug.LogWarning($"[LogControllerTest] 批量警告 #{i}");
                }
                
                if (i % 25 == 0)
                {
                    Debug.LogError($"[LogControllerTest] 批量错误 #{i}");
                }
            }
            
            Debug.Log("[LogControllerTest] 大量日志测试完成");
        }
    }
}