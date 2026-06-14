namespace DevelopersHub.ClashOfWhatecer
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 编辑器数据迁移工具：一键执行 Excel -> JSON -> ScriptableObject 全流程。
    /// 菜单执行后会先调用 Tools/export_config.py，再将 JSON 写入对应 SO 资产。
    /// 菜单路径：Tools → Config → Generate SO Assets from Data.cs
    /// 输出目录：Assets/Resources/Config/
    /// </summary>
    public static class DataConfigMigration
    {
        private const string OutputFolder = "Assets/Resources/Config";
        private const string JsonFolder   = "Assets/Config/Json";

        [MenuItem("Tools/Config/Generate SO Assets from Data.cs")]
        public static void GenerateAll()
        {
            if (!RunPythonExport())
                return;

            if (!Directory.Exists(JsonFolder))
            {
                Debug.LogError("[DataConfigMigration] JSON 目录不存在: " + JsonFolder);
                return;
            }

            EnsureFolder(OutputFolder);

            GenerateGameplaySettings();
            GenerateClanSettings();
            GenerateBuildingAvailability();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DataConfigMigration] 全部 SO 资产已从 " + JsonFolder + " 生成至 " + OutputFolder);
        }

        /// <summary>
        /// 在项目根目录执行 Python 导出脚本，自动生成 JSON 配置文件。
        /// 优先尝试 python，再回退到 py -3。
        /// </summary>
        private static bool RunPythonExport()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string scriptPath  = Path.Combine(projectRoot, "Tools", "export_config.py");

            if (!File.Exists(scriptPath))
            {
                Debug.LogError("[DataConfigMigration] 找不到导出脚本: " + scriptPath);
                return false;
            }

            bool pythonOk = TryRunProcess(
                "python",
                "\"" + scriptPath + "\"",
                projectRoot,
                out int pythonExitCode,
                out string pythonStdOut,
                out string pythonStdErr);

            if (!pythonOk)
            {
                bool pyLauncherOk = TryRunProcess(
                    "py",
                    "-3 \"" + scriptPath + "\"",
                    projectRoot,
                    out int pyExitCode,
                    out string pyStdOut,
                    out string pyStdErr);

                if (!pyLauncherOk)
                {
                    Debug.LogError(
                        "[DataConfigMigration] 执行 Python 导出失败。\n" +
                        "python 退出码: " + pythonExitCode + "\n" +
                        "python stdout:\n" + pythonStdOut + "\n" +
                        "python stderr:\n" + pythonStdErr + "\n" +
                        "py 退出码: " + pyExitCode + "\n" +
                        "py stdout:\n" + pyStdOut + "\n" +
                        "py stderr:\n" + pyStdErr + "\n" +
                        "请确认已安装 Python，并可在命令行执行 python 或 py。"
                    );
                    return false;
                }

                Debug.Log("[DataConfigMigration] Python 导出完成（py -3）。\n" + pyStdOut);
                return true;
            }

            Debug.Log("[DataConfigMigration] Python 导出完成（python）。\n" + pythonStdOut);
            return true;
        }

        /// <summary>
        /// 执行外部进程并捕获输出；退出码为 0 返回 true。
        /// </summary>
        private static bool TryRunProcess(
            string fileName,
            string arguments,
            string workingDirectory,
            out int exitCode,
            out string standardOutput,
            out string standardError)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (System.Diagnostics.Process process = new System.Diagnostics.Process { StartInfo = startInfo })
                {
                    process.Start();
                    standardOutput = process.StandardOutput.ReadToEnd();
                    standardError = process.StandardError.ReadToEnd();

                    bool exited = process.WaitForExit(120000);
                    if (!exited)
                    {
                        process.Kill();
                        exitCode = -2;
                        standardError = standardError + Environment.NewLine + "Process timeout after 120000ms.";
                        return false;
                    }

                    exitCode = process.ExitCode;
                    return exitCode == 0;
                }
            }
            catch (Exception ex)
            {
                exitCode = -1;
                standardOutput = string.Empty;
                standardError = ex.Message;
                return false;
            }
        }

        // ── 游戏基础设置 ──────────────────────────────────────────────────────
        /// <summary>
        /// 读取 GameplaySettings.json，通过 JsonUtility 将字段直接覆写到 SO。
        /// JSON 的 key 与 GameplaySettingsSO 的字段名一一对应，无需中间映射。
        /// </summary>
        private static void GenerateGameplaySettings()
        {
            string jsonPath  = JsonFolder + "/GameplaySettings.json";
            string assetPath = OutputFolder + "/GameplaySettings.asset";
            if (!File.Exists(jsonPath)) { Debug.LogWarning("[DataConfigMigration] 文件不存在: " + jsonPath); return; }

            GameplaySettingsSO so = LoadOrCreate<GameplaySettingsSO>(assetPath);
            JsonUtility.FromJsonOverwrite(File.ReadAllText(jsonPath), so);
            EditorUtility.SetDirty(so);
            Debug.Log("[DataConfigMigration] GameplaySettings 写入: " + assetPath);
        }

        // ── 部落设置 ──────────────────────────────────────────────────────────
        /// <summary>
        /// 读取 ClanSettings.json，通过 JsonUtility 将字段直接覆写到 SO。
        /// 数组字段（如 clanWarAvailableCounts）在 JSON 中已是原生数组，无需额外解析。
        /// </summary>
        private static void GenerateClanSettings()
        {
            string jsonPath  = JsonFolder + "/ClanSettings.json";
            string assetPath = OutputFolder + "/ClanSettings.asset";
            if (!File.Exists(jsonPath)) { Debug.LogWarning("[DataConfigMigration] 文件不存在: " + jsonPath); return; }

            ClanSettingsSO so = LoadOrCreate<ClanSettingsSO>(assetPath);
            JsonUtility.FromJsonOverwrite(File.ReadAllText(jsonPath), so);
            EditorUtility.SetDirty(so);
            Debug.Log("[DataConfigMigration] ClanSettings 写入: " + assetPath);
        }

        // ── 建筑解锁配置 ───────────────────────────────────────────────────────
        /// <summary>
        /// 读取 BuildingAvailability.json，通过 JsonUtility 将嵌套列表直接覆写到 SO。
        /// JSON 顶层 key 为 "townHallConfigs"，与 BuildingAvailabilitySO.townHallConfigs 字段名一致。
        /// </summary>
        private static void GenerateBuildingAvailability()
        {
            string jsonPath  = JsonFolder + "/BuildingAvailability.json";
            string assetPath = OutputFolder + "/BuildingAvailability.asset";
            if (!File.Exists(jsonPath)) { Debug.LogWarning("[DataConfigMigration] 文件不存在: " + jsonPath); return; }

            BuildingAvailabilitySO so = LoadOrCreate<BuildingAvailabilitySO>(assetPath);
            JsonUtility.FromJsonOverwrite(File.ReadAllText(jsonPath), so);
            EditorUtility.SetDirty(so);
            Debug.Log("[DataConfigMigration] BuildingAvailability 写入: " + assetPath);
        }

        // ── 资产辅助方法 ───────────────────────────────────────────────────────
        /// <summary>加载已存在的 SO 资产；若不存在则新建并注册到 <paramref name="assetPath"/>。</summary>
        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;
            T so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, assetPath);
            return so;
        }

        /// <summary>确保 <paramref name="path"/> 对应的 Unity 文件夹存在；不存在则递归创建。</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}