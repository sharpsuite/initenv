using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InitializeEnvironment
{
    public class PrepareAuxiliaryPartitionStage : IStage
    {
        public string StageIdentifier => "prepare-aux-partition";
        private Logger Log = LogManager.GetCurrentClassLogger();

        internal static bool mounted_part = false;
        internal static string temp_mount_point = "";

        public PrepareAuxiliaryPartitionStage()
        {

        }

        public bool Execute()
        {
            var partition_name = Program.AuxiliaryPartitionPath;

            if(!File.Exists(partition_name))
            {
                Log.Error("Invalid partition name \"{0}\".", partition_name);
                return false;
            }

            Log.Debug("Checking if {0} is already mounted...", partition_name);

            var mount_list_output = Utilities.RunCommand("mount", "").Split('\n');
            var mount_point = "";

            if(!mount_list_output.Any(l => l.StartsWith(partition_name)))
            {
                Log.Info("Mounting {0}...", partition_name);

                var temp_location = "/tmp/" + Path.GetRandomFileName();

                Directory.CreateDirectory(temp_location);

                var mount_output = Utilities.RunCommand("mount", "{0} {1}", partition_name, temp_location);

                // TODO: don't assume that this succeeded

                if(!Directory.Exists(temp_location))
                {
                    Log.Error("Error while mounting {0}, this is what mount told us: {1}", partition_name, mount_output);
                    return false;
                }

                mount_point = temp_location;
                temp_mount_point = mount_point;
                mounted_part = true;
            }
            else
            {
                mount_point = mount_list_output.First(l => l.StartsWith(partition_name)).Split(new[] { "on", "type" }, StringSplitOptions.None)[1].Trim();
            }

            Log.Info("{0} is mounted on {1}", partition_name, mount_point);
            
            var uuid = Utilities.RunCommand("lsblk", "-no uuid {0}", partition_name).Trim();

            if (string.IsNullOrWhiteSpace(uuid) || !Guid.TryParse(uuid, out Guid garbage))
            {
                Log.Warn("Couldn't obtain the UUID of the device {0}.", partition_name);
                Log.Warn("InitializeEnvironment will be unable to perform GRUB setup.");
            }
            else
                Program.AuxiliaryPartitionUuid = uuid;

            if (Directory.GetDirectories(mount_point).Any())
            {
                var prompt_result = Utilities.Prompt(string.Format("{0} already contains some directories, do you want to continue? DO NOT SAY YES IF YOU HAVE ANY DATA ON THE AUXILIARY PARTITION.", mount_point));

                if (!prompt_result)
                    return false;

                if (!Program.NukeAuxiliaryPartition)
                    Program.NukeAuxiliaryPartition = Utilities.Prompt($"Wipe data on {mount_point}?");

                if (Program.NukeAuxiliaryPartition)
                    Utilities.RunCommand("rm", $"-rf \"{mount_point}\"");
            }

            var base_dir_names = new[] { "/boot", "/bin", "/sbin", "/dev", "/proc", "/sys", "/run", "/tmp", "/root", "/root/sys", "/lib", "/lib64", "/etc", "/var", "/usr", "/usr/lib" };

            foreach (var dir in base_dir_names)
            {
                Directory.CreateDirectory(Path.Combine(mount_point, dir.Substring(1)));
                Log.Debug("Created {0}", Path.Combine(mount_point, dir.Substring(1)));
            }

            Log.Info("Created filesystem layout.");
            Log.Info("Copying images...");

            File.Copy(Program.VmlinuzPath, Path.Combine(mount_point, Program.VmlinuzPath.Substring(1)), true);
            File.Copy(Program.InitrdPath, Path.Combine(mount_point, Program.InitrdPath.Substring(1)), true);

            Log.Info("Copying dotnet dependencies...");

            foreach(var dep in Program.DotnetDependencies)
            {
                var target = Path.Combine(mount_point, dep.Substring(1));
                var dir = Path.GetDirectoryName(target);

                if(!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                Log.Debug("{0} -> {1}", dep, target);
                File.Copy(dep, target, true);

                var potential_usr_lib_symlink = target.Replace("/lib/", "/usr/lib/");
                var potential_lib_symlink = target.Replace("/usr/lib/", "/lib/");
                dir = dir.Substring(mount_point.Length);

                if (!dir.StartsWith("/usr/lib") && !File.Exists(potential_usr_lib_symlink))
                {
                    Log.Debug("{0} -> {1}", target, potential_usr_lib_symlink);
                    if (!Directory.Exists(Path.GetDirectoryName(potential_usr_lib_symlink)))
                        Directory.CreateDirectory(Path.GetDirectoryName(potential_usr_lib_symlink));
                    Utilities.RunCommand("ln", "{0} {1}", target, potential_usr_lib_symlink);
                }
                else if (!dir.StartsWith("/lib") && !File.Exists(potential_lib_symlink))
                {
                    Log.Debug("{0} -> {1}", target, potential_lib_symlink);
                    if (!Directory.Exists(Path.GetDirectoryName(potential_lib_symlink)))
                        Directory.CreateDirectory(Path.GetDirectoryName(potential_lib_symlink));
                    Utilities.RunCommand("ln", "{0} {1}", target, potential_lib_symlink);
                }
            }

            Log.Info("Copying dotnet itself...");

            var dotnet_parent_dir = Path.GetDirectoryName(Program.DotnetPath);
            Utilities.RunCommand("cp", "-rf {0} {1}", dotnet_parent_dir, Path.Combine(mount_point, "dotnet"));
            Utilities.MakeExecutable(Path.Combine(mount_point, "dotnet/dotnet"));

            Log.Info("Creating dotnet symlinks...");

            Utilities.RunCommand("ln", "{0} {1}", Path.Combine(mount_point, "dotnet/dotnet"), Path.Combine(mount_point, "bin/dotnet"));
            Utilities.RunCommand("ln", "-s {0} {1}", "/dotnet/host", Path.Combine(mount_point, "bin/host"));
            Utilities.RunCommand("ln", "-s {0} {1}", "/dotnet/shared", Path.Combine(mount_point, "bin/shared"));
            //Utilities.RunCommand("ln", "{0} {1}", Path.Combine(mount_point, "/dotnet/dotnet-thunk.sh"), Path.Combine(mount_point, "/bin/dotnet-thunk"));

            Log.Info("Copying busybox...");

            File.Copy(Program.BusyboxPath, Path.Combine(mount_point, "bin/busybox"), true);
            File.Copy(Program.BusyboxPath, Path.Combine(mount_point, "sbin/busybox"), true);

            Utilities.MakeExecutable(Path.Combine(mount_point, "bin/busybox"));
            Utilities.MakeExecutable(Path.Combine(mount_point, "sbin/busybox"));

            Log.Info("Adding busybox links...");

            var busybox_commands = Utilities.RunCommand(Program.BusyboxPath, "--list").Split('\n').Select(c => c.Trim());

            foreach(var command in busybox_commands)
            {
                Utilities.RunCommand("ln", "{0} {1}", Path.Combine(mount_point, "bin/busybox"), Path.Combine(mount_point, "bin/" + command));
                Utilities.RunCommand("ln", "{0} {1}", Path.Combine(mount_point, "sbin/busybox"), Path.Combine(mount_point, "sbin/" + command));
            }

            if (File.Exists("dotnet-init.sh"))
            {
                Log.Info("Copying init script...");

                File.WriteAllText(Path.Combine(mount_point, "dotnet/dotnet-init"), File.ReadAllText("./dotnet-init.sh").Replace("\r", ""));
                Utilities.MakeExecutable(Path.Combine(mount_point, "dotnet/dotnet-init"));
            }

            if (Directory.Exists("./init-files"))
            {
                Log.Info("Copying init files...");
                Utilities.RunCommand("cp", "-r ./init-files {0}", Path.Combine(mount_point, "dotnet"));
            }

            return true;
        }
    }
}
