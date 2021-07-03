using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace InitializeEnvironment
{
    public class AddGrubEntryStage : IStage
    {
        public string StageIdentifier => "add-grub-entry";
        public Logger Log = LogManager.GetCurrentClassLogger();

        public AddGrubEntryStage()
        {
        }

        public bool Execute()
        {
            if(!File.Exists("grub_entry.txt"))
            {
                Log.Error("Missing GRUB entry template (grub_entry.txt). GRUB won't be configured.");
                return false;
            }

/*          if(string.IsNullOrWhiteSpace(Program.AuxiliaryPartitionUuid))
            {
                Log.Error("Couldn't find partition UUID. GRUB won't be configured.");
                return false;
            }*/

            var template = File.ReadAllText("grub_entry.txt").Replace("\r\n", "\n");
            
            var template_lines = template.Split('\n');

            if (!File.Exists(Program.InitrdPath)) {
                template_lines = template_lines.Where(line => !line.StartsWith("initrd")).ToArray();
                template = string.Join('\n', template_lines);
                template = template.Replace("root=UUID={uuid}", "root={root-dev}");
            }

            if (!File.Exists(Program.InitrdPath) || string.IsNullOrWhiteSpace(Program.AuxiliaryPartitionUuid))
                template = template.Replace("root=UUID={uuid}", "root={root-dev}");

            template = template.Replace("{linux_img}", Program.VmlinuzPath);
            template = template.Replace("{initrd_img}", Program.InitrdPath);
            template = template.Replace("{uuid}", Program.AuxiliaryPartitionUuid);
            template = template.Replace("{root-dev}", Program.AuxiliaryPartitionPath);
            template = template.Replace("{init}", "/bin/init");

            File.WriteAllText("/etc/grub.d/06_sharpsuite", template);
            Utilities.MakeExecutable("/etc/grub.d/06_sharpsuite");

            Log.Info("Created GRUB entry. Attempting to update GRUB...");
            Log.Info("GRUB SAYS:");
            Log.Info(new string('-', 50));

            var grub_command = "update-grub";
            var grub_command_args = "";

            // crude
            if (!File.Exists(grub_command)) 
            {
                grub_command = "grub2-mkconfig";
                grub_command_args = "-o /boot/grub2/grub.cfg";
            }

            var result = Utilities.RunCommand(grub_command, grub_command_args);

            Log.Info(new string('-', 50));

            Log.Info("update-grub returned: {0}", result);
            Log.Info("If there's an error in the above message, you might have a different alias " +
                "for update-grub, or we might have just borked your GRUB setup. The only file " +
                "InitializeEnvironment touches is /etc/grub.d/06_sharpsuite, so try removing it" +
                " and running update-grub again if GRUB keeps failing.");

            return true;
        }
    }
}
