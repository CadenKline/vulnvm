/*
 * Class that runs basic functions of the virtual machine and the agent deployment pipeline
 * Refer to the actual agent functionality in agent.cs
*/

using System.Diagnostics;
using helpers;
using gui;

namespace vm
{
    public class VmSettings
    {
        public string IsoPath { get; set; }
        public string VmName { get; set; }
        public string SnapshotName { get; set; }
        public string VdiPath { get; set; }
        public string VBoxPath { get; set; }
        public string VmPath { get; set; }
        public string AgentPath { get; set; }
        public int RamGB { get; set; }
        
        public int StorageGB { get; set; }
        public int CpuCount { get; set; }
    }
    public class RunProcess
    {
        public (string output, string error) DoCommand(string args)
        {
            var vboxPath = FindVirtualBoxPath();
            if (!string.IsNullOrEmpty(vboxPath))
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!currentPath.Contains(vboxPath))
                {
                    Environment.SetEnvironmentVariable("PATH", vboxPath + ";" + currentPath);
                }
            }
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {                    
                    FileName = "VBoxManage",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // enables basic error calling
            // todo: find a cleaner way to display error output. 
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"Error: {error}");
            }

            return (output, error);
        }

        private string FindVirtualBoxPath()
        {
            string[] paths = new[]
            {
            @"C:\Program Files\Oracle\VirtualBox",
            @"C:\Program Files (x86)\Oracle\VirtualBox",
            @"C:\Program Files\VirtualBox"
            };

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                    return path;
            }

            return null;
        }
    }
    public class VBoxInit(RunProcess process, VBoxController vm, VmSettings vmset)
    {
        public bool VBoxCheckExist()
        {
            var (output, error) = process.DoCommand($"list vms");
            return output.Contains($"\"{vmset.VmName}\"");
        }

        // removes leftover files that were created upon initialization of a previously used vm
        public void CleanUpFiles()
        {
            if (!VBoxCheckExist())
            {
                string vdiFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "vulnVmSandbox");
                vmset.VdiPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vdi");
                vmset.VBoxPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vbox");

                if (File.Exists(vmset.VdiPath))
                {
                    File.Delete(vmset.VdiPath);
                    process.DoCommand($"closemedium disk \"{vmset.VdiPath}\"");
                    Console.WriteLine("removed leftover .vdi file and closed medium");
                }
                if (File.Exists(vmset.VBoxPath))
                {
                    File.Delete(vmset.VBoxPath);
                    Console.WriteLine("removed leftosver .vbox file");
                }

                vm.DeleteVM();
            }
        }

        // creates/starts the vm
        public void VBoxCreateFromIso(IsoInterface gui)
        {
            if (!VBoxCheckExist())
            {
                CleanUpFiles();

                // redundant feature of the cleanupfile method that involves timing issues during unattended installation -- leaving here as a temporary fix.
                string vdiFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "vulnVmSandbox");
                Directory.CreateDirectory(vdiFolder);
                vmset.VdiPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vdi");
                vmset.VBoxPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vbox");
                process.DoCommand($"createvm --name \"{vmset.VmName}\" --ostype Windows11_64 --register");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --memory {vmset.RamGB} --cpus {vmset.CpuCount}");
                // -- additional modifications to ensure that the vm runs smoothly upon initial setup.
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --firmware efi64");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --tpm-type 2.0");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --graphicscontroller vmsvga");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --vram 128");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --accelerate3d off");
                process.DoCommand($"createhd --filename \"{vmset.VdiPath}\" --size {vmset.StorageGB}");
                process.DoCommand($"storagectl \"{vmset.VmName}\" --name \"SATA\" --add sata");
                process.DoCommand($"storageattach \"{vmset.VmName}\" --storagectl \"SATA\" --port 0 --device 0 --type hdd --medium \"{vmset.VdiPath}\"");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --description \"VulnVM - Created by vulnvm application\"");

                vm.SetupSharedFolder();

                process.DoCommand($"unattended install \"{vmset.VmName}\" " +
                    $"--iso=\"{vmset.IsoPath}\" " +
                    $"--user=\"user\" --password=\"password\" " +
                    $"--install-additions " +
                    $"--post-install-command=\"cmd /c sc config VBoxService start= auto && sc start VBoxService && reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v EnableLUA /t REG_DWORD /d 0 /f && net localgroup Administrators user /add\"");

                vm.StartVM();

                // hack to ensure that the window is focused and clicked into to start the os installation process
                WindowHelper windowHelper = new WindowHelper();
                windowHelper.FocusVmWindow(vmset.VmName);
                process.DoCommand($"controlvm \"{vmset.VmName}\" keyboardputscancode 1c 9c");

                gui.LoadVmList();
                vm.WaitForBoot();
                gui.LoadVmList();
                vm.WaitForGuestControl();
                vm.CopyAgent();
                vm.RegisterAgent();
                vm.TriggerLogonForAgentStart();
                vm.WaitForBoot();
                vm.WaitForGuestControl();
                vm.WaitForAgent();
                vm.SaveSnapshot();
            }
            else
            {
                vm.RestoreSnapShot();
                vm.StartVM();
            }
        }
    }

    public class VBoxController(RunProcess process, VmSettings vmset)
    {
        public void StartVM()
        {
            process.DoCommand($"startvm \"{vmset.VmName}\"");
        }

        public void StopVM()
        {
            process.DoCommand($"controlvm \"{vmset.VmName}\" poweroff");
        }

        // saves a snapshot of the vm to later be called back to. (on initial startup, a snapshot is saved to rollback and changes made by analysis)
        public void SaveSnapshot()
        {
            process.DoCommand($"snapshot \"{vmset.VmName}\" take \"{vmset.SnapshotName}\"");
        }

        public void RestoreSnapShot()
        {
            process.DoCommand($"snapshot \"{vmset.VmName}\" restore \"{vmset.SnapshotName}\"");
        }

        private bool GuestRun(string exe, string argv, out string output, out string error)
        {
            (output, error) = process.DoCommand(
                $"guestcontrol \"{vmset.VmName}\" run " +
                $"--username user --password password -- {argv}");

            return string.IsNullOrWhiteSpace(error) ||
                   !error.Contains("error", StringComparison.OrdinalIgnoreCase);
        }

        private bool GuestResponds()
        {
            bool ok = GuestRun(@"C:\Windows\System32\cmd.exe", "cmd.exe /c echo ready", out var output, out _);
            return ok && output.Split('\n').Any(l => l.Trim().Equals("ready", StringComparison.OrdinalIgnoreCase));
        }

        public void TriggerLogonForAgentStart()
        {
            Console.WriteLine("Restarting vm to trigger launch with elevated token");

            GuestRun(@"C:\Windows\System32\shutdown.exe", "shutdown.exe /r /t 0", out _, out var err);
            if (!string.IsNullOrWhiteSpace(err))
                Console.WriteLine($"Reboot command returned: {err}");

            // wait until the guest actually goes DOWN so WaitForBoot doesn't pass on the pre-reboot session
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline && GuestResponds())
                Thread.Sleep(3000);

            Console.WriteLine("Reboot triggered");
        }

        public void WaitForGuestControl(int timeoutMinutes = 60)
        {
            Console.WriteLine("Waiting for guest control service to be ready");
            var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);
            int attempts = 0;

            while (!GuestResponds())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Guest control never became available.");

                attempts++;
                if (attempts % 12 == 0)
                {
                    var (glOutput, _) = process.DoCommand(
                        $"guestproperty get \"{vmset.VmName}\" /VirtualBox/GuestAdd/Vbgl/Version");
                    Console.WriteLine($"Still waiting - GuestAdd reported version: {glOutput}");
                }
                Console.WriteLine("Guest control not ready, retrying in 5 seconds...");
                Thread.Sleep(5000);
            }
            Console.WriteLine("Guest control service ready");
        }

        public bool IsAgentRunning()
        {
            GuestRun(@"C:\Windows\System32\tasklist.exe",
                     "tasklist.exe /FI \"IMAGENAME eq vulnVMAgent.exe\"",
                     out var output, out _);
            return output.Contains("vulnVMAgent.exe", StringComparison.OrdinalIgnoreCase);
        }

        public void WaitForAgent(int timeoutMinutes = 5)
        {
            Console.WriteLine("Waiting for agent to start...");
            var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);

            while (!IsAgentRunning())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Agent never started in the guest (check Run key / autologon).");
                Thread.Sleep(5000);
            }
            Console.WriteLine("Agent confirmed running");
        }

        public void WaitForBoot(int timeoutMinutes = 60)
        {
            Console.WriteLine("Waiting for guest control to be available...");
            const int requiredStableChecks = 2;
            int stableCount = 0;
            var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);

            while (stableCount < requiredStableChecks)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"Guest did not become available within {timeoutMinutes} minutes.");

                try
                {
                    if (GuestResponds())
                    {
                        stableCount++;
                        Console.WriteLine($"Guest control responding (check {stableCount}/{requiredStableChecks})");
                    }
                    else
                    {
                        stableCount = 0;
                        Console.WriteLine("Guest not yet ready...");
                    }
                }
                catch (Exception ex)
                {
                    stableCount = 0;
                    Console.WriteLine($"Guest control check failed: {ex.Message}");
                }

                if (stableCount < requiredStableChecks)
                    Thread.Sleep(5000);
            }
            Console.WriteLine("Guest control confirmed available - VM ready");
        }

        public void SetupSharedFolder()
        {
            string logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "vulnVmSandbox", "logs"
            );
            Directory.CreateDirectory(logFolder);

            process.DoCommand($"sharedfolder add \"{vmset.VmName}\" " +
                $"--name \"SandboxLogs\" " +
                $"--hostpath \"{logFolder}\" " +
                $"--automount");
        }

        public void CopyAgent()
        {
            string agentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vulnVMAgent.exe");
            if (!File.Exists(agentPath))
                throw new FileNotFoundException("vulnVMAgent.exe not found next to the host executable.", agentPath);

            Console.WriteLine("Copying agent into guest vm");

            // mkdir errors if the folder already exists - that's fine ig
            process.DoCommand($"guestcontrol \"{vmset.VmName}\" mkdir \"C:\\vulnVMAgent\" --username user --password password");
            process.DoCommand($"guestcontrol \"{vmset.VmName}\" mkdir \"C:\\vulnVMAgent\\dropped\" --username user --password password");

            var (_, copyError) = process.DoCommand(
                $"guestcontrol \"{vmset.VmName}\" copyto \"{agentPath}\" \"C:\\vulnVMAgent\\vulnVMAgent.exe\" " +
                $"--username user --password password");

            if (copyError.Contains("error", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Agent copy failed: {copyError}");

            Console.WriteLine("Agent copied successfully");
        }

        // redundant, but ensures that the vm is available on every runnable instance of the vm
        public void RegisterAgent()
        {
            string logPath = @"\\vboxsvr\SandboxLogs\log.txt";
            Console.WriteLine("Registering agent as startup program...");

            bool ok = GuestRun(@"C:\Windows\System32\reg.exe",
                "reg.exe add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" " +
                $"/v vulnVMAgent /t REG_SZ /d \"C:\\vulnVMAgent\\vulnVMAgent.exe {logPath}\" /f",
                out var output, out var error);

            Console.WriteLine($"Register result: {output} {error}");
            if (!ok) throw new InvalidOperationException("Agent registration failed: " + error);
            Console.WriteLine("Agent registered successfully");
        }

        public void DropFileIntoVm(string hostFilePath)
        {
            if (string.IsNullOrEmpty(hostFilePath) || !File.Exists(hostFilePath))
            {
                Console.WriteLine($"DropFileIntoVm: file not found or path empty: {hostFilePath}");
                return;
            }

            string fileName = Path.GetFileName(hostFilePath);
            string guestPath = $"C:\\vulnVMAgent\\dropped\\{fileName}";

            Console.WriteLine($"Dropping file into VM: {fileName}");

            var (copyOutput, copyError) = process.DoCommand(
                $"guestcontrol \"{vmset.VmName}\" copyto \"{hostFilePath}\" \"{guestPath}\" --username user --password password");

            if (!string.IsNullOrEmpty(copyError))
                Console.WriteLine($"DropFileIntoVm FAILED: {copyError}");
            else
                Console.WriteLine($"File dropped successfully: {guestPath}");
        }

        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        public void DeleteVM()
        {
            process.DoCommand("unregistervm \"" + vmset.VmName + "\" --delete");
            Console.WriteLine("VM deleted successfully");
        }
    }
}