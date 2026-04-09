using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Framework.Log;

namespace Framework.Editor.Log
{
    /// <summary>
    /// 日志控制器编辑器窗口
    /// 提供可视化的日志输出控制界面，支持控制台输出和本地文件落地两个功能页签
    /// </summary>
    public class LogControllerEditor : EditorWindow
    {
        private LogController _logController;
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showOnlyConfigured = false;
        private bool _expandAll = false;
        
        // 页签相关
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "控制台输出", "本地日志", "日志管理" };
        
        // 搜索选项
        private bool _ignoreCase = true;
        private bool _useRegex = false;
        
        // 鼠标悬停高亮
        private string _hoveredPath = null;
        
        // 模式切换检测
        private bool _lastIsPlaying = false;
        private bool _needsRefresh = false;
        
        // 文件树数据
        private List<FileTreeNode> _fileTree = new List<FileTreeNode>();
        private Dictionary<string, bool> _expandedFolders = new Dictionary<string, bool>();
        
        // 本地日志配置相关
        private string _logRootPath = "";
        private bool _enableDailyRotation = true;
        private int _maxFileSizeMB = 10;
        private int _maxFileCount = 30;
        private bool _writeInfoLogs = true;
        private bool _writeWarningLogs = true;
        private bool _writeErrorLogs = true;
        
        // 日志管理相关
        private Vector2 _logFilesScrollPosition;
        private List<string> _logFiles = new List<string>();
        
        // 样式
        private GUIStyle _folderStyle;
        private GUIStyle _fileStyle;
        private GUIStyle _enabledStyle;
        private GUIStyle _disabledStyle;
        
        [MenuItem("通用/日志控制器 %L", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<LogControllerEditor>("日志控制器");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeStyles();
            RefreshLogController();
            _lastIsPlaying = Application.isPlaying;
        }

        private void RefreshLogController()
        {
            try
            {
                _logController = LogController.Instance;
                
                // 如果LogController已初始化，刷新文件树
                if (_logController != null && _logController.IsInitialized)
                {
                    RefreshFileTree();
                    _needsRefresh = false;
                }
                else
                {
                    _needsRefresh = true;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LogControllerEditor] 刷新LogController失败: {ex.Message}");
                _needsRefresh = true;
            }
        }

        private void InitializeLogController(bool forceReinitialize)
        {
            try
            {
                UnityEngine.Debug.Log($"[LogControllerEditor] 开始{(forceReinitialize ? "强制重新" : "")}初始化LogController...");
                
                if (forceReinitialize && _logController != null)
                {
                    _logController.ForceReinitialize();
                }
                else
                {
                    _logController = LogController.Instance;
                }
                
                // 等待一帧确保初始化完成
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        // 重新获取实例以确保状态同步
                        _logController = LogController.Instance;
                        
                        if (_logController != null && _logController.IsInitialized)
                        {
                            RefreshFileTree();
                            InitializeStyles(); // 重新初始化样式
                            _needsRefresh = false;
                            UnityEngine.Debug.Log($"[LogControllerEditor] 初始化成功，文件树已刷新，包含 {_fileTree.Count} 个根节点");
                            Repaint(); // 强制重绘窗口
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[LogControllerEditor] LogController创建成功但未完成初始化，请检查Console中的错误信息");
                            _needsRefresh = true;
                            Repaint(); // 即使失败也要重绘以显示错误状态
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[LogControllerEditor] 延迟初始化回调失败: {ex.Message}");
                        _needsRefresh = true;
                        Repaint();
                    }
                };
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LogControllerEditor] 初始化失败: {ex.Message}");
                _needsRefresh = true;
            }
        }

        private void InitializeStyles()
        {
            _folderStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            
            _fileStyle = new GUIStyle(EditorStyles.label);
            
            _enabledStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.green }
            };
            
            _disabledStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.red }
            };
        }

        private void OnGUI()
        {
            // 检测模式切换
            bool currentIsPlaying = Application.isPlaying;
            if (_lastIsPlaying != currentIsPlaying)
            {
                _lastIsPlaying = currentIsPlaying;
                // 模式切换时刷新LogController
                EditorApplication.delayCall += RefreshLogController;
            }

            // 如果需要刷新且LogController现在可用，则刷新
            if (_needsRefresh && _logController != null && _logController.IsInitialized)
            {
                RefreshFileTree();
                LoadFileConfigValues();
                RefreshLogFilesList();
                _needsRefresh = false;
            }

            // 处理鼠标事件
            HandleMouseEvents();
            
            if (_logController == null || !_logController.IsInitialized)
            {
                EditorGUILayout.HelpBox("日志控制器未初始化", MessageType.Warning);
                
                // 显示调试信息
                if (_logController != null)
                {
                    EditorGUILayout.LabelField($"控制器状态: 已创建但未初始化 (IsInitialized: {_logController.IsInitialized})");
                }
                else
                {
                    EditorGUILayout.LabelField("控制器状态: 未创建");
                }
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("初始化"))
                {
                    InitializeLogController(false);
                }
                
                if (GUILayout.Button("强制重新初始化"))
                {
                    InitializeLogController(true);
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            // 绘制页签
            DrawTabs();
            
            // 根据选中的页签绘制不同的内容
            switch (_selectedTab)
            {
                case 0: // 控制台输出
                    DrawConsoleOutputTab();
                    break;
                case 1: // 本地日志
                    DrawFileLogTab();
                    break;
                case 2: // 日志管理
                    DrawLogManagementTab();
                    break;
            }
            
            // 清理鼠标悬停状态
            HandleMouseLeave();
        }
        
        private void HandleMouseLeave()
        {
            Event currentEvent = Event.current;
            
            // 在每次重绘时检查鼠标位置，如果不在任何行内则清除悬停状态
            if (currentEvent.type == EventType.Repaint)
            {
                // 简单的清理逻辑：如果鼠标不在窗口内，清除悬停状态
                Rect windowRect = new Rect(0, 0, position.width, position.height);
                if (!windowRect.Contains(currentEvent.mousePosition) && _hoveredPath != null)
                {
                    _hoveredPath = null;
                    Repaint();
                }
            }
        }

        private void DrawToolbar()
        {
            // 合并的搜索和控制工具栏 - 增大行高50%
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
            
            // 搜索框 - 增大尺寸
            GUILayout.Label("搜索:", GUILayout.Width(40));
            string newSearchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarTextField, GUILayout.MinWidth(200));
            if (newSearchFilter != _searchFilter)
            {
                _searchFilter = newSearchFilter;
                RefreshFileTree();
            }
            
            // 清空搜索按钮 - 紧挨着搜索框
            if (!string.IsNullOrEmpty(_searchFilter) && GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                _searchFilter = "";
                RefreshFileTree();
            }
            
            // 搜索选项
            bool newIgnoreCase = GUILayout.Toggle(_ignoreCase, "忽略大小写", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (newIgnoreCase != _ignoreCase)
            {
                _ignoreCase = newIgnoreCase;
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    RefreshFileTree();
                }
            }
            
            bool newUseRegex = GUILayout.Toggle(_useRegex, "正则表达式", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (newUseRegex != _useRegex)
            {
                _useRegex = newUseRegex;
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    RefreshFileTree();
                }
            }
            
            // 分隔符
            GUILayout.Space(10);
            
            // 显示选项
            _showOnlyConfigured = GUILayout.Toggle(_showOnlyConfigured, "仅显示已配置", EditorStyles.toolbarButton);
            
            // 展开/折叠按钮
            if (GUILayout.Button(_expandAll ? "全部折叠" : "全部展开", EditorStyles.toolbarButton))
            {
                _expandAll = !_expandAll;
                ExpandCollapseAll(_expandAll);
            }
            
            // 刷新按钮
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
            {
                RefreshFileTree();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 搜索提示
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField("搜索提示:", EditorStyles.miniLabel, GUILayout.Width(60));
                string tip = _useRegex ? "正则表达式模式" : "普通文本匹配";
                tip += _ignoreCase ? " (忽略大小写)" : " (区分大小写)";
                EditorGUILayout.LabelField(tip, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            
            // 第三行：全局控制按钮
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // 全局控制按钮
            if (GUILayout.Button("全部启用", EditorStyles.toolbarButton))
            {
                EnableAllScripts();
            }
            
            if (GUILayout.Button("全部禁用", EditorStyles.toolbarButton))
            {
                DisableAllScripts();
            }
            
            if (GUILayout.Button("重置配置", EditorStyles.toolbarButton))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要重置所有配置吗？", "确定", "取消"))
                {
                    ResetConfig();
                }
            }
            
            GUILayout.FlexibleSpace();
            
            // 保存按钮
            if (GUILayout.Button("保存配置", EditorStyles.toolbarButton))
            {
                _logController.SaveConfig();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigSummary()
        {
            if (_logController == null || _logController.Config == null)
                return;
                
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("配置状态", EditorStyles.boldLabel);
            
            // 使用文本区域显示配置摘要，避免文字被遮挡
            string configSummary = _logController.GetConfigSummary();
            EditorGUILayout.TextArea(configSummary, EditorStyles.wordWrappedLabel, GUILayout.Height(40));
            
            EditorGUILayout.Space(5);
            
            // Scripts目录控制
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scripts目录输出:", GUILayout.Width(100));
            bool scriptsEnabled = _logController.Config.IsScriptsEnabled();
            bool newScriptsEnabled = EditorGUILayout.Toggle(scriptsEnabled);
            if (newScriptsEnabled != scriptsEnabled)
            {
                _logController.Config.SetDefaultEnabled(newScriptsEnabled);
                _logController.SaveConfig();
                // 刷新文件树显示
                RefreshFileTree();
            }
            EditorGUILayout.EndHorizontal();
            
            if (scriptsEnabled)
            {
                EditorGUILayout.HelpBox("Scripts目录已启用：所有Scripts下的日志都会输出到控制台", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Scripts目录已禁用：Scripts下的日志不会输出到控制台", MessageType.Warning);
            }
            
            // 日志类型忽略配置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("日志类型忽略配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("被忽略的日志类型不受全局禁用影响，即使路径被禁用也会输出", MessageType.Info);
            
            // 使用更紧凑的布局显示三个选项
            EditorGUILayout.BeginHorizontal();
            
            // Info日志忽略
            EditorGUILayout.LabelField("忽略Info:", GUILayout.Width(70));
            bool newIgnoreInfo = EditorGUILayout.Toggle(_logController.Config.IgnoreInfoLogs, GUILayout.Width(20));
            if (newIgnoreInfo != _logController.Config.IgnoreInfoLogs)
            {
                _logController.SetIgnoreInfoLogs(newIgnoreInfo);
            }
            
            GUILayout.Space(10);
            
            // Warning日志忽略
            EditorGUILayout.LabelField("忽略Warning:", GUILayout.Width(90));
            bool newIgnoreWarning = EditorGUILayout.Toggle(_logController.Config.IgnoreWarningLogs, GUILayout.Width(20));
            if (newIgnoreWarning != _logController.Config.IgnoreWarningLogs)
            {
                _logController.SetIgnoreWarningLogs(newIgnoreWarning);
            }
            
            GUILayout.Space(10);
            
            // Error日志忽略
            EditorGUILayout.LabelField("忽略Error:", GUILayout.Width(80));
            bool newIgnoreError = EditorGUILayout.Toggle(_logController.Config.IgnoreErrorLogs, GUILayout.Width(20));
            if (newIgnoreError != _logController.Config.IgnoreErrorLogs)
            {
                _logController.SetIgnoreErrorLogs(newIgnoreError);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawFileTree()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(25)); // 增大行高
            EditorGUILayout.LabelField("文件树", EditorStyles.boldLabel);
            
            // 显示搜索状态
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                int totalNodes = CountTotalNodes();
                EditorGUILayout.LabelField($"(搜索: \"{_searchFilter}\", 找到 {totalNodes} 项)", EditorStyles.miniLabel);
                
                GUILayout.FlexibleSpace();
                
                // 一键启用/禁用所有搜索项的按钮
                if (GUILayout.Button("启用所有搜索项", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    EnableAllSearchResults();
                }
                
                if (GUILayout.Button("禁用所有搜索项", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    DisableAllSearchResults();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 表格头部
            DrawTableHeader();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_fileTree.Count == 0 && !string.IsNullOrEmpty(_searchFilter))
            {
                EditorGUILayout.HelpBox("未找到匹配的文件或文件夹", MessageType.Info);
            }
            else
            {
                foreach (var node in _fileTree)
                {
                    DrawFileTreeNode(node, 0);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private int CountTotalNodes()
        {
            int count = 0;
            foreach (var node in _fileTree)
            {
                count += CountNodesRecursive(node);
            }
            return count;
        }
        
        private int CountNodesRecursive(FileTreeNode node)
        {
            int count = 1; // 当前节点
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    count += CountNodesRecursive(child);
                }
            }
            return count;
        }
        
        private void DrawTableHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            
            // 左侧：文件/文件夹名称列
            EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width - 200));
            EditorGUILayout.LabelField("文件/文件夹", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            // 右侧：控制列
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            EditorGUILayout.LabelField("状态", EditorStyles.boldLabel, GUILayout.Width(35));
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel, GUILayout.Width(90));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 分隔线
            EditorGUILayout.Space(1);
            Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Color.gray);
        }

        private void DrawFileTreeNode(FileTreeNode node, int indentLevel)
        {
            if (node.IsDirectory)
            {
                DrawDirectoryNode(node, indentLevel);
            }
            else
            {
                DrawFileNode(node, indentLevel);
            }
        }

        private void DrawDirectoryNode(FileTreeNode node, int indentLevel)
        {
            // 检测鼠标悬停
            bool isHovered = _hoveredPath == node.Path;
            
            // 使用表格样式布局，如果悬停则使用高亮背景
            GUIStyle rowStyle = isHovered ? GetHoverStyle() : GUIStyle.none;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(20));
            
            // 左侧：缩进 + 展开/折叠 + 文件夹名
            EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width - 200)); // 为右侧按钮预留200px
            
            // 缩进
            GUILayout.Space(indentLevel * 20);
            
            // 展开/折叠
            bool isExpanded = _expandedFolders.GetValueOrDefault(node.Path, false);
            bool newExpanded = EditorGUILayout.Foldout(isExpanded, node.Name, _folderStyle);
            if (newExpanded != isExpanded)
            {
                _expandedFolders[node.Path] = newExpanded;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 右侧：固定宽度的控制按钮区域
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            DrawPathControlButtons(node.Path);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 检测鼠标事件
            if (Event.current.type == EventType.Repaint)
            {
                Rect rowRect = GUILayoutUtility.GetLastRect();
                HandleRowMouseEvents(rowRect, node.Path);
            }
            
            // 绘制子节点
            if (newExpanded && node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    DrawFileTreeNode(child, indentLevel + 1);
                }
            }
        }

        private void DrawFileNode(FileTreeNode node, int indentLevel)
        {
            // 检测鼠标悬停
            bool isHovered = _hoveredPath == node.Path;
            
            // 使用表格样式布局，如果悬停则使用高亮背景
            GUIStyle rowStyle = isHovered ? GetHoverStyle() : GUIStyle.none;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(18));
            
            // 左侧：缩进 + 文件图标 + 文件名
            EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width - 200)); // 为右侧按钮预留200px
            
            // 缩进
            GUILayout.Space(indentLevel * 20 + 20); // 额外缩进表示文件
            
            // 文件图标和名称
            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("cs Script Icon"), GUILayout.Width(16));
            EditorGUILayout.LabelField(node.Name, _fileStyle);
            
            EditorGUILayout.EndHorizontal();
            
            // 右侧：固定宽度的控制按钮区域
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            DrawPathControlButtons(node.Path);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 检测鼠标事件
            if (Event.current.type == EventType.Repaint)
            {
                Rect rowRect = GUILayoutUtility.GetLastRect();
                HandleRowMouseEvents(rowRect, node.Path);
            }
        }

        private void DrawPathControlButtons(string path)
        {
            if (_logController == null || _logController.Config == null)
                return;
                
            bool isEnabled = _logController.IsPathEnabled(path);
            bool hasConfig = _logController.Config.EnabledPaths.Contains(path) || 
                           _logController.Config.DisabledPaths.Contains(path);
            
            // 状态指示器 - 固定宽度
            GUIStyle statusStyle = isEnabled ? _enabledStyle : _disabledStyle;
            string statusText = isEnabled ? "启用" : "禁用";
            if (!hasConfig)
            {
                statusText = "继承";
                statusStyle = EditorStyles.label;
            }
            
            EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(35));
            
            // 控制按钮 - 紧凑布局
            if (GUILayout.Button(new GUIContent("启", "启用此路径的日志输出"), EditorStyles.miniButtonLeft, GUILayout.Width(30)))
            {
                _logController.EnablePath(path);
            }
            
            if (GUILayout.Button(new GUIContent("禁", "禁用此路径的日志输出"), EditorStyles.miniButtonMid, GUILayout.Width(30)))
            {
                _logController.DisablePath(path);
            }
            
            if (GUILayout.Button(new GUIContent("重", "重置为继承父目录设置"), EditorStyles.miniButtonRight, GUILayout.Width(30)))
            {
                _logController.Config.RemovePath(path);
            }
            
            // 填充剩余空间，确保右对齐
            GUILayout.FlexibleSpace();
        }

        private void RefreshFileTree()
        {
            try
            {
                _fileTree.Clear();
                
                string assetsPath = Application.dataPath;
                string scriptsPath = Path.Combine(assetsPath, "Scripts");
                
                if (Directory.Exists(scriptsPath))
                {
                    var rootNode = BuildFileTree(scriptsPath, "Scripts");
                    if (rootNode != null)
                    {
                        _fileTree.Add(rootNode);
                    }
                }
                
                // 如果有搜索条件，自动展开搜索结果
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    AutoExpandSearchResults();
                }
                
                // 刷新成功后清除需要刷新的标志
                _needsRefresh = false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LogControllerEditor] 刷新文件树失败: {ex.Message}");
                // 保持需要刷新的状态，以便稍后重试
                _needsRefresh = true;
            }
        }
        
        private void AutoExpandSearchResults()
        {
            foreach (var node in _fileTree)
            {
                ExpandNodeWithMatches(node);
            }
        }
        
        private void ExpandNodeWithMatches(FileTreeNode node)
        {
            if (node.IsDirectory)
            {
                // 如果当前节点或其子节点有匹配项，则展开
                if (NodeNameMatches(node.Name, _searchFilter) || HasMatchingChildren(node))
                {
                    _expandedFolders[node.Path] = true;
                }
                
                // 递归处理子节点
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        ExpandNodeWithMatches(child);
                    }
                }
            }
        }
        
        private void ExpandCollapseAll(bool expand)
        {
            // 清空当前的展开状态
            _expandedFolders.Clear();
            
            // 递归设置所有文件夹的展开状态
            foreach (var node in _fileTree)
            {
                SetExpandStateRecursive(node, expand);
            }
        }
        
        private void SetExpandStateRecursive(FileTreeNode node, bool expand)
        {
            if (node.IsDirectory)
            {
                _expandedFolders[node.Path] = expand;
                
                // 递归处理子节点，但只处理文件夹
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        // 只递归处理文件夹，跳过文件
                        if (child.IsDirectory)
                        {
                            SetExpandStateRecursive(child, expand);
                        }
                    }
                }
            }
        }

        private FileTreeNode BuildFileTree(string directoryPath, string displayName)
        {
            var node = new FileTreeNode
            {
                Name = displayName,
                Path = NormalizeConfigPath(directoryPath),
                IsDirectory = true,
                Children = new List<FileTreeNode>()
            };
            
            try
            {
                // 添加子目录
                var directories = Directory.GetDirectories(directoryPath)
                    .Where(dir => ShouldIncludeDirectory(dir))
                    .OrderBy(dir => Path.GetFileName(dir));
                
                foreach (string dir in directories)
                {
                    var childNode = BuildFileTree(dir, Path.GetFileName(dir));
                    if (childNode != null)
                    {
                        // 在搜索模式下，如果子目录包含匹配项，则包含该目录
                        if (string.IsNullOrEmpty(_searchFilter) || ShouldIncludeNodeInSearch(childNode))
                        {
                            node.Children.Add(childNode);
                        }
                    }
                }
                
                // 添加C#文件
                var files = Directory.GetFiles(directoryPath, "*.cs")
                    .Where(file => ShouldIncludeFile(file))
                    .OrderBy(file => Path.GetFileName(file));
                
                foreach (string file in files)
                {
                    var fileNode = new FileTreeNode
                    {
                        Name = Path.GetFileName(file),
                        Path = NormalizeConfigPath(file),
                        IsDirectory = false
                    };
                    
                    // 在搜索模式下，只包含匹配的文件
                    if (string.IsNullOrEmpty(_searchFilter) || ShouldIncludeNodeInSearch(fileNode))
                    {
                        node.Children.Add(fileNode);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"构建文件树时出错: {ex.Message}");
            }
            
            // 在搜索模式下，即使没有子节点，如果当前节点匹配搜索条件也应该返回
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                if (node.Children.Count > 0 || NodeNameMatches(node.Name, _searchFilter))
                {
                    return node;
                }
                return null;
            }
            
            return node.Children.Count > 0 ? node : null;
        }

        private bool ShouldIncludeDirectory(string directoryPath)
        {
            string dirName = Path.GetFileName(directoryPath);
            
            // 排除特定目录
            return !dirName.StartsWith(".") && 
                   !string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase) && 
                   !string.Equals(dirName, "obj", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldIncludeFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            
            // 排除meta文件和特定文件
            return !fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && 
                   !fileName.StartsWith(".");
        }

        private string NormalizeConfigPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/');

            int assetsIndex = normalized.LastIndexOf("/Assets/");
            if (assetsIndex >= 0)
            {
                normalized = normalized.Substring(assetsIndex + 1);
            }

            if (normalized.StartsWith("Assets/"))
            {
                normalized = normalized.Substring(7);
            }

            if (normalized.Length > 1 && normalized.EndsWith("/"))
            {
                normalized = normalized.TrimEnd('/');
            }

            return normalized;
        }

        private bool ShouldIncludeNode(FileTreeNode node)
        {
            // 搜索过滤
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                if (!MatchesSearchFilter(node))
                {
                    return false;
                }
            }
            
            // 仅显示已配置的项
            if (_showOnlyConfigured && _logController != null && _logController.Config != null)
            {
                bool hasConfig = _logController.Config.EnabledPaths.Contains(node.Path) || 
                               _logController.Config.DisabledPaths.Contains(node.Path);
                if (!hasConfig)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        private bool ShouldIncludeNodeInSearch(FileTreeNode node)
        {
            // 检查当前节点是否匹配搜索条件
            if (NodeNameMatches(node.Name, _searchFilter))
                return true;
                
            // 如果是目录，检查是否有匹配的子项
            if (node.IsDirectory && node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (ShouldIncludeNodeInSearch(child))
                        return true;
                }
            }
            
            return false;
        }
        
        private bool MatchesSearchFilter(FileTreeNode node)
        {
            if (string.IsNullOrEmpty(_searchFilter))
                return true;
                
            // 检查当前节点是否匹配
            if (NodeNameMatches(node.Name, _searchFilter))
                return true;
                
            // 如果是目录，递归检查子节点
            if (node.IsDirectory && node.Children != null)
            {
                return HasMatchingChildren(node);
            }
            
            return false;
        }
        
        private bool NodeNameMatches(string nodeName, string searchFilter)
        {
            try
            {
                if (_useRegex)
                {
                    var options = _ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                    return Regex.IsMatch(nodeName, searchFilter, options);
                }
                else
                {
                    var comparison = _ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    return nodeName.IndexOf(searchFilter, comparison) >= 0;
                }
            }
            catch (ArgumentException)
            {
                // 正则表达式无效时，回退到普通字符串匹配
                var comparison = _ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return nodeName.IndexOf(searchFilter, comparison) >= 0;
            }
        }
        
        private bool HasMatchingChildren(FileTreeNode node)
        {
            if (node.Children == null)
                return false;
                
            foreach (var child in node.Children)
            {
                if (NodeNameMatches(child.Name, _searchFilter))
                    return true;
                    
                if (child.IsDirectory && HasMatchingChildren(child))
                    return true;
            }
            
            return false;
        }

        private void EnableAllScripts()
        {
            if (_logController == null)
                return;
                
            _logController.EnablePath("Scripts");
            RefreshFileTree();
        }

        private void DisableAllScripts()
        {
            if (_logController == null)
                return;
                
            _logController.DisablePath("Scripts");
            RefreshFileTree();
        }

        private void ResetConfig()
        {
            if (_logController == null || _logController.Config == null)
                return;
                
            _logController.Config.Clear();
            _logController.Config.SetDefaultEnabled(false);
            RefreshFileTree();
        }
        
        private void EnableAllSearchResults()
        {
            if (_logController == null || string.IsNullOrEmpty(_searchFilter))
                return;
                
            foreach (var node in _fileTree)
            {
                EnableSearchResultsRecursive(node);
            }
        }
        
        private void DisableAllSearchResults()
        {
            if (_logController == null || string.IsNullOrEmpty(_searchFilter))
                return;
                
            foreach (var node in _fileTree)
            {
                DisableSearchResultsRecursive(node);
            }
        }
        
        private void EnableSearchResultsRecursive(FileTreeNode node)
        {
            // 如果当前节点匹配搜索条件，启用它
            if (NodeNameMatches(node.Name, _searchFilter))
            {
                _logController.EnablePath(node.Path);
            }
            
            // 递归处理子节点
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    EnableSearchResultsRecursive(child);
                }
            }
        }
        
        private void DisableSearchResultsRecursive(FileTreeNode node)
        {
            // 如果当前节点匹配搜索条件，禁用它
            if (NodeNameMatches(node.Name, _searchFilter))
            {
                _logController.DisablePath(node.Path);
            }
            
            // 递归处理子节点
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    DisableSearchResultsRecursive(child);
                }
            }
        }
        
        private string GetConfigFilePath()
        {
            // 配置文件保存在项目的Framework\Log文件夹下
            string assetsPath = Application.dataPath;
            string configFolder = Path.Combine(assetsPath, "Scripts", "Framework", "Log");
            return Path.Combine(configFolder, "LogConfig.json");
        }
        
        private void OpenConfigFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    // Windows系统打开文件夹
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", $"文件夹不存在:\n{folderPath}", "确定");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"无法打开文件夹:\n{ex.Message}", "确定");
            }
        }
        
        private GUIStyle GetHoverStyle()
        {
            var style = new GUIStyle();
            // 使用更鲜艳的橙色高亮，增加透明度使其更明显
            style.normal.background = MakeTexture(2, 2, new Color(1f, 0.5f, 0f, 0.6f)); // 鲜艳橙色高亮
            return style;
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        
        private void HandleMouseEvents()
        {
            Event currentEvent = Event.current;
            
            // 处理鼠标移动事件
            if (currentEvent.type == EventType.MouseMove)
            {
                Repaint(); // 鼠标移动时重绘界面
            }
        }
        
        private void HandleRowMouseEvents(Rect rect, string path)
        {
            Event currentEvent = Event.current;
            
            // 检查鼠标是否在当前行内
            if (rect.Contains(currentEvent.mousePosition))
            {
                if (_hoveredPath != path)
                {
                    _hoveredPath = path;
                    Repaint(); // 重绘界面以显示高亮效果
                }
            }
        }

        #region 页签功能

        /// <summary>
        /// 绘制页签
        /// </summary>
        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            int newSelectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(25));
            if (newSelectedTab != _selectedTab)
            {
                _selectedTab = newSelectedTab;
                // 切换页签时重置搜索
                _searchFilter = "";
                if (_selectedTab == 0)
                {
                    RefreshFileTree();
                }
                else if (_selectedTab == 2)
                {
                    RefreshLogFilesList();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        /// <summary>
        /// 绘制控制台输出页签内容
        /// </summary>
        private void DrawConsoleOutputTab()
        {
            DrawToolbar();
            DrawConfigSummary();
            DrawFileTree();
        }

        /// <summary>
        /// 绘制本地日志页签内容
        /// </summary>
        private void DrawFileLogTab()
        {
            DrawFileLogToolbar();
            DrawFileLogConfig();
            DrawFileLogFileTree();
        }

        /// <summary>
        /// 绘制日志管理页签内容
        /// </summary>
        private void DrawLogManagementTab()
        {
            DrawLogManagementToolbar();
            DrawLogFilesList();
        }

        #endregion

        #region 本地日志页签功能

        /// <summary>
        /// 绘制本地日志工具栏
        /// </summary>
        private void DrawFileLogToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
            
            // 搜索框
            GUILayout.Label("搜索:", GUILayout.Width(40));
            string newSearchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarTextField, GUILayout.MinWidth(200));
            if (newSearchFilter != _searchFilter)
            {
                _searchFilter = newSearchFilter;
                RefreshFileTree();
            }
            
            // 清空搜索按钮
            if (!string.IsNullOrEmpty(_searchFilter) && GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                _searchFilter = "";
                RefreshFileTree();
            }
            
            // 显示选项
            _showOnlyConfigured = GUILayout.Toggle(_showOnlyConfigured, "仅显示已配置", EditorStyles.toolbarButton);
            
            // 展开/折叠按钮
            if (GUILayout.Button(_expandAll ? "全部折叠" : "全部展开", EditorStyles.toolbarButton))
            {
                _expandAll = !_expandAll;
                ExpandCollapseAll(_expandAll);
            }
            
            // 刷新按钮
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
            {
                RefreshFileTree();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制本地日志配置
        /// </summary>
        private void DrawFileLogConfig()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("本地日志配置", EditorStyles.boldLabel);
            
            // 日志根目录配置
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("日志根目录:", GUILayout.Width(80));
            string newLogRootPath = EditorGUILayout.TextField(_logRootPath);
            if (newLogRootPath != _logRootPath)
            {
                _logRootPath = newLogRootPath;
                _logController?.SetLogRootPath(_logRootPath);
                _logController?.SaveFileConfig();
            }
            
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择日志根目录", _logRootPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _logRootPath = selectedPath;
                    _logController?.SetLogRootPath(_logRootPath);
                    _logController?.SaveFileConfig();
                }
            }
            
            if (GUILayout.Button("打开", GUILayout.Width(50)))
            {
                string actualPath = _logController?.GetLogRootPath() ?? "";
                if (!string.IsNullOrEmpty(actualPath))
                {
                    OpenConfigFolder(actualPath);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // 显示实际使用的路径
            string actualLogPath = _logController?.GetLogRootPath() ?? "";
            if (!string.IsNullOrEmpty(actualLogPath))
            {
                EditorGUILayout.LabelField($"实际路径: {actualLogPath}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.Space(5);
            
            // Scripts目录文件日志控制
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scripts目录文件日志:", GUILayout.Width(120));
            bool scriptsFileEnabled = _logController?.FileConfig?.IsScriptsEnabled() ?? false;
            bool newScriptsFileEnabled = EditorGUILayout.Toggle(scriptsFileEnabled);
            if (newScriptsFileEnabled != scriptsFileEnabled)
            {
                _logController?.SetFileLogDefaultEnabled(newScriptsFileEnabled);
                _logController?.SaveFileConfig();
                // 刷新文件树显示
                RefreshFileTree();
            }
            EditorGUILayout.EndHorizontal();
            
            if (scriptsFileEnabled)
            {
                EditorGUILayout.HelpBox("Scripts目录已启用：所有Scripts下的日志都会写入文件", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Scripts目录已禁用：Scripts下的日志不会写入文件", MessageType.Warning);
            }
            
            EditorGUILayout.Space(5);
            
            // 日志类型配置
            EditorGUILayout.LabelField("日志类型配置:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            bool newWriteInfoLogs = EditorGUILayout.Toggle("Info日志", _writeInfoLogs);
            bool newWriteWarningLogs = EditorGUILayout.Toggle("Warning日志", _writeWarningLogs);
            bool newWriteErrorLogs = EditorGUILayout.Toggle("Error日志", _writeErrorLogs);
            
            if (newWriteInfoLogs != _writeInfoLogs || newWriteWarningLogs != _writeWarningLogs || newWriteErrorLogs != _writeErrorLogs)
            {
                _writeInfoLogs = newWriteInfoLogs;
                _writeWarningLogs = newWriteWarningLogs;
                _writeErrorLogs = newWriteErrorLogs;
                
                _logController?.FileConfig?.SetLogTypeFileOutput(_writeInfoLogs, _writeWarningLogs, _writeErrorLogs);
                _logController?.SaveFileConfig();
            }
            EditorGUILayout.EndHorizontal();
            
            // 文件管理配置
            EditorGUILayout.LabelField("文件管理配置:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("最大文件大小(MB):", GUILayout.Width(120));
            int newMaxFileSizeMB = EditorGUILayout.IntField(_maxFileSizeMB, GUILayout.Width(60));
            
            EditorGUILayout.LabelField("最大文件数量:", GUILayout.Width(80));
            int newMaxFileCount = EditorGUILayout.IntField(_maxFileCount, GUILayout.Width(60));
            
            bool newEnableDailyRotation = EditorGUILayout.Toggle("按日期分文件", _enableDailyRotation);
            EditorGUILayout.EndHorizontal();
            
            if (newMaxFileSizeMB != _maxFileSizeMB || newMaxFileCount != _maxFileCount || newEnableDailyRotation != _enableDailyRotation)
            {
                _maxFileSizeMB = Mathf.Max(1, newMaxFileSizeMB);
                _maxFileCount = Mathf.Max(1, newMaxFileCount);
                _enableDailyRotation = newEnableDailyRotation;
                
                _logController?.FileConfig?.SetFileManagementConfig(_maxFileSizeMB, _maxFileCount, _enableDailyRotation);
                _logController?.SaveFileConfig();
            }
            
            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存配置"))
            {
                _logController?.SaveFileConfig();
                EditorUtility.DisplayDialog("提示", "本地日志配置已保存", "确定");
            }
            
            if (GUILayout.Button("重新加载配置"))
            {
                _logController?.ReloadFileConfig();
                LoadFileConfigValues();
                EditorUtility.DisplayDialog("提示", "本地日志配置已重新加载", "确定");
            }
            
            if (GUILayout.Button("刷新日志"))
            {
                _logController?.FlushFileLog();
                EditorUtility.DisplayDialog("提示", "日志已刷新到文件", "确定");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        /// <summary>
        /// 绘制本地日志文件树
        /// </summary>
        private void DrawFileLogFileTree()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
            EditorGUILayout.LabelField("文件路径控制 (控制哪些文件的日志写入本地文件)", EditorStyles.boldLabel);
            
            // 显示搜索状态
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                int totalNodes = CountTotalNodes();
                EditorGUILayout.LabelField($"(搜索: \"{_searchFilter}\", 找到 {totalNodes} 项)", EditorStyles.miniLabel);
                
                GUILayout.FlexibleSpace();
                
                // 一键启用/禁用所有搜索项的按钮
                if (GUILayout.Button("启用所有搜索项", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    EnableAllFileLogSearchResults();
                }
                
                if (GUILayout.Button("禁用所有搜索项", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    DisableAllFileLogSearchResults();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 表格头部
            DrawFileLogTableHeader();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_fileTree.Count == 0 && !string.IsNullOrEmpty(_searchFilter))
            {
                EditorGUILayout.HelpBox("未找到匹配的文件或文件夹", MessageType.Info);
            }
            else
            {
                foreach (var node in _fileTree)
                {
                    DrawFileLogTreeNode(node, 0);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制本地日志文件树表格头部
        /// </summary>
        private void DrawFileLogTableHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            
            // 左侧：文件/文件夹名称列
            EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width - 200));
            EditorGUILayout.LabelField("文件/文件夹", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            // 右侧：控制列
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            EditorGUILayout.LabelField("状态", EditorStyles.boldLabel, GUILayout.Width(35));
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel, GUILayout.Width(90));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 分隔线
            EditorGUILayout.Space(1);
            Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Color.gray);
        }

        /// <summary>
        /// 绘制本地日志文件树节点
        /// </summary>
        private void DrawFileLogTreeNode(FileTreeNode node, int indentLevel)
        {
            if (!ShouldShowNode(node))
                return;

            if (node.IsDirectory)
            {
                DrawFileLogDirectoryNode(node, indentLevel);
            }
            else
            {
                DrawFileLogFileNode(node, indentLevel);
            }
        }

        /// <summary>
        /// 绘制本地日志文件夹节点
        /// </summary>
        private void DrawFileLogDirectoryNode(FileTreeNode node, int indentLevel)
        {
            // 检测鼠标悬停
            bool isHovered = _hoveredPath == node.Path;
            
            // 使用表格样式布局，如果悬停则使用高亮背景
            GUIStyle rowStyle = isHovered ? GetHoverStyle() : GUIStyle.none;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(20));
            
            // 左侧：缩进 + 展开/折叠 + 文件夹名
            EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width - 200)); // 为右侧按钮预留200px
            
            // 缩进
            GUILayout.Space(indentLevel * 20);
            
            // 展开/折叠
            bool isExpanded = _expandedFolders.GetValueOrDefault(node.Path, false);
            bool newExpanded = EditorGUILayout.Foldout(isExpanded, node.Name, _folderStyle);
            if (newExpanded != isExpanded)
            {
                _expandedFolders[node.Path] = newExpanded;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 右侧：固定宽度的控制按钮区域
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            DrawFileLogPathControlButtons(node.Path);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 检测鼠标事件
            if (Event.current.type == EventType.Repaint)
            {
                Rect rowRect = GUILayoutUtility.GetLastRect();
                HandleRowMouseEvents(rowRect, node.Path);
            }
            
            // 绘制子节点
            if (newExpanded && node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    DrawFileLogTreeNode(child, indentLevel + 1);
                }
            }
        }

        /// <summary>
        /// 绘制本地日志文件节点
        /// </summary>
        private void DrawFileLogFileNode(FileTreeNode node, int indentLevel)
        {
            // 检测鼠标悬停
            bool isHovered = _hoveredPath == node.Path;
            
            // 使用表格样式布局，如果悬停则使用高亮背景
            GUIStyle rowStyle = isHovered ? GetHoverStyle() : GUIStyle.none;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(18));
            
            // 左侧：缩进 + 文件图标 + 文件名
            EditorGUILayout.BeginHorizontal(GUILayout.Width(position.width - 200)); // 为右侧按钮预留200px
            
            // 缩进
            GUILayout.Space(indentLevel * 20 + 20); // 额外缩进表示文件
            
            // 文件图标和名称
            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("cs Script Icon"), GUILayout.Width(16));
            EditorGUILayout.LabelField(node.Name, _fileStyle);
            
            EditorGUILayout.EndHorizontal();
            
            // 右侧：固定宽度的控制按钮区域
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            DrawFileLogPathControlButtons(node.Path);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 检测鼠标事件
            if (Event.current.type == EventType.Repaint)
            {
                Rect rowRect = GUILayoutUtility.GetLastRect();
                HandleRowMouseEvents(rowRect, node.Path);
            }
        }

        /// <summary>
        /// 绘制本地日志路径控制按钮
        /// </summary>
        private void DrawFileLogPathControlButtons(string path)
        {
            if (_logController == null || _logController.FileConfig == null)
                return;
                
            bool isEnabled = _logController.IsFilePathEnabled(path);
            bool hasConfig = _logController.FileConfig.EnabledPaths.Contains(path) || 
                           _logController.FileConfig.DisabledPaths.Contains(path);
            
            // 状态指示器 - 固定宽度
            GUIStyle statusStyle = isEnabled ? _enabledStyle : _disabledStyle;
            string statusText = isEnabled ? "启用" : "禁用";
            if (!hasConfig)
            {
                statusText = "继承";
                statusStyle = EditorStyles.label;
            }
            
            EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(35));
            
            // 控制按钮 - 紧凑布局
            if (GUILayout.Button(new GUIContent("启", "启用此路径的文件日志输出"), EditorStyles.miniButtonLeft, GUILayout.Width(30)))
            {
                _logController.EnableFilePath(path);
                _logController.SaveFileConfig();
            }
            
            if (GUILayout.Button(new GUIContent("禁", "禁用此路径的文件日志输出"), EditorStyles.miniButtonMid, GUILayout.Width(30)))
            {
                _logController.DisableFilePath(path);
                _logController.SaveFileConfig();
            }
            
            if (GUILayout.Button(new GUIContent("清", "清除此路径的配置，使用继承设置"), EditorStyles.miniButtonRight, GUILayout.Width(30)))
            {
                _logController.FileConfig.EnabledPaths.Remove(path);
                _logController.FileConfig.DisabledPaths.Remove(path);
                _logController.FileConfig.ClearCache();
                _logController.SaveFileConfig();
            }
        }

        /// <summary>
        /// 启用所有文件日志搜索结果
        /// </summary>
        private void EnableAllFileLogSearchResults()
        {
            if (_logController == null || string.IsNullOrEmpty(_searchFilter))
                return;
                
            foreach (var node in _fileTree)
            {
                EnableAllFileLogSearchResultsRecursive(node);
            }
            
            _logController.SaveFileConfig();
        }

        /// <summary>
        /// 禁用所有文件日志搜索结果
        /// </summary>
        private void DisableAllFileLogSearchResults()
        {
            if (_logController == null || string.IsNullOrEmpty(_searchFilter))
                return;
                
            foreach (var node in _fileTree)
            {
                DisableAllFileLogSearchResultsRecursive(node);
            }
            
            _logController.SaveFileConfig();
        }

        /// <summary>
        /// 递归启用所有文件日志搜索结果
        /// </summary>
        private void EnableAllFileLogSearchResultsRecursive(FileTreeNode node)
        {
            if (ShouldShowNode(node))
            {
                _logController.EnableFilePath(node.Path);
            }
            
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    EnableAllFileLogSearchResultsRecursive(child);
                }
            }
        }

        /// <summary>
        /// 递归禁用所有文件日志搜索结果
        /// </summary>
        private void DisableAllFileLogSearchResultsRecursive(FileTreeNode node)
        {
            if (ShouldShowNode(node))
            {
                _logController.DisableFilePath(node.Path);
            }
            
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    DisableAllFileLogSearchResultsRecursive(child);
                }
            }
        }

        /// <summary>
        /// 加载文件配置值
        /// </summary>
        private void LoadFileConfigValues()
        {
            if (_logController?.FileConfig != null)
            {
                var config = _logController.FileConfig;
                _logRootPath = config.LogRootPath;
                _enableDailyRotation = config.EnableDailyRotation;
                _maxFileSizeMB = config.MaxFileSizeMB;
                _maxFileCount = config.MaxFileCount;
                _writeInfoLogs = config.WriteInfoLogs;
                _writeWarningLogs = config.WriteWarningLogs;
                _writeErrorLogs = config.WriteErrorLogs;
            }
        }

        /// <summary>
         /// 检查是否应该显示节点
         /// </summary>
         private bool ShouldShowNode(FileTreeNode node)
         {
             // 搜索过滤
             if (!string.IsNullOrEmpty(_searchFilter))
             {
                 if (!MatchesSearchFilter(node))
                 {
                     return false;
                 }
             }
             
             // 仅显示已配置的项
             if (_showOnlyConfigured && _logController != null && _logController.FileConfig != null)
             {
                 bool hasConfig = _logController.FileConfig.EnabledPaths.Contains(node.Path) || 
                                _logController.FileConfig.DisabledPaths.Contains(node.Path);
                 if (!hasConfig)
                 {
                     return false;
                 }
             }
             
             return true;
         }

        #endregion

        #region 日志管理页签功能

        /// <summary>
        /// 绘制日志管理工具栏
        /// </summary>
        private void DrawLogManagementToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
            
            if (GUILayout.Button("刷新日志列表", EditorStyles.toolbarButton))
            {
                RefreshLogFilesList();
            }
            
            if (GUILayout.Button("清理所有日志", EditorStyles.toolbarButton))
            {
                ClearAllLogFiles();
            }
            
            if (GUILayout.Button("打开日志目录", EditorStyles.toolbarButton))
            {
                string logRootPath = _logController?.GetLogRootPath() ?? "";
                if (!string.IsNullOrEmpty(logRootPath))
                {
                    OpenConfigFolder(logRootPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "日志目录未配置", "确定");
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制日志文件列表
        /// </summary>
        private void DrawLogFilesList()
        {
            EditorGUILayout.LabelField("日志文件管理", EditorStyles.boldLabel);
            
            string logRootPath = _logController?.GetLogRootPath() ?? "";
            if (string.IsNullOrEmpty(logRootPath))
            {
                EditorGUILayout.HelpBox("日志根目录未配置", MessageType.Warning);
                return;
            }
            
            if (!Directory.Exists(logRootPath))
            {
                EditorGUILayout.HelpBox($"日志目录不存在: {logRootPath}", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.LabelField($"日志目录: {logRootPath}", EditorStyles.miniLabel);
            
            _logFilesScrollPosition = EditorGUILayout.BeginScrollView(_logFilesScrollPosition);
            
            if (_logFiles.Count == 0)
            {
                EditorGUILayout.LabelField("没有找到日志文件", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (string logFile in _logFiles)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    string fileName = Path.GetFileName(logFile);
                    FileInfo fileInfo = new FileInfo(logFile);
                    string fileSize = FormatFileSize(fileInfo.Length);
                    string lastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    EditorGUILayout.LabelField($"{fileName} ({fileSize})", GUILayout.ExpandWidth(true));
                    EditorGUILayout.LabelField(lastModified, GUILayout.Width(150));
                    
                    if (GUILayout.Button("打开", GUILayout.Width(50)))
                    {
                        OpenLogFile(logFile);
                    }
                    
                    if (GUILayout.Button("删除", GUILayout.Width(50)))
                    {
                        DeleteLogFile(logFile);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 刷新日志文件列表
        /// </summary>
        private void RefreshLogFilesList()
        {
            _logFiles.Clear();
            
            string logRootPath = _logController?.GetLogRootPath() ?? "";
            if (string.IsNullOrEmpty(logRootPath) || !Directory.Exists(logRootPath))
                return;
            
            try
            {
                var files = Directory.GetFiles(logRootPath, "*.log", SearchOption.TopDirectoryOnly);
                _logFiles.AddRange(files);
                _logFiles.Sort((x, y) => File.GetLastWriteTime(y).CompareTo(File.GetLastWriteTime(x))); // 按修改时间倒序
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"刷新日志文件列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理所有日志文件
        /// </summary>
        private void ClearAllLogFiles()
        {
            if (EditorUtility.DisplayDialog("确认", "确定要删除所有日志文件吗？此操作不可恢复。", "确定", "取消"))
            {
                string logRootPath = _logController?.GetLogRootPath() ?? "";
                if (string.IsNullOrEmpty(logRootPath) || !Directory.Exists(logRootPath))
                {
                    EditorUtility.DisplayDialog("错误", "日志目录不存在", "确定");
                    return;
                }
                
                try
                {
                    // 先临时关闭当前日志文件
                    _logController?.TemporaryCloseCurrentLogFile();
                    
                    // 等待一小段时间确保文件句柄释放
                    System.Threading.Thread.Sleep(200);
                    
                    var files = Directory.GetFiles(logRootPath, "*.log", SearchOption.TopDirectoryOnly);
                    int deletedCount = 0;
                    
                    foreach (string file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogWarning($"删除日志文件失败: {file}, 错误: {ex.Message}");
                        }
                    }
                    
                    // 重新打开日志文件
                    _logController?.ReopenCurrentLogFile();
                    
                    RefreshLogFilesList();
                    EditorUtility.DisplayDialog("完成", $"已删除 {deletedCount} 个日志文件", "确定");
                }
                catch (Exception ex)
                {
                    // 确保重新打开日志文件
                    _logController?.ReopenCurrentLogFile();
                    EditorUtility.DisplayDialog("错误", $"清理日志文件失败: {ex.Message}", "确定");
                }
            }
        }

        /// <summary>
        /// 打开日志文件
        /// </summary>
        private void OpenLogFile(string filePath)
        {
            try
            {
                System.Diagnostics.Process.Start(filePath);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"无法打开日志文件: {ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 删除日志文件
        /// </summary>
        private void DeleteLogFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            if (EditorUtility.DisplayDialog("确认", $"确定要删除日志文件 '{fileName}' 吗？", "确定", "取消"))
            {
                try
                {
                    // 检查是否是当前正在使用的日志文件
                    string currentLogFile = _logController?.GetCurrentLogFilePath() ?? "";
                    bool isCurrentFile = !string.IsNullOrEmpty(currentLogFile) && 
                                       Path.GetFullPath(filePath).Equals(Path.GetFullPath(currentLogFile), StringComparison.OrdinalIgnoreCase);
                    
                    if (isCurrentFile)
                    {
                        // 如果是当前文件，先临时关闭
                        _logController?.TemporaryCloseCurrentLogFile();
                        
                        // 等待一小段时间确保文件句柄释放
                        System.Threading.Thread.Sleep(100);
                    }
                    
                    // 删除文件
                    File.Delete(filePath);
                    
                    if (isCurrentFile)
                    {
                        // 重新打开日志文件（会创建新文件）
                        _logController?.ReopenCurrentLogFile();
                    }
                    
                    RefreshLogFilesList();
                    EditorUtility.DisplayDialog("完成", "日志文件已删除", "确定");
                }
                catch (Exception ex)
                {
                    // 如果删除失败，确保重新打开日志文件
                    string currentLogFile = _logController?.GetCurrentLogFilePath() ?? "";
                    bool isCurrentFile = !string.IsNullOrEmpty(currentLogFile) && 
                                       Path.GetFullPath(filePath).Equals(Path.GetFullPath(currentLogFile), StringComparison.OrdinalIgnoreCase);
                    
                    if (isCurrentFile)
                    {
                        _logController?.ReopenCurrentLogFile();
                    }
                    
                    EditorUtility.DisplayDialog("错误", $"删除日志文件失败: {ex.Message}", "确定");
                }
            }
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        #endregion

        private class FileTreeNode
        {
            public string Name;
            public string Path;
            public bool IsDirectory;
            public List<FileTreeNode> Children;
        }
    }
}
