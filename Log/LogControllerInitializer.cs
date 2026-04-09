using UnityEngine;

namespace Framework.Log
{
    /// <summary>
    /// 日志控制器初始化器
    /// 确保LogController在Unity启动时自动初始化
    /// </summary>
    public static class LogControllerInitializer
    {
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // 在场景加载前初始化日志控制器
            var controller = LogController.Instance;
            
            // 确保在应用退出时保存配置
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            LogController.Instance.Dispose();
        }
    }
}