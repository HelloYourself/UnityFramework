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
    /// 文件日志配置持久化。
    /// </summary>
    public static class LogFileConfigPersistence
    {
        private const string CONFIG_FILE = "Asset/LogFileConfig.json";

        /// <summary>
        /// 保存文件日志配置到本地可写目录。
        /// </summary>
        public static bool SaveConfig(LogFileConfig config)
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

                Debug.Log($"[LogController] 文件日志配置已保存: {configPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogController] 保存文件日志配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载文件日志配置，失败时回落默认配置。
        /// </summary>
        public static async UniTask<LogFileConfig> LoadConfigAsync(CancellationToken ct = default)
        {
            try
            {
                string loadText = await FileUtil.LoadTextAsync(CONFIG_FILE, ct);
                if (string.IsNullOrEmpty(loadText))
                {
                    Debug.LogWarning("[LogController] 文件日志配置为空，使用默认配置");
                    return CreateDefaultConfig();
                }

                LogFileConfig config = JsonUtility.FromJson<LogFileConfig>(loadText);
                if (config == null)
                {
                    Debug.LogWarning("[LogController] 文件日志配置反序列化失败，使用默认配置");
                    return CreateDefaultConfig();
                }

                if (!IsConfigValid(config))
                {
                    Debug.LogWarning("[LogController] 文件日志配置无效，使用默认配置");
                    return CreateDefaultConfig();
                }

                Debug.Log($"[LogController] 文件日志配置加载成功: {config.GetConfigSummary()}");
                return config;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogController] 加载文件日志配置失败: {ex.Message}，使用默认配置");
                return CreateDefaultConfig();
            }
        }

        /// <summary>
        /// 创建默认文件日志配置。
        /// </summary>
        public static LogFileConfig CreateDefaultConfig()
        {
            LogFileConfig config = new LogFileConfig();
            config.ResetToDefault();

            Debug.Log($"[LogController] 创建默认文件日志配置: {config.GetConfigSummary()}");
            return config;
        }

        /// <summary>
        /// 配置有效性校验。
        /// </summary>
        private static bool IsConfigValid(LogFileConfig config)
        {
            if (config == null)
                return false;

            if (string.IsNullOrEmpty(config.Version))
                return false;

            if (config.EnabledPaths == null || config.DisabledPaths == null)
                return false;

            if (config.MaxFileSizeMB <= 0 || config.MaxFileCount <= 0)
                return false;

            if (!string.IsNullOrEmpty(config.LogRootPath))
            {
                try
                {
                    Path.GetFullPath(config.LogRootPath);
                }
                catch
                {
                    Debug.LogWarning($"[LogController] 无效日志根目录: {config.LogRootPath}");
                    return false;
                }
            }

            return true;
        }
    }
}
