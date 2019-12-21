#include <iostream>
#include <fstream>
#include <map>
#include <filesystem>

#include <archive.h>
#include <archive_entry.h>

#include "termcolor.hpp"

namespace fs = std::filesystem;
using namespace std::string_literals;

// TODO: Get these from a config file or command line options
static const std::map<std::string, std::string> files = {
        { "EFI/BOOT/BOOTX64.EFI", "SparkBoot/BOOTX64.EFI" },
        { "system/kernel.bin",     "SparkKernel/kernel.bin" }
};
static const std::string projects[] = { "SparkBoot", "SparkKernel" };
static const std::map<std::string, std::string> repos = {
        {"SparkBoot", "https://github.com/Official-Spark-OS/SparkBoot.git"},
        {"SparkKernel", "https://github.com/Official-Spark-OS/SparkKernel.git"}
};

/**
 * A simple method to convert a c_str to a path,
 * so I don't have to manually do `std::string("c_str")` every time.
 * @see std::string_literals::operator ""s
 * @return c_str casted to fs::path
 */
fs::path operator ""_p(const char* str, std::size_t len)
{
    return std::string{ str, len };
}

int main(int argc, char** argv)
{
    std::string target = argc > 1 ? argv[1] : "x86_64-Debug";
    fs::path targetDir = "out";
    targetDir /= target;

    std::cout << "Welcome to the Spark OS Bootstrapper" << std::endl;

    // TODO: See line 51
    if (fs::exists(targetDir))
        fs::remove_all(targetDir);

    std::cout << termcolor::blue << "==> Making directories..." << termcolor::reset << std::endl;

    fs::create_directories(targetDir);

    std::cout << termcolor::magenta << "==> Building components..." << termcolor::reset << std::endl;

    for (const auto& projpath : projects)
    {
        std::cout << "Building project '" << projpath << "'" << std::endl;
        fs::path project = targetDir / projpath;

        // TODO: Only build the project if objects are out of date, we already use CMake so it shouldn't be too hard
        if (fs::exists(project))
            fs::remove_all(project);

        fs::create_directory(project);

        if (fs::exists(projpath))
        {
            if (!fs::exists(projpath / "CMakeLists.txt"_p))
            {
                std::cout << termcolor::yellow << "'" << projpath << "' does not appear to be a CMakeProject. Re-cloning... <==" << termcolor::reset << std::endl;
                fs::remove_all(projpath);
                int result = std::system(("git clone --recursive "s + repos.at(projpath) + " " + projpath).c_str());
                if (result != 0)
                {
                    std::cout << termcolor::red << "Failed to clone '" << projpath << "'. Aborting. <==" << termcolor::reset << std::endl;
                    return 1;
                }
            }
            else if (fs::exists(projpath / ".git"_p))
            {
                std::cout << termcolor::green << "==> Pulling latest changes for '" << projpath << "'" << termcolor::reset << std::endl;
                fs::current_path(projpath);
                int result = std::system("git pull && git submodule foreach git pull origin master");
                if (result != 0)
                {
                    std::cout << termcolor::yellow << "Failed to pull latest changes for '" << projpath << "' but project appears to be valid. Continuing... <==" << termcolor::reset << std::endl;
                }

                // FIXME: Shouldn't assume that projpath is a single directory level.
                fs::current_path("../");
            }
        }
        else
        {
            if (repos.find(projpath) == repos.end())
            {
                std::cout << termcolor::red << "CMake project '" << projpath << "' does not exist and isn't linked to a git repo. Aborting... <==" << termcolor::reset << std::endl;
                return 1;
            }

            std::cout << termcolor::green << "==> Cloning project '" << projpath << "'" << termcolor::reset << std::endl;
            int result = std::system(("git clone --recursive "s + repos.at(projpath) + " " + projpath).c_str());
            if (result != 0)
            {
                std::cout << termcolor::red << "Failed to clone '" << projpath << "'. Aborting. <==" << termcolor::reset << std::endl;
                return 1;
            }
        }

        if (!fs::exists(projpath) || !fs::exists(projpath / "CMakeLists.txt"_p))
        {
            std::cout << termcolor::red << "CMake project '" << projpath
                      << "' is missing CMakeLists.txt or does not exist. <==" << termcolor::reset << std::endl;
            return 1;
        }

        fs::current_path(project);

        int exitCode = std::system(
                (R"(cmake -DCMAKE_SYSTEM_NAME=Generic -DCMAKE_C_COMPILER=clang -DCMAKE_CXX_COMPILER=clang++ -DCMAKE_ASM_NASM_COMPILER=nasm -G "Ninja" ../../../)"
                        + projpath).c_str());
        if (exitCode != 0)
        {
            std::cout << termcolor::red << "CMake exited with exit code " << exitCode << termcolor::reset << std::endl;
            return 1;
        }

        exitCode = std::system("ninja");
        if (exitCode != 0)
        {
            std::cout << termcolor::red << "Ninja exited with exit code " << exitCode << termcolor::reset << std::endl;
            return 1;
        }

        fs::current_path("../../../");
    }

    std::cout << termcolor::blue << "==> Bundling image..." << termcolor::reset << std::endl;

    // TODO: Perhaps move this to it's own function?
    struct archive *a;
    struct archive_entry *entry = nullptr;
    struct stat st;
    char* buff;
    std::ifstream infile;
    int len;

    a = archive_write_new();
    archive_write_add_filter_none(a);
    archive_write_add_filter_none(a);
    archive_write_set_format_iso9660(a);
    archive_write_set_format_option(a, NULL, "joliet", "true");
    archive_write_set_format_option(a, NULL, "volume-id", "SPARK_OS");
    archive_write_set_option(a, NULL, "rockridge", NULL);
    archive_write_set_option(a, NULL, "pad", NULL);
    archive_write_set_option(a, NULL, "boot", "EFI/BOOT/BOOTX64.EFI");
    archive_write_set_option(a, NULL, "boot-load-seg", "0x00");
    archive_write_set_option(a, NULL, "boot-type", "no-emulation");
    archive_write_set_option(a, NULL, "boot-load-size", "4");
    archive_write_open_filename(a, (targetDir / "spark.iso"_p).c_str());
    for (auto& item : files)
    {
        stat((targetDir / item.second).c_str(), &st);
        if (entry != nullptr)
            archive_entry_clear(entry);
        else
            entry = archive_entry_new();

        archive_entry_set_pathname(entry, item.first.c_str());
        archive_entry_set_size(entry, st.st_size);
        archive_entry_set_filetype(entry, AE_IFREG);
        archive_write_header(a, entry);
        infile.open(targetDir / item.second, std::ios::binary);
        infile.seekg(0, std::ios::end);
        len = infile.tellg();
        infile.seekg(0, std::ios::beg);
        buff = new char[len];
        infile.read(buff, len);
        archive_write_data(a, buff, len);
        delete[] buff;
        infile.close();
    }
    int result = archive_write_close(a);
    if (result == ARCHIVE_FATAL)
    {
        std::cout << termcolor::red << "Writing ISO file failed with code " << archive_errno(a) << " (" << archive_error_string(a) << ") <==" << termcolor::reset << std::endl;
        archive_write_free(a);
        return 1;
    }
    archive_write_free(a);

    std::cout << termcolor::green << "==> Finished operation." << termcolor::reset << std::endl;
    return 0;
}
