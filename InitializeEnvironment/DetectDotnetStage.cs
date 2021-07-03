using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace InitializeEnvironment
{
    public class DetectDotnetStage : IStage
    {
        public string StageIdentifier => "detect-dotnet";
        private Logger Log = LogManager.GetCurrentClassLogger();

        public DetectDotnetStage()
        {

        }

        public bool Execute()
        {
            Log.Info("Trying to find dotnet...");

            var dotnet_location = Utilities.Which("dotnet");

            if(!File.Exists(dotnet_location)) // dotnet isn't in PATH
            {
                Log.Warn("dotnet isn't in PATH");

                var args = Environment.CommandLine.Split(' ');

                if (!args.Any() || !File.Exists(args.First()) || !Path.GetFileName(args.First()).Equals("dotnet"))
                {
                    Log.Error("dotnet couldn't be found in command line or in PATH, is the binary itself called dotnet? If not, try renaming it.");
                }
                else
                {
                    dotnet_location = args.First();
                }
            }

            bool download_new_dotnet = false;

            if (File.Exists(dotnet_location))
            {
                var possible_system_locations = new[] { "/usr/", "/var/", "/opt/", "/bin/" };

                if (possible_system_locations.Any(s => dotnet_location.StartsWith(s)))
                {
                    if (!Program.UseDownloadedDotnet && !Program.AvoidPrompts)
                    {
                        download_new_dotnet = Utilities.Prompt("Your installation of dotnet looks like it was " +
                            "created by a package manager. These types of installations can cause problems when creating " +
                            "a secondary environment. Do you want to download a fresh copy dedicated to the new environment?");
                     }
                     else
                     {
                        download_new_dotnet = true;
                     }
                }
            }
            else
                download_new_dotnet = true;

            if(download_new_dotnet && File.Exists("./dotnet-tmp/dotnet"))
            {
                //download_new_dotnet = !Program.UseDownloadedDotnet ? (Program.AvoidPrompts ? true : Utilities.Prompt("Found a downloaded copy of dotnet. Reuse it?")) : true;
                if (!Program.UseDownloadedDotnet)
                {
                    if (Program.AvoidPrompts)
                        download_new_dotnet = true;
                    else
                        download_new_dotnet = Utilities.Prompt("Found a downloaded copy of dotnet. Reuse it?");
                }
                else { download_new_dotnet = false; }
                dotnet_location = Path.GetFullPath("./dotnet-tmp/dotnet");
            }

            if(download_new_dotnet)
            {
                // I know, this is horrible

                var client = new WebClient();
                Log.Info("Downloading .NET Core...");
                
                var release_manifest = JObject.Parse(client.DownloadString("http://raw.githubusercontent.com/dotnet/core/master/release-notes/5.0/releases.json"));
                var releases = release_manifest["releases"];
                
                var most_recent_release = releases.Where(r => r["runtime"].Type != JTokenType.Null).OrderByDescending(r => DateTime.Parse(r.Value<string>("release-date"))).First();
                var runtime_obj = most_recent_release["runtime"];
                var runtime_files = runtime_obj["files"];

                var linux_x64 = runtime_files.First(r => r.Value<string>("rid") == "linux-x64");
                var url = linux_x64.Value<string>("url");
                var hash = linux_x64.Value<string>("hash");

                Log.Info("Version: {0}", runtime_obj["version"]);

                string filename = "./dotnet-tmp/dotnet.tar.gz";
                var download_result = Utilities.DownloadFileWithProgress(url, filename);

                Log.Info("Extracting {0}...", filename);

                Log.Debug(Utilities.RunCommand("tar", "-xf {0} --directory dotnet-tmp", filename));

                File.Delete(filename);

                dotnet_location = Path.GetFullPath("./dotnet-tmp/dotnet");
            }

            Program.DotnetPath = dotnet_location;
            Log.Debug("Found dotnet at {0}", dotnet_location);

            return true;
        }
    }
}
