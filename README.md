# VulnVM
An application that automates VirtualBox VM provisioning via VBoxManage, creating isolated sandbox environments on demand for malware analysis.

Created by Caden Kline.

# Requirements
* Windows Host
* VirtualBox installed and VBoxManage on your PATH
* A Windows 11 .iso file
* .NET 8 SDK

# Features
* Automated VM provisioning via VBoxManage,
* Unattended Windows Installation with predefined configuration,
* Built-in agent deployment pipeline with built-in detection measures,
* Snapshot creation and restoration for repeatable sample analysis,
* Host-to-VM file transfer support for test samples

# Building
donet build

The agent is published as self-contained single-file .exe and is copied into the build output automatically as part of the build.

# Usage
1. Launch vulnVM.exe
2. Enter a VM name, storage size, RAM, and CPU count
3. Drop a Win 11 .iso file onto the drop zone or manually search for it
4. Click Create and run VM. Provisioning runs fully automatically and takes roughly 15-30 minutes depending on your systems hardware
5. Once setup is complete, a clean snapshot is saved and the VM list updates
6. Double click and VM in the list to restore its snapshot and boot it up
7. To test a file for analysis, drag it onto the inject file drop zone while a VM is running. The agent will automatically detect it and log any processes it spawns.

* Logs are written to USERPROFILE\Documents\vulnVmSandbox\logs\log.txt
Each entry is timestamped. Processes spawned by a dropped file are tagged with the originating filename so you can trace the sample

# Notes
This application is intended to run isolated sandbox environments locally. This application is NOT responsible for any damage done to a local machine.

* The guest account credentials are intentionally simple for an isolated sandbox. DO NOT expose the VM to a network.
* The VM snapshot name defaults to a fixed value, if you delete or rename snapshots manually in VirtualBox, the boot-from-list feature may not work as expected.
* This tool is Windows only on both the host and guest side.

# Status
Work in progress. Known issues and planned improvements are tracked in the repo.



