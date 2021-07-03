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
            var cwd = Environment.CurrentDirectory;
            var partition_name = Program.AuxiliaryPartitionPath;
            partition_name = Path.GetFullPath(partition_name);

            var aux_part_entry = Mono.Unix.UnixFileSystemInfo.GetFileSystemEntry(partition_name);
            var aux_path = partition_name;

            if (aux_part_entry.IsBlockDevice)
            {
                Program.Stages.Add(new AddGrubEntryStage());

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
                    Program.Stages.Add(new UnmountAuxiliaryPartitionStage());
                }
                else
                {
                    mount_point = mount_list_output.First(l => l.StartsWith(partition_name)).Split(new[] { "on", "type" }, StringSplitOptions.None)[1].Trim();
                }

                Log.Info("{0} is mounted on {1}", partition_name, mount_point);
                aux_path = mount_point;
                
                var uuid = Utilities.RunCommand("lsblk", "-no uuid {0}", partition_name).Trim();

                if (string.IsNullOrWhiteSpace(uuid) || !Guid.TryParse(uuid, out Guid garbage))
                {
                    Log.Warn("Couldn't obtain the UUID of the device {0}.", partition_name);
                }
                else
                    Program.AuxiliaryPartitionUuid = uuid;
            }
            else if (aux_part_entry.IsDirectory)
            {
                Log.Warn($"{partition_name} is a directory. GRUB won't be configured.");
                aux_path = partition_name;
            }
            else
            {
                Log.Error($"{partition_name} is neither a block device nor a directory.");
                return false;
            }

            Environment.CurrentDirectory = aux_path;

            if (Directory.GetDirectories(aux_path).Any() && !Program.AcceptDirtyAux)
            {
                if (!Program.NukeAuxiliaryPartition)
                {
                    if (Program.AvoidPrompts)
                        return false;

                    var prompt_result = Utilities.Prompt(string.Format("{0} already contains some directories, do you want to continue? DO NOT SAY YES IF YOU HAVE ANY DATA ON THE AUXILIARY PARTITION.", aux_path));

                    if (!prompt_result)
                        return false;

                    Program.NukeAuxiliaryPartition = Utilities.Prompt($"Wipe all data on {aux_path}?");
                }

                if (Program.NukeAuxiliaryPartition)
                {
                    foreach (var dir in Directory.GetDirectories(aux_path))
                    {
                        var abs_path = Path.GetFullPath(dir, aux_path);
                        Log.Warn($"Removing {abs_path}...");
                        Utilities.RunCommand("rm", $"-rf \"{abs_path}\"");
                    }
                }
            }

            var base_dir_names = new[] { "boot", "dev", "proc", "sys", "run", "tmp", "root", "root/sys", "etc", "usr" };
            var usr_dir_names = new [] { "bin", "sbin", "lib", "lib64", "libexec" };

            foreach (var dir in base_dir_names)
            {
                Directory.CreateDirectory(Utilities.CombinePath(aux_path, dir));
                Log.Debug($"Created /{dir} on {Utilities.CombinePath(aux_path, dir)}");
            }

            foreach (var dir in usr_dir_names)
            {
                Directory.CreateDirectory(Utilities.CombinePath(aux_path, "usr", dir));
                Log.Debug($"Created /usr/{dir} on {Utilities.CombinePath(aux_path, dir)}");
                Utilities.Link($"usr/{dir}", dir);
            }

            Log.Info("Created filesystem layout.");
            Log.Info("Copying images...");

            File.Copy(Program.VmlinuzPath, Utilities.CombinePath(aux_path, Program.VmlinuzPath.Substring(1)), true);

            if (File.Exists(Program.InitrdPath))
                File.Copy(Program.InitrdPath, Utilities.CombinePath(aux_path, Program.InitrdPath.Substring(1)), true);

            Log.Info("Copying dotnet dependencies...");

            foreach(var dep in Program.DotnetDependencies)
            {
                var target = Utilities.CombinePath(aux_path, "usr", dep);
                var dir = Path.GetDirectoryName(target);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                Log.Debug("{0} -> {1}", dep, target);
                File.Copy(dep, target, true);

                /*
                var potential_usr_lib_symlink = target.Replace("/lib/", "/usr/lib/");
                var potential_lib_symlink = target.Replace("/usr/lib/", "/lib/");
                dir = dir.Substring(aux_path.Length);

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
                }*/
            }

            Log.Info("Copying dotnet itself...");

            var dotnet_parent_dir = Path.GetDirectoryName(Program.DotnetPath);
            Utilities.RunCommand("cp", "-rf {0} {1}", dotnet_parent_dir, Utilities.CombinePath(aux_path, "dotnet"));
            Utilities.MakeExecutable(Utilities.CombinePath(aux_path, "dotnet/dotnet"));

            Log.Info("Creating dotnet symlinks...");

            Utilities.Link("/dotnet/dotnet", Utilities.CombinePath(aux_path, "usr/bin/dotnet"));
            Utilities.Link("/dotnet/host", Utilities.CombinePath(aux_path, "usr/bin/host"));
            Utilities.Link("/dotnet/shared", Utilities.CombinePath(aux_path, "usr/bin/shared"));
            //Utilities.RunCommand("ln", "{0} {1}", Utilities.CombinePath(aux_path, "/dotnet/dotnet-thunk.sh"), Utilities.CombinePath(aux_path, "/bin/dotnet-thunk"));

            Log.Info("Copying busybox...");

            File.Copy(Program.BusyboxPath, Utilities.CombinePath(aux_path, "usr/bin/busybox"), true);
            Utilities.MakeExecutable(Utilities.CombinePath(aux_path, "usr/bin/busybox"));

            Log.Info("Adding busybox links...");

            var busybox_commands = Utilities.RunCommand(Program.BusyboxPath, "--list").Split('\n').Select(c => c.Trim());
            var skipped = 0;

            foreach(var command in busybox_commands)
            {
                //Utilities.RunCommand("ln", "-Ts {0} {1}", "/usr/bin/busybox", link_name);
                Utilities.Link("/usr/bin/busybox", Utilities.CombinePath(aux_path, "usr/bin/" + command), symbolic: true, check_if_exists: true);
            }

            /*if (File.Exists("dotnet-init.sh"))
            {
                Log.Info("Copying init script...");

                File.WriteAllText(Utilities.CombinePath(aux_path, "dotnet/dotnet-init"), File.ReadAllText("./dotnet-init.sh").Replace("\r", ""));
                Utilities.MakeExecutable(Utilities.CombinePath(aux_path, "dotnet/dotnet-init"));
            }

            if (Directory.Exists("./init-files"))
            {
                Log.Info("Copying init files...");
                Utilities.RunCommand("cp", "-r /init-files {0}", Utilities.CombinePath(aux_path, "dotnet"));
            }*/

            return true;
        }
    }
}
