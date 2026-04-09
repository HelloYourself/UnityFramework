using UnityEngine;
using Framework.Log;
using System.IO;

namespace Framework.Log
{
    /// <summary>
    /// 日志文件删除测试脚本
    /// 用于验证日志文件删除功能是否正常工作
    /// </summary>
    public class LogFileDeleteTest : MonoBehaviour
    {
        [ContextMenu("测试日志文件删除")]
        public void TestLogFileDeletion()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogFileDeleteTest] LogController未初始化");
                return;
            }

            Debug.Log("[LogFileDeleteTest] 开始测试日志文件删除功能...");

            // 输出一些日志确保文件被创建
            for (int i = 0; i < 10; i++)
            {
                Debug.Log($"[LogFileDeleteTest] 测试日志 #{i}");
            }

            // 刷新日志到文件
            logController.FlushFileLog();

            string currentLogFile = logController.GetCurrentLogFilePath();
            Debug.Log($"[LogFileDeleteTest] 当前日志文件: {currentLogFile}");

            if (!string.IsNullOrEmpty(currentLogFile) && File.Exists(currentLogFile))
            {
                Debug.Log("[LogFileDeleteTest] 日志文件存在，可以在Editor中测试删除功能");
                
                // 显示文件信息
                FileInfo fileInfo = new FileInfo(currentLogFile);
                Debug.Log($"[LogFileDeleteTest] 文件大小: {fileInfo.Length} 字节");
                Debug.Log($"[LogFileDeleteTest] 最后修改时间: {fileInfo.LastWriteTime}");
            }
            else
            {
                Debug.LogWarning("[LogFileDeleteTest] 当前日志文件不存在或路径为空");
            }

            Debug.Log("[LogFileDeleteTest] 测试完成，请在Editor的日志管理页签中测试删除功能");
        }

        [ContextMenu("测试文件句柄释放")]
        public void TestFileHandleRelease()
        {
            var logController = LogController.Instance;
            if (logController == null || !logController.IsInitialized)
            {
                Debug.LogError("[LogFileDeleteTest] LogController未初始化");
                return;
            }

            string currentLogFile = logController.GetCurrentLogFilePath();
            Debug.Log($"[LogFileDeleteTest] 当前日志文件: {currentLogFile}");

            if (!string.IsNullOrEmpty(currentLogFile))
            {
                Debug.Log("[LogFileDeleteTest] 临时关闭日志文件...");
                logController.TemporaryCloseCurrentLogFile();

                Debug.Log("[LogFileDeleteTest] 等待2秒...");
                System.Threading.Thread.Sleep(2000);

                Debug.Log("[LogFileDeleteTest] 重新打开日志文件...");
                logController.ReopenCurrentLogFile();

                Debug.Log("[LogFileDeleteTest] 文件句柄释放测试完成");
            }
        }

        [ContextMenu("输出大量日志")]
        public void GenerateLotsOfLogs()
        {
            Debug.Log("[LogFileDeleteTest] 开始输出大量日志...");
            
            for (int i = 0; i < 100; i++)
            {
                Debug.Log($"[LogFileDeleteTest] 大量日志测试 #{i} - 这是一条测试日志，用于增加文件大小");
                
                if (i % 10 == 0)
                {
                    Debug.LogWarning($"[LogFileDeleteTest] Warning日志 #{i}");
                }
                
                if (i % 20 == 0)
                {
                    Debug.LogError($"[LogFileDeleteTest] Error日志 #{i}");
                }
            }
            
            Debug.Log("[LogFileDeleteTest] 大量日志输出完成");
        }
    }
}