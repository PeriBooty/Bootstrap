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
        static readonly string[] projects = { "SparkBoot", "SparkKernel" };
        static readonly Dictionary<string, string> files = new Dictionary<string, string>()
        {
            { "/EFI/BOOT/BOOTX64.EFI", "SparkBoot/BOOTX64.EFI" },
            { "/system/kernel.bin", "SparkKernel/kernel.bin" }
        };

        static int Main(string[] args)
        {
            Directory.SetCurrentDirectory("../../../");
            Console.WriteLine(Directory.GetCurrentDirectory());
            string target = args.Length > 0 ? args[0] : "x86_64-Debug";
            DirectoryInfo target_dir = new DirectoryInfo($"out/{target}");

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

            Directory.CreateDirectory($"out/{target}");

            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.WriteLine("==> Building components...");
            Console.ResetColor();

            foreach (string project_path in projects)
            {
                Console.ResetColor();
                Console.WriteLine($"Building project '{project_path}'");

                DirectoryInfo project = new DirectoryInfo($"out/{target}/{project_path}");
                if (!project.Exists)
                    Directory.CreateDirectory($"out/{target}/{project_path}");

                if (!Directory.Exists(project_path) || !File.Exists($"{project_path}/CMakeLists.txt"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;

                    Console.WriteLine($"CMake project '{project_path}' is missing CMakeLists.txt or does not exist. <==");
                    Console.ResetColor();
                    return 1;
                }

                foreach (FileInfo f in project.GetFiles())
                    f.Delete();

                foreach (DirectoryInfo d in project.GetDirectories())
                    d.Delete(true);

                Directory.SetCurrentDirectory($"out/{target}/{project_path}");

                try
                {
                    ProcessStartInfo cmake = new ProcessStartInfo("cmake", $"-DCMAKE_SYSTEM_NAME=Generic -DCMAKE_C_COMPILER=clang -DCMAKE_CXX_COMPILER=clang++ -DCMAKE_ASM_NASM_COMPILER=nasm -G \"Ninja\" ../../../{project_path}");
                    Process cmake_p = Process.Start(cmake);

                    cmake_p.WaitForExit();

                    if (cmake_p.ExitCode != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;

                        Console.WriteLine($"CMake exited with exit code {cmake_p.ExitCode}");
                        Console.ResetColor();

                        return 1;
                    }


                    ProcessStartInfo ninja = new ProcessStartInfo("ninja");
                    Process ninja_p = Process.Start(ninja);

                    ninja_p.WaitForExit();

                    if (ninja_p.ExitCode != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;

                        Console.WriteLine($"Ninja exited with exit code {ninja_p.ExitCode}");
                        Console.ResetColor();

                        return 1;
                    }

                }
                catch (System.ComponentModel.Win32Exception exc)
                {
                    Console.ForegroundColor = ConsoleColor.Red;

                    Console.WriteLine($"An error occurred:\n\r{exc.Message}");
                    Console.ResetColor();

                    return 1;
                }

                Directory.SetCurrentDirectory("../../../");
            }

            Console.ForegroundColor = ConsoleColor.Blue;

            Console.WriteLine("==> Bundling image...");
            Console.ResetColor();

            SetupHelper.SetupComplete();

            using Stream img = File.Create($"out/{target}/spark.img");

            Disk disk = Disk.Initialize(img, DiscUtils.Streams.Ownership.None, 30 * 1024 * 1024);
            GuidPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsFat);

            using FatFileSystem fs = FatFileSystem.FormatPartition(disk, 0, string.Empty);

            foreach (KeyValuePair<string, string> item in files)
                fs.CopyFile($"out/{target}/{item.Value}", item.Key, true);

            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine("==> Finished operation.");
            Console.ResetColor();
            return 0;
        }
    }
}
