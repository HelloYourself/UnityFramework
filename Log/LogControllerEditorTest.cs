using UnityEditor;
using UnityEngine;
using Framework.Log;

namespace Framework.Editor.Log
{
    /// <summary>
    /// LogControllerEditor测试工具
    /// 用于验证Editor窗口的功能
    /// </summary>
    public static class LogControllerEditorTest
    {
        [MenuItem("程序/调试/Log Controller/Test Editor Window", false, 101)]
        public static void TestEditorWindow()
        {
            UnityEngine.Debug.Log("[LogControllerEditorTest] 开始测试Editor窗口...");

            // 打开窗口
            var window = EditorWindow.GetWindow<LogControllerEditor>("Log Controller");
            window.Show();

            UnityEngine.Debug.Log("[LogControllerEditorTest] Editor窗口已打开");

            // 测试LogController状态
            var controller = LogController.Instance;
            UnityEngine.Debug.Log(
                $"[LogControllerEditorTest] LogController状态: IsInitialized = {controller.IsInitialized}");

            if (controller.IsInitialized)
            {
                UnityEngine.Debug.Log($"[LogControllerEditorTest] 配置摘要: {controller.GetConfigSummary()}");
            }
        }

        [MenuItem("程序/调试/Log Controller/Force Reinitialize", false, 102)]
        public static void ForceReinitialize()
        {
            UnityEngine.Debug.Log("[LogControllerEditorTest] 强制重新初始化LogController...");

            var controller = LogController.Instance;
            controller.ReloadConfig();

            UnityEngine.Debug.Log("[LogControllerEditorTest] 重新初始化完成");

            // 刷新所有打开的LogControllerEditor窗口
            var windows = Resources.FindObjectsOfTypeAll<LogControllerEditor>();
            foreach (var window in windows)
            {
                window.Repaint();
            }
        }

        [MenuItem("程序/调试/Log Controller/Debug Info", false, 103)]
        public static void ShowDebugInfo()
        {
            var controller = LogController.Instance;

            UnityEngine.Debug.Log("=== LogController Debug Info ===");
            UnityEngine.Debug.Log($"IsInitialized: {controller.IsInitialized}");
            UnityEngine.Debug.Log($"IsIntercepting: {controller.IsIntercepting}");

            if (controller.Config != null)
            {
                UnityEngine.Debug.Log($"Config Summary: {controller.GetConfigSummary()}");
                UnityEngine.Debug.Log($"Enabled Paths Count: {controller.Config.EnabledPaths.Count}");
                UnityEngine.Debug.Log($"Disabled Paths Count: {controller.Config.DisabledPaths.Count}");

                foreach (var path in controller.Config.EnabledPaths)
                {
                    UnityEngine.Debug.Log($"  Enabled: {path}");
                }

                foreach (var path in controller.Config.DisabledPaths)
                {
                    UnityEngine.Debug.Log($"  Disabled: {path}");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("Config is null");
            }

            UnityEngine.Debug.Log("=== End Debug Info ===");
        }

        [MenuItem("程序/调试/Log Controller/真机测试", false, 104)]
        public static void ShowRuntimeTest()
        {
            var testStr = "Boot.Update.LaunchRemainCSharpEnvFlow:CallRuntimeInitializ";
            LogController.Instance.IsFilePathEnabled(testStr);
        }
    }
}