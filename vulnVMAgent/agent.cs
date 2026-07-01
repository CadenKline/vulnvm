/*
 * class that controls the functionality of the agent on the vm sandbox
 * tracks dropped files, monitors process creatios, and logs process lineage for analysis
*/ 

using System.Management;

class Agent
{
    const string DroppedFilesFolder = @"C:\vulnVMAgent\dropped";
    static readonly Dictionary<int, int> ProcessParents = new();
    static readonly Dictionary<int, string> TaintedProcesses = new();
    static readonly object _lock = new();
    static string _logPath = @"C:\vulnVMAgent\log.txt";

    static void Main(string[] args)
    {
        // creates the logging directory and output file path
        _logPath = args.Length > 0 ? args[0] : @"C:\vulnVMAgent\log.txt";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            Directory.CreateDirectory(DroppedFilesFolder);
            Log("Agent started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write startup log: {ex}");
        }

        Console.WriteLine("vulnVMAgent running");

        // starts process monitoring, now with parent-PID attribution
        try
        {
            var watcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace")
            );
            watcher.EventArrived += OnProcessStarted;
            watcher.Start();
            Log("Process watcher started");
        }
        catch (Exception ex)
        {
            Log($"Watcher failed to start (process): {ex}");
            return;
        }

        // dropped file watcher
        // when the host copies a file in here (e.g. via guestcontrol copyto, triggered
        // by a drag and drop in the host UI), this fires and marks it as a tracked root
        try
        {
            var dropWatcher = new FileSystemWatcher(DroppedFilesFolder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName
            };
            dropWatcher.Created += OnFileDropped;
            dropWatcher.Error += (s, e) => Log($"Dropped files watcher error: {e.GetException()}");
            dropWatcher.EnableRaisingEvents = true;
            Log($"Dropped files watcher started: {DroppedFilesFolder}");
        }
        catch (Exception ex)
        {
            Log($"Watcher failed to start (dropped files): {ex}");
        }

        while (true) Thread.Sleep(1000);
    }

    static void OnFileDropped(object sender, FileSystemEventArgs e)
    {
        // give the copy a moment to finish landing before anyone tries to run it
        Thread.Sleep(250);
        Log($"FILE DROPPED: {e.FullPath} — now tracking as root for any process it spawns");
    }

    static void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        string processName = e.NewEvent["ProcessName"]?.ToString() ?? "(unknown)";
        int pid = SafeToInt(e.NewEvent["ProcessID"]);
        int parentPid = SafeToInt(e.NewEvent["ParentProcessID"]);

        lock (_lock)
        {
            ProcessParents[pid] = parentPid;

            // case 1: this process IS a dropped file being executed directly
            // (its image path/name matches something sitting in DroppedFilesFolder).
            string? matchedDrop = TryMatchDroppedFile(processName);
            if (matchedDrop != null)
            {
                TaintedProcesses[pid] = matchedDrop;
                Log($"Process started: {processName} (PID {pid}, parent {parentPid}) — THIS IS A DROPPED FILE BEING RUN ({matchedDrop})");
                return;
            }

            // case 2: this process's parent is already tainted (i.e. it was spawned,
            // directly or indirectly, by a dropped file). inherits the taint.
            if (TaintedProcesses.TryGetValue(parentPid, out var rootFile))
            {
                TaintedProcesses[pid] = rootFile;
                Log($"Process started: {processName} (PID {pid}, parent {parentPid}) — SPAWNED BY [{rootFile}] lineage");
                return;
            }

            // case 3: ordinary, untainted process -- logs plainly, same as before.
            Log($"Process started: {processName} (PID {pid}, parent {parentPid})");
        }
    }

    // checks to see if the newly started process matches one of the files dropped into analysis directory
    static string? TryMatchDroppedFile(string processImagePath)
    {
        try
        {
            string runningFileName = Path.GetFileName(processImagePath);
            foreach (var droppedFile in Directory.GetFiles(DroppedFilesFolder))
            {
                if (string.Equals(Path.GetFileName(droppedFile), runningFileName, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFileName(droppedFile);
            }
        }
        catch
        {
            // folder may not exist yet on a very early event; ignore ts
        }
        return null;
    }

    static int SafeToInt(object? value)
    {
        try { return Convert.ToInt32(value); }
        catch { return -1; }
    }

    // default logging mechanism that tracks the time when the event is executed and what it is.
    static void Log(string message)
    {
        string line = $"{DateTime.Now}: {message}";
        Console.WriteLine(line);
        try
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Log write failed: {ex}");
        }
    }
}
