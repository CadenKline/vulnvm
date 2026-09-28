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
        public guiHelpers(VBoxController vmController)
        {
            _vmController = vmController;
        }

        public TextBox CreateNewTextBox(string? args, Point location)
        {
            return new TextBox
            {
                AcceptsReturn = true,
                Text = args,
                Location = location
            };
        }

        public Button CreateNewButton(string? args, Point location, EventHandler onClick)
        {
            {
                var Button = new Button { Text = args, Location = location };
                Button.Click += onClick;
                return Button;
            };
        }

        public Panel CreateNewPanel(string? args, Point location, Action<string> onFileDropped)
        {
            var panel = new Panel { AllowDrop = true, Location = location, Width = 150, Height = 100, BorderStyle = BorderStyle.FixedSingle, Text = args };
            panel.DragEnter += HandleDragEnter;
            panel.DragDrop += (sender, e) => HandleDragDrop(sender, e, onFileDropped);

            return panel;
        }

        public void HandleDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        public void HandleDragDrop(object? sender, DragEventArgs e, Action<string> onFileDropped)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
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
