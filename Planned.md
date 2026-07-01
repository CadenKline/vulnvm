# Known issues
* runVm runs on the UI thread, causing the window to freeze for the entire duration of VM provisioning. Needs to move to a background thread with progress reported back to the form.
* WaitForBoot, WaitForGuestControl, and WaitForAgent loop indefinitely with no timeout. A hung or crashed VM will lock the process permanently with no way to recover short of killing it.
* Booting a VM from the list after restarting the app fails silently because SnapshotName is never repopulated from VirtualBox, its only set during the original provisioning run.
* The VM list does not reflect running state, there is no visual distinction between a VM that is currently running and one that is powered off.


# Intended updates
* Move VM provisioning off the UI thread and add a progress indicator so the form stays responsive during setup.
* Add timeouts to all polling loops (WaitForBoot, WaitForGuestControl, WaitForAgent) with proper error surfacing to the UI when something goes wrong.
* Persist snapshot names alongside VM names so the boot-from-list feature works correctly after an app restart.
* Make guest credentials (user / password) configurable rather than hardcoded throughout the codebase.
* Show running/stopped state in the VM list and refresh it automatically when a VM is started or stopped.
* Add a way to view the agent log (log.txt) directly from the UI without opening it manually in a file explorer.
* Expand agent monitoring to include registry changes and file system events in addition to process starts.
* Add network connection logging to the agent so outbound connections made by a dropped file are captured.


# Longer term
Linux host support. The core provisioning logic talks to VirtualBox exclusively through VBoxManage, which is cross-platform, 
so the main blockers are the WinForms UI (would need to be replaced or wrapped) and a handful of Windows-specific helpers in helpers.cs
(user32.dll P/Invoke for window focusing). The agent itself is Windows-only by design since it targets a Windows guest, but the host-side 
tooling could reasonably support Linux with those changes.
