using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace whereis
{
    /// <summary>
    /// An enumuration of the possible architectures.
    /// </summary>
    public enum Architecture
    {
        x86,
        x64
    }

    /// <summary>
    /// Wrapper class for the main entry point of this application.
    /// </summary>
    public class Program
    {       
        /// <summary>
        /// Gets the specified registry key by starting to search from the specified
        /// registry key.
        /// </summary>
        /// <param name="start_in">The registry key to start searching in.</param>
        /// <param name="path">The path, where each key is separate by a back slash.</param>
        /// <returns>The registry key that was reqeusted or null if it does not exists.</returns>
        private static RegistryKey __get_registry_key(RegistryKey start_in, string path)
        {
            if (start_in == null || path == null)
                return null;

            RegistryKey current_key = start_in;

            string[] segments = path.Split('\\');
            foreach (string segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                current_key = current_key.OpenSubKey(segment);
                if (current_key == null)
                    return null;
            }

            return current_key;
        }

        /// <summary>
        /// Gets the assembly directory, the directory the currently executing assembly
        /// is located in.
        /// </summary>
        /// <returns>The assembly directory, the directory the currently executing assembly
        /// is located in.</returns>
        public static string GetAssemblyDirectory()
        {
            string code_base = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(code_base);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        /// <summary>
        /// Determines whether the operating system we're running on is Windows XP SP2 or higher.
        /// </summary>
        /// <returns>True when the operating system we're running on is Windows XP SP2 or higher
        /// and false when it is not.</returns>
        public static bool IsWindowsXPSP2OrHigher()
        {
            OperatingSystem OS = Environment.OSVersion;
            bool windows_xp = (OS.Platform == PlatformID.Win32NT) && ((OS.Version.Major > 5) || ((OS.Version.Major == 5) && (OS.Version.Minor >= 1)));

            return windows_xp;
        }

        /// <summary>
        /// Determines whether Safe DLL search is enabled on this system.
        /// </summary>
        /// <returns>True when safe DLL search is enabled on this system.</returns>
        public static bool IsSafeDllSearchEnabled()
        {       
            bool default_setting = true;
            if (!IsWindowsXPSP2OrHigher())
                default_setting = false;
                
            RegistryKey key_safe_dll_search = __get_registry_key(Registry.LocalMachine, "SYSTEM\\CurrentControlSet\\Control\\Session Manager");
            if (key_safe_dll_search == null)
                return default_setting;

            bool enabled = Convert.ToBoolean(key_safe_dll_search.GetValue("SafeDllSearchMode", 0));
            return enabled;
        }

        // <summary>
        /// Gets all the paths to search in for executables and
        /// scripts. This is done by getting all paths in the `PATH` environment
        /// variable.
        /// </summary>
        /// <returns>All paths in the user and system `PATH` environment variable.</returns>
        public static List<string> GetSearchPaths()
        {
            string[] paths = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine).Split(';');
            return paths.ToList();
        }

        /// <summary>
        /// Gets the path to the directory where the known DLL files are located in.
        /// </summary>
        /// <returns>The path to the directory where the known DLL files are located in. Null is returned
        /// if something went wrong.</returns>
        public static string GetDllDirectory(Architecture architecture)
        {
            RegistryKey key_known_dlls = __get_registry_key(Registry.LocalMachine, "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\KnownDLLs");
            if (key_known_dlls == null)
                return null;     

            string dll_directory_key_name = "DllDirectory";
            if (Environment.Is64BitOperatingSystem && architecture == Architecture.x64)
                dll_directory_key_name = "DllDirectory32";

            object dll_directory_obj = key_known_dlls.GetValue(dll_directory_key_name);
            if (dll_directory_obj == null)
                return null;

            string dll_directory = dll_directory_obj.ToString();
            return dll_directory;
        }

        /// <summary>
        /// Gets al list of known DLL files from HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs.
        /// </summary>
        /// <remarks>This does respect `ExcludeFromKnownDlls`. DLL's in `ExcludeFromKnownDlls` will be ignored.</remarks>
        /// <returns>A list of known DLL files from HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs.</returns>
        public static List<string> GetKnownDlls()
        {
            List<string> results = new List<string>();

            RegistryKey key_known_dlls = __get_registry_key(Registry.LocalMachine, "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\KnownDLLs");
            if (key_known_dlls == null)
                return results;

            RegistryKey key_exclude_known_dlls = __get_registry_key(Registry.LocalMachine, "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\ExcludeFromKnownDlls");
            string[] exclude_known_dlls = new string[0];
            if (key_exclude_known_dlls != null)
                exclude_known_dlls = key_exclude_known_dlls.GetValueNames();

            string[] value_names = key_known_dlls.GetValueNames();
            foreach (string value_name in value_names)
            {
                if (value_name == "DllDirectory" || value_name == "DllDirectory32")
                    continue;

                bool exclude = false;
                foreach (string exclude_dll in exclude_known_dlls)
                {
                    if (exclude_dll != value_name)
                        continue;

                    exclude = true;
                    break;
                }

                if (exclude)
                    continue;

                string value = key_known_dlls.GetValue(value_name).ToString();
                results.Add(value);
            }

            return results;
        }

        /// <summary>
        /// Gets all possible absolute paths of a command, by examing the `PATH` environment
        /// variable.
        /// </summary>
        /// <param name="command">The command to find the absolute path of.</param>
        /// <param name="working_directory">Optional: The current working directory to search in.</param>
        /// <returns>All possible absolute paths for the specified command.</returns>
        public static List<string> FindCommandPaths(string command, Architecture architecture, string working_directory = null)
        {
            List<string> results = new List<string>();

            List<string> possible_names = new List<string>();
            if (!command.EndsWith(".cmd") && !command.EndsWith(".exe") && !command.EndsWith(".bat"))
            {
                possible_names.Add(string.Format("{0}.exe", command));
                possible_names.Add(string.Format("{0}.cmd", command));
                possible_names.Add(string.Format("{0}.bat", command));
            }
            else
            {
                possible_names.Add(command);
            }

            if (working_directory == null)
                working_directory = Directory.GetCurrentDirectory();

            foreach (string possible_name in possible_names)
            {
                IEnumerable<string> possible_name_results = FindFilePaths(possible_name, architecture);
                results.AddRange(possible_name_results);
            }

            return results;
        }

        /// <summary>
        /// Gets the absolute path of a command, by examing the `PATH` environment
        /// variable.
        /// </summary>
        /// <param name="command">The command to find the absolute path of.</param>
        /// <param name="working_directory">Optional: The current working directory to search in.</param>
        /// <returns>The absolute path of the specified command or null if no absolute
        /// path for the specified command could be found.</returns>
        public static string FindCommandPath(string command, Architecture architecture, string working_directory = null)
        {
            List<string> absolute_paths = FindCommandPaths(command, architecture, working_directory);
            if (absolute_paths.Count <= 0)
                return null;

            string cmd_absolute_path = absolute_paths[0];
            return cmd_absolute_path;
        }

        /// <summary>
        /// Gets all absolute paths to the specified file, by examing the `PATH` environment
        /// variable.
        /// </summary>
        /// <param name="dll_filename">The name of the DLL file to find all possible absolute paths for.</param>
        /// <param name="working_dir">Optional: The current working directory to search in.</param>
        /// <param name="executable_dir">Optional: The directory the executable is in located.</param>
        /// <returns>All possible absolute paths for the specified file.</returns>
        public static List<string> FindFilePaths(string filename, Architecture architecture, string working_dir = null, string executable_dir = null)
        {           
            List<string> results = new List<string>();

            if (filename == null)
                return results;

            List<string> known_dlls = GetKnownDlls();
            if (known_dlls.Contains(filename))
            {
                string absolute_path = Path.Combine(
                    GetDllDirectory(architecture),
                    filename                    
                );

                if (File.Exists(absolute_path))
                    results.Add(absolute_path);
            }

            if (working_dir == null)
                working_dir = Directory.GetCurrentDirectory();

            if (executable_dir == null)
                executable_dir = GetAssemblyDirectory();

            bool safe_dll_search_enabled = IsSafeDllSearchEnabled();

            List<string> search_paths = new List<string>();
            search_paths.Add(executable_dir);

            if(!safe_dll_search_enabled)
                search_paths.Add(working_dir);

            search_paths.Add(Environment.SystemDirectory);
            search_paths.Add("C:\\Windows\\System");
            search_paths.Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

            if (safe_dll_search_enabled)
                search_paths.Add(working_dir);

            search_paths.AddRange(
                GetSearchPaths()
            );

            foreach (string path in search_paths)
            {
                string expanded_path = Environment.ExpandEnvironmentVariables(path);
                if (!Directory.Exists(expanded_path))
                    continue;

                string possible_absolute_path = Path.Combine(expanded_path, filename);
                if (!File.Exists(possible_absolute_path))
                {
                    continue;
                }

                results.Add(possible_absolute_path);
            }

            return results;
        }

        /// <summary>
        /// Gets the absolute path to the specified file, by examing the `PATH` environment
        /// variable.
        /// </summary>
        /// <param name="dll_filename">The name of the DLL file to find all possible absolute paths for.</param>
        /// <param name="working_directory">Optional: The current working directory to search in.</param>
        /// <returns>The absolute path for the specified filename. Null is returned if it could
        /// not be found.</returns>
        public static string FindFilePath(string filename, Architecture architecture, string working_directory = null)
        {
            List<string> absolute_paths = FindFilePaths(filename, architecture, working_directory);
            if (absolute_paths.Count <= 0)
                return null;

            string file_absolute_path = absolute_paths[0];
            return file_absolute_path;
        }

        /// <summary>
        /// Main entry point for this program.
        /// </summary>
        /// <param name="args">The command line parameters that were passed to this application.</param>
        internal static void Main(string[] args)
        {
            //If no parameters where specified, just dump usage/help
            if (args.Length <= 0)
            {
                Console.WriteLine("Usage: whereis [command/filename] [optional:x86/x64] [optional:working directory] [optional:executeable directory]");
                return;
            }

            //Determine the architecture
            Architecture architecture = Architecture.x86;
            if (Environment.Is64BitOperatingSystem)
                architecture = Architecture.x64;

            //The user can override the architecture
            if (args.Length > 1)
            {
                if (args[1] == "x86")
                    architecture = Architecture.x86;
                else if (args[1] == "x64")
                    architecture = Architecture.x64;
            }

            //The user can override the working directory
            string working_directory = null;
            if (args.Length > 2)
            {
                if(Directory.Exists(args[2]))
                    working_directory = args[2];
            }

            //The user can overwrite the executable directory
            string executable_directory = null;
            if (args.Length > 3)
            {
                if (Directory.Exists(args[3]))
                    executable_directory = args[3];
            }

            //Extract the command/filename to search for and declare an empty list
            //to store the result in
            string search_for = args[0];
            List<string> results = null;
            
            //If it's a file, use FindFilePaths, otherwise use FindCommandPaths, which builds up
            //a list of possible filenames and then uses FindFilePaths on that list
            if (search_for.Contains("."))
                results = FindFilePaths(search_for, architecture, working_directory, executable_directory);
            else
                results = FindCommandPaths(search_for, architecture, working_directory);

            //Dump the results to the console
            foreach (string path in results)
            {
                Console.WriteLine(path);
            }

#if DEBUG
            Console.ReadKey();
#endif
        }
    }
}
