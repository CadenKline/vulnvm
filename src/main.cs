/*
 * the main class of vulnVM!
*/

using vm;
using gui;
using helpers;

public class vulnVM()
{
    [STAThread]
    static void Main(string[] args)
    {
        Form interfaceState = new Form();
        RunProcess process = new RunProcess();
        VmSettings vmset = new VmSettings();
        VBoxController vm = new VBoxController(process, vmset);
        guiHelpers helper = new guiHelpers(vm);
        VBoxInit init = new VBoxInit(process, vm, vmset);
        IsoInterface isoInter = new IsoInterface(interfaceState, vmset, helper, process, vm);

        isoInter.startGUI();
    }
}

