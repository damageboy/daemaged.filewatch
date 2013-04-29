using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.Options;

namespace Daemaged.FileWatch
{
    public class Program
    {
      private static OptionSet _options;

      public static int Main(string[] args)
      {
        var recurse = false;
        var includePatterns = new List<string>();
        var excludePatterns = new List<string>();
        var verbose = false;
        var executeFile = false;
        var showVersion = false;
        var showHelp = false;
        var wait = false;
        string directory = ".";
        _options = new OptionSet {
          { "e|exec", "Execute file as a script/program when file is updated", v => executeFile = v != null },
          { "w|wait", "Wait for the file(s) to change and exit", v => wait = v != null },
          { "d|directory", "specify a directory to watch, default to '.'", v => directory = v},
          { "r|recurse", "Recurse into the current directory, watching everything matching 'expression'", v => recurse = v != null },
          { "i|include=", "include files according to pattern", includePatterns.Add },
          { "x|exclude=", "exclude files according to pattern", excludePatterns.Add },
          { "v|verbose", "output verbose informational logs", v => verbose = v != null },
          { "V|version", "show version information about this program", v => showVersion = v != null},
          { "h|help", "Show this message", v => showHelp = v != null },
        };

        var extraArgs = _options.Parse(args);

        if (extraArgs.Count != 1 && !executeFile && !wait)
          DieUsage("Either -e/--execute OR -w/--wait OR one command must be specific");

        if (includePatterns.Count == 0)
          DieUsage("At least one include pattern must be specified");

        if (!Directory.Exists(directory))
          DieUsage(string.Format("Watch directory {0} does not exist", directory));

        String cmd = null;
        if (!executeFile && !wait)
         cmd = extraArgs.Single();

        var fsw = new BetterFileSystemWatcher(directory) {IncludeSubdirectories = recurse};

        FileSystemEventHandler handleChange = (o, e) => {
          if (verbose)
            Console.WriteLine("Testing file {0}", e.FullPath);

          if (excludePatterns.Any(p => Glob.FnMatch(p, e.FullPath, Glob.Constants.FNM_NOESCAPE)))
          {
            if (verbose)
              Console.WriteLine("File matched exclusion pattern, skipping...");
            return;
          }
          if (!includePatterns.Any(p => Glob.FnMatch(p, e.FullPath, Glob.Constants.FNM_NOESCAPE | Glob.Constants.FNM_DOTMATCH)))
          {
            if (verbose)
              Console.WriteLine("File did not match any inclusion pattern, skipping...");

            return;
          }

          if (verbose)
            Console.WriteLine("Found match for patterns: {0}", e.FullPath);
          if (wait)
            Environment.Exit(0);

          if (executeFile)
          {
            var p = Process.Start(e.FullPath);
            p.WaitForExit();
            return;
          }

          var psi = new ProcessStartInfo
          {
            FileName = "cmd.exe",
            Arguments = string.Format("/c \"{0}\"", cmd),
            UseShellExecute = false,
          };
          psi.EnvironmentVariables.Add("FILENAME", e.FullPath);

          Process.Start(psi);         
        };

        fsw.Changed += handleChange;
        fsw.Created += handleChange;
        //fsw.Renamed += handleChange;

        fsw.EnableRaisingEvents = true;

        if (verbose)
          Console.WriteLine("Watching directory '{0}' for changes....", directory);

        while(true)
          Thread.Sleep(1000);
        
        return 0;

      }


      private static void DieUsage(string msg)
      {
        Console.WriteLine("Usage Error: {0}", msg);
        Console.WriteLine();
        if (_options != null)
        {
          Console.WriteLine("Usage :");
          _options.WriteOptionDescriptions(Console.Out);
        }
        Environment.Exit(-1);
      }
    }
}
