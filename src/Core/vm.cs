/*
 * Class that runs basic functions of the virtual machine and the agent deployment pipeline
 * Refer to the actual agent functionality in agent.cs
*/

using System.Diagnostics;
using helpers;

namespace vm
{
    // init variables that will be used to create the vm 
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

    // helper to point to VBoxManage as a callable method rather than manually typing out VBoxManage commands
    public class RunProcess
    {
        public (string output, string error) DoCommand(string args)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    // identitifies VBoxManage and its params
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
    }

    // class to handle the initial creation of the vm and take care of any remaining files
    // that were deleted during previous use.
    public class VBoxInit(RunProcess process, VBoxController vm, VmSettings vmset)
    {
        // checks VirtualBox to see if the vm with the name specified by the user is already created
        public bool VBoxCheckExist()
        {
            var (output, error) = process.DoCommand($"list vms");
            return output.Contains($"\"{vmset.VmName}\"");
        }

        // removes leftover files that were created upon initialization of a previously used vm
        public void CleanUpFiles()
        {
            // checks to see if the name provided is not in virtualboxes directory
            if (!VBoxCheckExist())
            {
                // moves the virtual disk and configuration files into the users documents directory
                string vdiFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "vulnVmSandbox");
                vmset.VdiPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vdi");
                vmset.VBoxPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vbox");

                // removes the vdiFolder IF there was a vm that was previously used with the same name the user is currently attempting to enter
                if (Directory.Exists(vdiFolder))
                {
                    Directory.Delete(vdiFolder, true);
                    Console.WriteLine("Removed leftover sandbox files");
                }
                // removes the vdiPath IF there was a vm that was previously used with the same name the user is currently attempting to enter
                if (File.Exists(vmset.VdiPath))
                {
                    File.Delete(vmset.VdiPath);
                    // ensures that the medium created by the previous vm is properly removed/closed
                    process.DoCommand($"closemedium disk \"{vmset.VdiPath}\"");
                    Console.WriteLine("removed leftover .vdi file and closed medium");
                }
                // removes the leftover .vox file IF there was a vm that was previously used with the same name the user is currently attempting to enter
                if (File.Exists(vmset.VBoxPath))
                {
                    File.Delete(vmset.VBoxPath);
                    Console.WriteLine("removed leftover .vbox file");
                }
            }
        }

        // creates/starts the vm
        public void VBoxCreateFromIso()
        {
            if (!VBoxCheckExist())
            {
                // cleans up leftover files if the user is attempting to enter the name of a vm that was previously in use
                CleanUpFiles();

                // redundant feature of the cleanupfile method that involves timing issues during unattended installation -- leaving here as a temporary fix.
                string vdiFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "vulnVmSandbox");
                Directory.CreateDirectory(vdiFolder);
                vmset.VdiPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vdi");
                vmset.VBoxPath = Path.Combine(vdiFolder, $"{vmset.VmName}.vbox");

                // creates the vm using the specified variables entered by the user
                process.DoCommand($"createvm --name \"{vmset.VmName}\" --ostype Windows11_64 --register");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --memory {vmset.RamGB} --cpus {vmset.CpuCount}");
                // -- additional modifications to ensure that the vm runs smoothly upon initial setup.
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --firmware efi64");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --tpm-type 2.0");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --graphicscontroller vmsvga");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --vram 128");
                process.DoCommand($"modifyvm \"{vmset.VmName}\" --accelerate3d off");
                // creates and attaches the storage medium to the vm
                process.DoCommand($"createhd --filename \"{vmset.VdiPath}\" --size {vmset.StorageGB}");
                process.DoCommand($"storagectl \"{vmset.VmName}\" --name \"SATA\" --add sata");
                process.DoCommand($"storageattach \"{vmset.VmName}\" --storagectl \"SATA\" --port 0 --device 0 --type hdd --medium \"{vmset.VdiPath}\"");

                // creates the folder responsible for future agent setup and for host to os file drop features
                vm.SetupSharedFolder();

                // installs the iso image and guest additions for the os
                process.DoCommand($"unattended install \"{vmset.VmName}\" " +
                    $"--iso=\"{vmset.IsoPath}\" " +
                    $"--user=\"user\" --password=\"password\" " +
                    $"--install-additions " +
                    $"--post-install-command=\"cmd /c sc config VBoxService start= auto && sc start VBoxService && reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v EnableLUA /t REG_DWORD /d 0 /f && net localgroup Administrators user /add\"");

                // starts the vm
                vm.StartVM();

                // hack to ensure that the window is focused and clicked into to start the os installation process
                WindowHelper windowHelper = new WindowHelper();
                windowHelper.FocusVmWindow(vmset.VmName);
                process.DoCommand($"controlvm \"{vmset.VmName}\" keyboardputscancode 1c 9c");

                // methods to hook the agent onto the vm
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
                // restores the vm and starts it
                vm.RestoreSnapShot();
                vm.StartVM();
            }

        }
    }

    // controller to help with basic vm functionality
    public class VBoxController(RunProcess process, VmSettings vmset)
    {
        // starts the vm with the specified name
        public void StartVM()
        {
            process.DoCommand($"startvm \"{vmset.VmName}\"");
        }

        // stops the vm with the specified name
        public void StopVM()
        {
            process.DoCommand($"controlvm \"{vmset.VmName}\" poweroff");
        }

        // saves a snapshot of the vm to later be called back to. (on initial startup, a snapshot is saved to rollback and changes made by analysis)
        public void SaveSnapshot()
        {
            process.DoCommand($"snapshot \"{vmset.VmName}\" take \"{vmset.SnapshotName}\"");
        }

        // restores the snapshot created by the vm on initial startup.
        public void RestoreSnapShot()
        {
            process.DoCommand($"snapshot \"{vmset.VmName}\" restore \"{vmset.SnapshotName}\"");
        }

        // reboots the vm once the agent is registered to ensure that it runs with an elevated token and is not denied access based off of permissions.
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

        // method to wait for the os to FULLY reboot before calling any other functions
        public void WaitForGuestControl()
        {
            Console.WriteLine("Waiting for guest control service to be ready");
            bool ready = false;
            int attempts = 0;

            // loops until the guest control service is ready
            while (!ready)
            {
                var (output, error) = process.DoCommand($"guestcontrol \"{vmset.VmName}\" run " +
                    $"--exe \"C:\\Windows\\System32\\cmd.exe\" " +
                    $"--username user --password password " +
                    $"-- /c exit");

                // checks to ensure that the guest control service is ready by reading for the not ready message contained in error
                if (!error.Contains("not ready"))
                {
                    ready = true;
                    Console.WriteLine("Guest control service ready");
                }
                // loops until the guest control service is ready
                else
                {
                    attempts++;
                    if (attempts % 12 == 0) // every 60~ seconds
                    {
                        Console.WriteLine("Still waiting - checking the guest additions run level");
                        var (glOutput, _) = process.DoCommand($"guestproperty get \"{vmset.VmName}\" /VirtualBox/GuestAdd/Vbgl/Version");
                        Console.WriteLine($"GuestAdd reported version: {glOutput}");
                    }
                    // checks to see if the guest control service is not ready every 5 seconds
                    Console.WriteLine("Guest control not ready, retrying in 5 seconds...");
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }

        // method to check if the agent is running in the vm using its .exe label
        public bool IsAgentRunning()
        {
            var (output, error) = process.DoCommand($"guestcontrol \"{vmset.VmName}\" run --exe \"C:\\Windows\\System32\\tasklist.exe\" --username user --password password");
            return output.Contains("vulnVMAgent.exe");
        }

        // waits for the agent to run before doing any other tasks. 
        public void WaitForAgent()
        {
            Console.WriteLine("Waiting for agent to start...");
            while (!IsAgentRunning())
            {
                System.Threading.Thread.Sleep(5000);
            }
            Console.WriteLine("Agent confirmed running");
        }

        // method to ensure that the vm is booted into the os FULLY
        public void WaitForBoot()
        {
            Console.WriteLine("Waiting for vm to boot...");
            int stableCount = 0;
            const int requiredStableChecks = 5;

            while (stableCount < requiredStableChecks)
            {
                // checks to see if the os is booted by checking for registered logged in users on the os
                var (output, error) = process.DoCommand($"guestproperty get \"{vmset.VmName}\" /VirtualBox/GuestInfo/OS/LoggedInUsers");

                if (output.Contains("Value:") && !output.Contains("Value: 0"))
                {
                    stableCount++;
                }
                // checks to see if TriggerLogonForAgentStart is currently being ran, stops all functions until it is detected as no longer running
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

        // creates the directory that will be used to extract log information from the agent
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

        // copies the agent onto the vm
        public void CopyAgent()
        {
            string agentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vulnVMAgent.exe");
            Console.WriteLine("Copying agent into guest vm");

            // creates a directory named "vulnVMAgent" with the .exe file in it
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

            // registers the agent to start up on boot of the vm using HKCU sids
            var (output, error) = process.DoCommand($"guestcontrol \"{vmset.VmName}\" run " +
                $"--exe \"C:\\Windows\\System32\\reg.exe\" " +
                $"--username user --password password " +
                $"-- add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" " +
                $"/v vulnVMAgent /t REG_SZ /d \"C:\\vulnVMAgent\\vulnVMAgent.exe {logPath}\" /f");

            // checks to see if the agent was registered onto the vm successfully or not
            Console.WriteLine($"Register result: {output} {error}");
            if (!string.IsNullOrEmpty(error))
                Console.WriteLine("Agent registration may have failed");
            else
                Console.WriteLine("Agent registered successfully");
        }

        // method that allows for files to be dropped into the vm for analysis
        public void DropFileIntoVm(string hostFilePath)
        {
            // checks to see if the file is found on the host machine
            if (string.IsNullOrEmpty(hostFilePath) || !File.Exists(hostFilePath))
            {
                Console.WriteLine($"DropFileIntoVm: file not found or path empty: {hostFilePath}");
                return;
            }

            // gets the original file name and stores it in the dropped folder found in vulnVMAgent
            string fileName = Path.GetFileName(hostFilePath);
            string guestPath = $"C:\\vulnVMAgent\\dropped\\{fileName}";

            Console.WriteLine($"Dropping file into VM: {fileName}");

            // puts the file onto the vm
            var (copyOutput, copyError) = process.DoCommand(
                $"guestcontrol \"{vmset.VmName}\" copyto \"{hostFilePath}\" \"{guestPath}\" --username user --password password");

            // checks to see if the file was successfully copied to the guest vm
            if (!string.IsNullOrEmpty(copyError))
                Console.WriteLine($"DropFileIntoVm FAILED: {copyError}");
            else
                Console.WriteLine($"File dropped successfully: {guestPath}");
        }
    }
}