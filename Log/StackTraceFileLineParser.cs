// ============================================================
//  StackTraceFileLineParser.cs
//  创建: chao.liang   2025年12月26日  17:12
//  说明: 解析调用栈中的文件行信息
// ============================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
#if !UNITY_EDITOR && UNITY_WEBGL
using File = Framework.WebGLIO.WebGLFile;
using Path = Framework.WebGLIO.WebGLPath;
#endif

namespace Framework.Log
{
    public static class StackTraceFileLineParser
    {
        // Unity: (at Assets/.../Foo.cs:100) or (at Assets/.../Foo.cs:100:12)
        private static readonly Regex UnityAtRegex = new(
            @"\(\s*at\s+(?<path>[^:)]+):(?<line>\d+)(?::\d+)?\s*\)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // C#: ... in D:\...\Foo.cs:58 or ... in D:\...\Foo.cs:58:12
        // 关键点：Windows 盘符里也有 ':'，用贪婪匹配让它吃到最后一个冒号之前
        private static readonly Regex CsInSameLineRegex = new(
            @"\bin\s+(?<path>.+):(?<line>\d+)(?::\d+)?\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // C# (换行): ... in \n   D:\...\Bar.cs:61 or :61:12
        private static readonly Regex CsInNextLineRegex = new(
            @"\bin\s*\r?\n\s*(?<path>.+):(?<line>\d+)(?::\d+)?\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// 解析“单行/单段”堆栈文本，成功则返回 true，并输出 文件名 + 行号
        /// </summary>
        public static bool TryParseFileAndLine(string text, out string path, out string fileName, out int lineNumber)
        {
            fileName = string.Empty;
            path = string.Empty;
            lineNumber = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match m = UnityAtRegex.Match(text);
            if (!m.Success) m = CsInNextLineRegex.Match(text);
            if (!m.Success) m = CsInSameLineRegex.Match(text);
            if (!m.Success) return false;

            path = m.Groups["path"].Value.Trim();
            var lineStr = m.Groups["line"].Value;

            if (!int.TryParse(lineStr, out lineNumber))
                return false;
            if (!File.Exists(path))
                return false;

            fileName = Path.GetFileName(path);
            return !string.IsNullOrEmpty(fileName);
        }

        /// <summary>
        /// 解析整个堆栈，提取所有出现的 文件名 + 行号
        /// </summary>
        public static List<(string fileName, int lineNumber)> ExtractAllFileAndLines(string stackTrace)
        {
            var results = new List<(string, int)>();
            if (string.IsNullOrWhiteSpace(stackTrace))
                return results;

            // 先抓“in 换行路径”的（它跨行，适合对整段做匹配）
            foreach (Match m in CsInNextLineRegex.Matches(stackTrace))
                Add(results, m);

            // 再逐行抓 “(at ...)” 和 “in 同行路径”
            using var sr = new StringReader(stackTrace);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var m1 = UnityAtRegex.Match(line);
                if (m1.Success) Add(results, m1);

                var m2 = CsInSameLineRegex.Match(line);
                if (m2.Success) Add(results, m2);
            }

            return results;
        }

        private static void Add(List<(string fileName, int lineNumber)> list, Match m)
        {
            var path = m.Groups["path"].Value.Trim();
            var lineStr = m.Groups["line"].Value.Trim();

            if (int.TryParse(lineStr, out var ln))
            {
                var fn = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(fn))
                    list.Add((fn, ln));
            }
        }
    }
}