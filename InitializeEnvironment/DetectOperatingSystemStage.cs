using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace InitializeEnvironment
{
    public class DetectOperatingSystemStage : IStage
    {
        public string StageIdentifier => "detect-os";
        private Logger Log = LogManager.GetCurrentClassLogger();

        public bool Execute()
        {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Log.Error("The InitializeEnvironment script currently only supports Linux.");
                return false;
            }

            if(!Directory.Exists("/boot"))
            {
                Log.Error("Couldn't find /boot. What kind of environment is this? Mount /boot and try again.");
                return false;
            }

            var cmdline = File.ReadAllText("/proc/cmdline");
            var image_path = cmdline.Split(' ').First(p => p.StartsWith("BOOT_IMAGE")).Split('=')[1];

            if (!File.Exists(image_path))
                image_path = "/boot" + image_path;

            if(!File.Exists(image_path))
            {
                Log.Error("Image path found in /proc/cmdline, but not in filesystem. Is /boot properly mounted?");
                return false;
            }

            Log.Debug("Found boot image at {0}", image_path);
            Program.VmlinuzPath = image_path;

            var dashed_descriptor = string.Join("-", Path.GetFileName(image_path).Split('-').Skip(1));

            Log.Debug("Dashed descriptor is {0}", dashed_descriptor);

            var dir = Path.GetDirectoryName(image_path);
            var files = Directory.GetFiles(dir);

            var common_exts = new[] { "", ".img" };

            foreach(var file in files)
            {
                if(Path.GetFileName(file).StartsWith("init") && common_exts.Any(ext => file.EndsWith(dashed_descriptor + ext))) // loose heuristic
                {
                    Log.Debug("Found initrd at {0}", file);

                    Program.InitrdPath = file;
                    break;
                }
            }

            if(!File.Exists(Program.InitrdPath))
            {
                Log.Error("Couldn't find your initrd file.");
                return false;
            }

            return true;
        }
    }
}
