using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace InitializeEnvironment
{
    public static class Utilities
    {
        public static Logger Log = LogManager.GetCurrentClassLogger();

        public static string CombinePath(params string[] path) => !path.Any() ? null :
            Path.Combine(path[0], Path.Combine(path.Skip(1).Select(p => p.TrimStart('/')).ToArray()));

        public static string RunCommand(string command, string arguments, params object[] format)
        {
            var psi = new ProcessStartInfo("/bin/bash", "-c \"" + command + " " + string.Format(arguments, format) + "\"");

            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;

            //Console.WriteLine(psi.Arguments);

            var process = Process.Start(psi);
            
            var ret = process.StandardOutput.ReadToEnd();

            return ret;
        }

        public static string FindLibrary(string search_term)
        {
            var libs = RunCommand("ldconfig", "-p").Split('\n');

            foreach(var lib in libs)
            {
                if (lib.Contains(search_term))
                    return lib.Split(new[] { "=>" }, StringSplitOptions.None)[1].Trim();
            }

            return null;
        }

        public static string Which(string program)
        {
            return RunCommand("which", program).Trim();
        }

        public static void MakeExecutable(string file)
        {
            RunCommand("chmod", "+x {0}", file);
        }

        public static bool Prompt(string message)
        {
            Console.WriteLine("{0} [y/n]", message);

            var choice = ' ';

            while (choice != 'y' && choice != 'n')
                choice = char.ToLower(Console.ReadKey(true).KeyChar);

            return choice == 'y';
        }

        public static SHA512 SHA = new SHA512CryptoServiceProvider();

        public static byte[] CalculateChecksum(string filename)
        {
            return SHA.ComputeHash(File.ReadAllBytes(filename));
        }

        public static bool DownloadFileWithProgress(string url, string filename)
        {
            var client = new WebClient();
            int last_prog = int.MinValue + 100;

            client.DownloadProgressChanged += (s, e) =>
            {
                if (e.ProgressPercentage - last_prog < 10)
                    return;

                lock (client)
                {
                    if (e.ProgressPercentage - last_prog < 5)
                        return;

                    Log.Debug("{0} {1}% done ({2}/{3})", Path.GetFileName(filename), e.ProgressPercentage, e.BytesReceived, e.TotalBytesToReceive);
                    last_prog = e.ProgressPercentage;
                }
            };

            Directory.CreateDirectory(Path.GetDirectoryName(filename));
            var task = client.DownloadFileTaskAsync(url, filename);
            task.Wait();

            return File.Exists(filename);
        }
    }
}
