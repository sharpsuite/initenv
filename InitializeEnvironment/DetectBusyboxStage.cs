using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace InitializeEnvironment
{
    public class DetectBusyboxStage : IStage
    {
        public string StageIdentifier => "detect-busybox";
        public Logger Log = LogManager.GetCurrentClassLogger();

        public DetectBusyboxStage()
        {

        }

        public bool Execute()
        {
            if (!File.Exists(Program.BusyboxPath) && File.Exists("busybox"))
                Program.BusyboxPath = Path.GetFullPath("./busybox");
        
            if (File.Exists(Program.BusyboxPath))
            {
                if (Utilities.RunCommand(Program.BusyboxPath, "--help").StartsWith("BusyBox"))
                {
                    Program.BusyboxPath = Path.GetFullPath(Program.BusyboxPath);
                    return true;
                }
                else if (Program.AvoidPrompts)
                    return false;
                else if (Utilities.Prompt("The busybox file you supplied exists, but it doesn't look" +
                    "like a valid busybox binary. Continue anyway?"))
                    return true;
            }

            if (Program.AvoidPrompts || Utilities.Prompt("Busybox file either nonexistent or invalid. Download from busybox.org?"))
            {
                if (Utilities.DownloadFileWithProgress("https://busybox.net/downloads/binaries/1.30.0-i686/busybox", "./busybox"))
                {
                    Utilities.MakeExecutable("./busybox");
                    Program.BusyboxPath = Path.GetFullPath("./busybox");
                    return true;
                }
                else
                {
                    Log.Error("Couldn't download busybox. Try supplying the path of a known-good busybox binary using the --busybox argument.");
                    return false;
                }
            }
            else
                return false;
        }
    }
}
