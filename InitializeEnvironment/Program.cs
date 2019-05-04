using System;
using System.Collections.Generic;
using System.IO;
using NDesk.Options;

using NLog;
using NLog.Config;
using NLog.Targets;

namespace InitializeEnvironment
{
    class Program
    {
        // excuse the globals

        public static string DotnetPath { get; set; }
        public static List<string> DotnetDependencies = new List<string>();

        public static string AuxiliaryPartitionPath { get; set; }
        public static string AuxiliaryPartitionUuid { get; set; }

        public static string GrubConfigurationPath { get; set; }

        public static string VmlinuzPath { get; set; }
        public static string InitrdPath { get; set; }

        public static string BusyboxPath { get; set; }

        public static bool UseDownloadedDotnet = false;

        private static Logger Log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            var log_level = LogLevel.Info;
        
            OptionSet set = null;
            set = new OptionSet()
            {
                {"a|aux|auxiliary-partition=", "Sets the auxiliary partition device name. This is not the mount point, it must be the block device itself!", path => AuxiliaryPartitionPath = path},
                {"b|busybox=", "Sets the busybox binary path.", b => BusyboxPath = Path.GetFullPath(b) },
                {"use-downloaded-dotnet", "Do not prompt to ask if you want to reuse the downloaded dotnet package.", d => UseDownloadedDotnet = true },
                {"h|help", "Displays help.", h => DisplayHelp(set) },
                {"v|verbosity=", "Sets the output verbosity level.", v => { foreach (var rule in LogManager.Configuration?.LoggingRules) { rule.EnableLoggingForLevel((log_level = LogLevel.FromString(v))); } } },
            };

            if (LogManager.Configuration == null)
            {
                var config = new LoggingConfiguration();
                var console = new ConsoleTarget("console") { Layout = "${date} [${uppercase:${level}}] ${message}"};
                config.AddTarget(console);
                config.AddRule(log_level, LogLevel.Fatal, console);
                LogManager.Configuration = config;
            }

            var left = set.Parse(args);

            var stages = new List<IStage>()
            {
                new DetectOperatingSystemStage(),
                new DetectDotnetStage(),
                new DetectDotnetDependenciesStage(),
                new DetectBusyboxStage(),
                new PrepareAuxiliaryPartitionStage(),
                new AddGrubEntryStage(),
                new UnmountAuxiliaryPartitionStage()
            };

            foreach(var stage in stages)
            {
                Log.Info("{0} starting...", stage.StageIdentifier);

                var result = stage.Execute();

                if (!result)
                {
                    Log.Error("Stage {0} failed, exiting.", stage.StageIdentifier);
                    break;
                }

                Log.Info("{0} completed successfully.", stage.StageIdentifier);
            }

            Log.Info("InitializeEnvironment done.");
        }

        static void DisplayHelp(OptionSet set)
        {
            Console.WriteLine("InitializeEnvironment -- a script to set up a minimal linux + dotnet environment.");
            Console.WriteLine();
            set.WriteOptionDescriptions(Console.Out);

            Environment.Exit(0);
        }
    }
}
