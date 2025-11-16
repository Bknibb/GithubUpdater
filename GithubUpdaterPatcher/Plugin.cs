using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GithubUpdater
{
    public static class Patcher
    {
        public static string githubUpdaterExe = Path.Combine(Paths.PatcherPluginPath, "GithubUpdater\\GithubUpdater.exe");
        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                RunPatcher();
                yield return string.Empty;
            }
        }

        public static void Patch(AssemblyDefinition assembly)
        {

        }
        private static void UpdatePlugin(AssemblyDefinition assembly)
        {
            var githubAttribute = assembly.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == "GithubAttribute");
            if (githubAttribute == null) return;
            string owner = (string)githubAttribute.ConstructorArguments[0].Value;
            string repo = (string)githubAttribute.ConstructorArguments[1].Value;

            Console.WriteLine($"[Updater] Checking {assembly.Name.Name} for updates, currently version {assembly.Name.Version}");

            var startInfo = new ProcessStartInfo
            {
                FileName = githubUpdaterExe,
                Arguments = BuildArguments([owner, repo, assembly.Name.Version.ToString(), Paths.PluginPath]),               // Add command line arguments if needed
                UseShellExecute = false,     // Required to redirect output
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,       // Don't show console window
            };
            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Console.Error.WriteLine($"[Updater] Failed to start updater process for {assembly.Name.Name}.");
                        return;
                    }

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Console.WriteLine($"[Updater] {assembly.Name.Name}: " + e.Data);
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Console.Error.WriteLine($"[Updater] {assembly.Name.Name} Error: " + e.Data);
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Updater] {assembly.Name.Name} Exception while running updater: " + ex);
            }
        }
        public static string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\""; // empty argument is two quotes

            // If no spaces or quotes, no need to quote
            if (!arg.Contains(" ") && !arg.Contains("\""))
                return arg;

            // Escape embedded quotes by doubling them
            string escaped = arg.Replace("\"", "\"\"");

            // Wrap with quotes
            return $"\"{escaped}\"";
        }
        public static string BuildArguments(IEnumerable<string> args)
        {
            return string.Join(" ", args.Select(EscapeArgument).ToArray());
        }
        private static void RunPatcher()
        {
            Console.WriteLine("[Updater] Checking plugins for updates");
            foreach (var file in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);

                try
                {
                    using (var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(file))))
                    {
                        UpdatePlugin(assembly);

                        assembly.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Updater] Failed to check {name} for updates, exception: {e.Message}\n{e.StackTrace}");
                }
            }
            Console.WriteLine("[Updater] Finished checking plugins for updates");
        }
    }
}
