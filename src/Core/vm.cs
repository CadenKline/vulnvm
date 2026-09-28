/*
 * Class that runs basic functions of the virtual machine and the agent deployment pipeline
 * Refer to the actual agent functionality in agent.cs
*/

using System.Diagnostics;
using vulnAgent;
using helpers;

namespace vm
{
    public class VmSettings
    {
        public string IsoPath { get; set; }
        public string VmName { get; set; }
        public string SnapshotName { get; set; }
        public string VdiPath { get; set; }
        public string VBoxPath { get; set; }
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
                {                    FileName = "VBoxManage",
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

                if (Directory.Exists(vdiFolder))
                {
                    TryDeleteDirectory(vdiFolder);
                }
                if (File.Exists(vmset.VdiPath))
                {
                    File.Delete(vmset.VdiPath);
                    process.DoCommand($"closemedium disk \"{vmset.VdiPath}\"");
                    Console.WriteLine("removed leftover .vdi file and closed medium");
                }
                if (File.Exists(vmset.VBoxPath))
                {
                    File.Delete(vmset.VBoxPath);
                    Console.WriteLine("removed leftover .vbox file");
                }
            }
        }
        private void TryDeleteDirectory(string path)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 500;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var cmd = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rmdir /s /q \"{path}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardError = true
                        }
                    };
                    cmd.Start();
                    cmd.WaitForExit();

                    if (cmd.ExitCode == 0 || !Directory.Exists(path))
                    {
                        Console.WriteLine("Removed leftover sandbox files");
                        return;
                    }
                }
                catch { }

                if (i < maxRetries - 1)
                    System.Threading.Thread.Sleep(retryDelayMs);
            }
        }

        // creates/starts the vm
        public void VBoxCreateFromIso()
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

                vm.WaitForBoot();
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

        public void TriggerLogonForAgentStart()
        {
            Console.WriteLine("Restarting vm to trigger launch with elevated token");

            // user and password are the defaults for admin control
            process.DoCommand($"guestcontrol \"{vmset.VmName}\" run " +
                $"--exe \"C:\\Windows\\System32\\shutdown.exe\" " +
                $"--username user --password password " +
                $"-- /r /t 0");

            Console.WriteLine("Reboot triggered");
        }

        public void WaitForGuestControl()
        {
            Console.WriteLine("Waiting for guest control service to be ready");
            bool ready = false;
            int attempts = 0;

            while (!ready)
            {
                var (output, error) = process.DoCommand($"guestcontrol \"{vmset.VmName}\" run " +
                    $"--exe \"C:\\Windows\\System32\\cmd.exe\" " +
                    $"--username user --password password " +
                    $"-- /c exit");

                if (!error.Contains("not ready"))
                {
                    ready = true;
                    Console.WriteLine("Guest control service ready");
                }
                else
                {
                    attempts++;
                    if (attempts % 12 == 0) // every 60~ seconds
                    {
                        Console.WriteLine("Still waiting - checking the guest additions run level");
                        var (glOutput, _) = process.DoCommand($"guestproperty get \"{vmset.VmName}\" /VirtualBox/GuestAdd/Vbgl/Version");
                        Console.WriteLine($"GuestAdd reported version: {glOutput}");
                    }
                    Console.WriteLine("Guest control not ready, retrying in 5 seconds...");
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }
        public bool IsAgentRunning()
        {
            var (output, error) = process.DoCommand($"guestcontrol \"{vmset.VmName}\" run --exe \"C:\\Windows\\System32\\tasklist.exe\" --username user --password password");
            return output.Contains("vulnVMAgent.exe");
        }

        public void WaitForAgent()
        {
            Console.WriteLine("Waiting for agent to start...");
            while (!IsAgentRunning())
            {
                System.Threading.Thread.Sleep(5000);
            }
            Console.WriteLine("Agent confirmed running");
        }

        public void WaitForBoot()
        {
            Console.WriteLine("Waiting for vm to boot...");
            int stableCount = 0;
            const int requiredStableChecks = 5;

            while (stableCount < requiredStableChecks)
            {
                var (output, error) = process.DoCommand($"guestproperty get \"{vmset.VmName}\" /VirtualBox/GuestInfo/OS/LoggedInUsers");

                if (output.Contains("Value:") && !output.Contains("Value: 0"))
                {
                    stableCount++;
                }
                else
                {
                    if (stableCount > 0)
                        Console.WriteLine("Login state dropped — likely a reboot in progress, resetting stability counter");
                    stableCount = 0;
                }

                System.Threading.Thread.Sleep(5000);
            }

            Console.WriteLine("Guest OS boot confirmed stable");
            System.Threading.Thread.Sleep(15000);
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
            Console.WriteLine("Copying agent into guest vm");

            var (mkdirOutput, mkdirError) = process.DoCommand(
                $"guestcontrol \"{vmset.VmName}\" mkdir \"C:\\vulnVMAgent\" --username user --password password");
            Console.WriteLine($"mkdir result: {mkdirOutput} {mkdirError}");

            // copies the agent into the directory
            var (copyOutput, copyError) = process.DoCommand(
                $"guestcontrol \"{vmset.VmName}\" copyto \"{agentPath}\" \"C:\\vulnVMAgent\\vulnVMAgent.exe\" --username user --password password");
            Console.WriteLine($"copyto result: {copyOutput} {copyError}");

            Console.WriteLine("Agent copied successfully");
        }

        // redundant, but ensures that the vm is available on every runnable instance of the vm
        public void RegisterAgent()
        {

            string logPath = @"\\vboxsvr\SandboxLogs\log.txt";
            Console.WriteLine("Registering agent as startup program...");

            var (output, error) = process.DoCommand($"guestcontrol \"{vmset.VmName}\" run " +
                $"--exe \"C:\\Windows\\System32\\reg.exe\" " +
                $"--username user --password password " +
                $"-- add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" " +
                $"/v vulnVMAgent /t REG_SZ /d \"C:\\vulnVMAgent\\vulnVMAgent.exe {logPath}\" /f");

            Console.WriteLine($"Register result: {output} {error}");
            if (!string.IsNullOrEmpty(error))
                Console.WriteLine("Agent registration may have failed");
            else
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
    }
}