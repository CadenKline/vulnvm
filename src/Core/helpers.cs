/*
 * Class to help minimize clutter by making recallable methods used in the gui class & on init startup
 * todo - find a way to reliably focus on the vm window
*/

using System.Runtime.InteropServices;
using vm;

namespace helpers
{
    public class guiHelpers
    {
        public string LastDroppedFile = string.Empty;
        private readonly VBoxController _vmController;

        // initalize vmController
        public guiHelpers(VBoxController vmController)
        {
            _vmController = vmController;
        }

        // helper to make text boxes
        public TextBox CreateNewTextBox(string? args, Point location)
        {
            return new TextBox
            {
                // params and modifiers
                AcceptsReturn = true,
                Text = args,
                Location = location
            };
        }

        // helper to make buttons
        public Button CreateNewButton(string? args, Point location, EventHandler onClick)
        {
            {
                // create a new button with the needed parameters
                var Button = new Button { Text = args, Location = location };
                // make the button recognize the function (runVm, setVmName, setIsoPath)
                Button.Click += onClick;
                return Button;
            };
        }

        // helper to make panels
        public Panel CreateNewPanel(string? args, Point location, Action<string> onFileDropped)
        {
            var panel = new Panel { AllowDrop = true, Location = location, Width = 150, Height = 100, BorderStyle = BorderStyle.FixedSingle, Text = args };
            // add drag and drop functionality
            panel.DragEnter += HandleDragEnter;
            panel.DragDrop += (sender, e) => HandleDragDrop(sender, e, onFileDropped);

            return panel;
        }


        // helper to allow for drag & drop
        public void HandleDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        // helper to get drag & drop data
        public void HandleDragDrop(object? sender, DragEventArgs e, Action<string> onFileDropped)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                // get last file dropped
                LastDroppedFile = files[0];
                onFileDropped(LastDroppedFile);
            }
            else
            {
                LastDroppedFile = string.Empty;
            }
        }

    }
    public class WindowHelper
    {
        // focus on the window to ensure that the vm is started initialized
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ShowWindow(IntPtr hWnd, int nCmdShow);

        public void FocusVmWindow(string? vmName)
        {
            IntPtr hwnd = IntPtr.Zero;

            while (hwnd == IntPtr.Zero)
            {
                hwnd = FindWindow(null, $"{vmName} [Running] - Oracle VM VirtualBox");
                // refer to todo at top
                System.Threading.Thread.Sleep(5000);
            }

            ShowWindow(hwnd, 1); // might have to chagne back to 9
            SetForegroundWindow(hwnd);
        }
    }
}
