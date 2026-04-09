using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Util;
using UnityEngine;
using FileUtil = Framework.Util.FileUtil;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if !UNITY_EDITOR && UNITY_WEBGL
using Directory = Framework.WebGLIO.WebGLDirectory;
using File = Framework.WebGLIO.WebGLFile;
using Path = Framework.WebGLIO.WebGLPath;
#endif

namespace Framework.Log
{
    /// <summary>
    /// 控制台日志配置持久化。
    /// </summary>
    public static class LogConfigPersistence
    {
        private const string CONFIG_FILE = "Asset/LogConfig.json";

        /// <summary>
        /// 保存配置到本地可写目录。
        /// </summary>
        public static bool SaveConfig(LogConfig config)
        {
            try
            {
                string configPath = FileUtil.GetAssetPath(CONFIG_FILE);
                string dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(configPath, json);

                Debug.Log($"[LogController] 配置已保存到: {configPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogController] 保存配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载配置，读不到时返回默认配置。
        /// </summary>
        public static async UniTask<LogConfig> LoadConfigAsync(CancellationToken ct = default)
        {
            try
            {
                string loadText = await FileUtil.LoadTextAsync(CONFIG_FILE, ct);
                if (string.IsNullOrEmpty(loadText))
                {
                    Debug.LogWarning("[LogController] 配置文件为空，使用默认配置");
                    return CreateDefaultConfig();
                }

                LogConfig config = JsonUtility.FromJson<LogConfig>(loadText);
                if (config == null)
                {
                    Debug.LogWarning("[LogController] 配置文件格式错误，使用默认配置");
                    return CreateDefaultConfig();
                }

                Debug.Log($"[LogController] 配置已从文件加载: {CONFIG_FILE}");
                Debug.Log($"[LogController] {config.GetConfigSummary()}");
                return config;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogController] 加载配置失败: {ex.Message}，使用默认配置");
                return CreateDefaultConfig();
            }
        }

        /// <summary>
        /// 创建默认配置。
        /// </summary>
        public static LogConfig CreateDefaultConfig()
        {
            LogConfig config = new LogConfig();
            config.SetDefaultEnabled(false);
            return config;
        }
    }
}
