using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InitializeEnvironment
{
    class DetectDotnetDependenciesStage : IStage
    {
        public string StageIdentifier => "detect-dotnet-deps";
        private Logger Log = LogManager.GetCurrentClassLogger();

        public DetectDotnetDependenciesStage()
        {

        }

        public bool Execute()
        {
            var ldd_path = Utilities.Which("ldd");

            if(!File.Exists(ldd_path))
            {
                Log.Error("Couldn't find ldd. Please install ldd before continuing.");
                return false;
            }

            var ldd_output = Utilities.RunCommand(ldd_path, "{0}", Program.DotnetPath).Split('\n').Select(l => l.Trim());

            if(ldd_output.First().Equals("not a dynamic executable"))
            {
                Log.Error("Something went wrong, ldd output:");
                Log.Error(ldd_output);

                return false;
            }

            foreach(var line in ldd_output)
            {
                var path = line;

                if (path.Contains("=>"))
                    path = path.Split(new[] { "=>" }, StringSplitOptions.None)[1].Trim();

                if (path.Contains(" "))
                    path = path.Split(' ')[0];

                if (!File.Exists(path))
                {
                    Log.Warn("Encountered dotnet dependency that couldn't be located: {0}, our best guess is \"{1}\" (which doesn't exist)", line, path);
                    continue;
                }

                Log.Debug("Added dotnet dependency: {0}", path);
                Program.DotnetDependencies.Add(path);
            }

            // these are dynamically loaded
            var compat_libs = new[] { "librt.", "libssl.", "libcrypto.", "libz.", "libm.", "libdl." };

            foreach (var lib in compat_libs)
            {
                var path = Utilities.FindLibrary(lib);

                if (File.Exists(path ?? ""))
                {
                    Program.DotnetDependencies.Add(path);
                    Log.Debug("Added compat lib: {0}", path);
                }
                else
                    Log.Warn("Couldn't find lib with search hint \"{0}\"!", lib);
            }

            return true;
        }
    }
}
