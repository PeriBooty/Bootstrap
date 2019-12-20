using DiscUtils.Complete;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Bootstrap
{
    class Program
    {
        static readonly string[] projects = { "SparkKernel" };
        static readonly Dictionary<string, string> files = new Dictionary<string, string>()
        {
            { "", "" },
            { "/system/kernel.bin", "SparkKernel/kernel.bin" }
        };

        static int Main(string[] args)
        {
            string target = args.Length > 0 ? args[0] : "x86_64-Debug";
            DirectoryInfo target_dir = new DirectoryInfo(string.Format("out/{0}", target));

            Console.WriteLine("Welcome to the Spark OS Bootstraper");

            if (target_dir.Exists)
            {
                foreach (FileInfo f in target_dir.GetFiles())
                    f.Delete();

                foreach (DirectoryInfo d in target_dir.GetDirectories())
                    d.Delete(true);
            }

            Console.ForegroundColor = ConsoleColor.Blue;

            Console.WriteLine("==> Making directories...");
            Console.ResetColor();

            Directory.CreateDirectory(string.Format("out/{0}/iso/system", target));
            Directory.CreateDirectory(string.Format("out/{0}/boot/grub", target));

            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.WriteLine("==> Building components...");
            Console.ResetColor();

            foreach (string project_path in projects)
            {
                DirectoryInfo project = new DirectoryInfo(string.Format("out/{0}/{1}", target, project_path));

                if (!Directory.Exists(project_path))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("==> Project '{0}' does not exist. <==", project_path));
                    return 1;
                }

                foreach (FileInfo f in project.GetFiles())
                    f.Delete();

                foreach (DirectoryInfo d in project.GetDirectories())
                    d.Delete(true);

                Process.Start("cmake", "-DCMAKE_C_COMPILER=clang -DCMAKE_CXX_COMPILER=clang++");
            }

            Console.ForegroundColor = ConsoleColor.Blue;

            Console.WriteLine("==> Bundling image...");

            SetupHelper.SetupComplete();

            using Stream img = File.Create(string.Format("out/{0}/spark.img", target));

            Disk disk = Disk.Initialize(img, DiscUtils.Streams.Ownership.None, 30 * 1024 * 1024);
            GuidPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsFat);

            using FatFileSystem fs = FatFileSystem.FormatPartition(disk, 0, string.Empty);

            foreach (KeyValuePair<string, string> item in files)
                fs.CopyFile(string.Format("out/{0}/{1}", target, item.Value), item.Key, true);

            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine("==> Finished operation.");
            return 0;
        }
    }
}
