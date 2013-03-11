using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SubModuleLink
{
    public class Program
    {
        private const string SUBMODULE_FILE_NAME = ".gitmodules";

        private static readonly Regex _submoduleSection = new Regex(@"\[\W*submodule\W+""([^""]+)""\W*\]", RegexOptions.Compiled);
        private static readonly Regex _submoduleValue = new Regex(@"\W*(\w+)\W*=\W*(\S+)", RegexOptions.Compiled);

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 1;
            }

            var undo = false;
            if (args.Length > 1)
            {
                for (var i = 0; i < args.Length - 1; i++)
                    if (args[i] == "/u")
                    {
                        undo = true;
                        break;
                    }
            }

            // get target directory
            var path = args[args.Length - 1].Trim(new[] { '"', ' ' });
            if (path.StartsWith("/"))
            {
                PrintHelp();
                return 1;
            }

            if (!Path.IsPathRooted(path))
            {
                // add to working path
                path = Path.Combine(Environment.CurrentDirectory, path);
            }

            // normalise path
            path = Path.GetFullPath(path);

            if (!Directory.Exists(path))
            {
                Console.WriteLine("Path '{0}' was not found.", path);
                return 1;
            }

            // load submodules
            var baseFile = Path.Combine(path, SUBMODULE_FILE_NAME);
            if (!File.Exists(baseFile)) return 0;

            var availableSubmodules = LoadSubmodules(baseFile).ToDictionary(s => s.Url);
            if (availableSubmodules.Count == 0) return 0;

            foreach (var submodulePath in GetSubmodulePaths(path).Skip(1))
            {
                foreach (var sm in LoadSubmodules(submodulePath.Item2))
                {
                    SubModule target;
                    if (!availableSubmodules.TryGetValue(sm.Url, out target))
                    {
                        Console.Error.WriteLine("Unable to find submodule '{0}' in primary project.", sm.Name);
                        continue;
                    }

                    var junction = new DirectoryInfo(Path.Combine(submodulePath.Item1, sm.Path));
                    if (junction.Exists)
                    {
                        if (undo)
                        {
                            try
                            {
                                JunctionPoint.Delete(junction.FullName);
                            }
                            catch (Exception exception)
                            {
                                Console.Error.WriteLine("Unable to delete junction '{0}': {1}", junction.FullName, exception.Message);        
                            }

                            junction.Create();
                            continue;
                        }

                        // if it's already junction ignore it
                        if (junction.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                        if (junction.GetFiles().Length > 0 || junction.GetDirectories().Length > 0) continue;
                    }

                    // create junction
                    try
                    {
                        JunctionPoint.Create(junction.FullName, Path.Combine(path, target.Path), true);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine("Unable to create junction '{0}': {1}", junction.FullName, exception.Message);
                    }
                }
            }

            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage: [/u] SubModuleLink.exe targetDirectory");
        }

        private static IEnumerable<Tuple<string, string>> GetSubmodulePaths(string path)
        {
            var file = Path.Combine(path, SUBMODULE_FILE_NAME);
            if (File.Exists(file)) yield return Tuple.Create(path, file);

            foreach (var dir in Directory.GetDirectories(path))
                foreach (var f in GetSubmodulePaths(dir)) yield return f;
        }

        private static IEnumerable<SubModule> LoadSubmodules(string submoduleFilePath)
        {
            StreamReader sr = null;

            try
            {
                sr = new StreamReader(File.OpenRead(submoduleFilePath));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Error reading file '{0}': {1}", submoduleFilePath, exception.Message);
            }

            string line, name = null, path = null, url = null;
            while ((line = sr.ReadLine()) != null)
            {
                var match = _submoduleSection.Match(line);
                if (match.Success)
                {
                    // store old section if we have details
                    if(name != null && path != null && url != null) 
                        yield return new SubModule(name, path, url);

                    name = match.Groups[1].Value;
                    path = url = null;
                }
                else if((match = _submoduleValue.Match(line)).Success)
                {
                    switch (match.Groups[1].Value.ToLowerInvariant())
                    {
                        case "path":
                            path = match.Groups[2].Value.Replace('/', '\\');
                            break;

                        case "url":
                            url = match.Groups[2].Value;
                            break;
                    }
                }
            }

            if (name != null && path != null && url != null)
                yield return new SubModule(name, path, url);
        }
    }

    public class SubModule
    {
        public SubModule(string name, string path, string url)
        {
            Name = name;
            Path = path;
            Url = url;
        }

        public string Name { get; private set; }
        public string Path { get; private set; }
        public string Url { get; private set; }
    }
}
