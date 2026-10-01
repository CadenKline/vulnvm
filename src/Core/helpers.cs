/*
 * Class to help minimize clutter by making recallable methods used in the gui class & on init startup
 * todo - find a way to reliably focus on the vm window
*/

using System.Runtime.InteropServices;
using System.Text;
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder title, int size);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public void FocusVmWindow(string? vmName)
        {
            IntPtr hwnd = IntPtr.Zero;
            int timeout = 0;
            const int maxTimeout = 120;

            while (hwnd == IntPtr.Zero && timeout < maxTimeout)
            {
                Console.WriteLine($"Searching for VM window matching: '{vmName}'");

                hwnd = FindWindow(null, $"{vmName} [Running] - Oracle VirtualBox");

                if (hwnd == IntPtr.Zero)
                {
                    List<string> allVirtualBoxWindows = new List<string>();
                    EnumWindows((hWnd, lParam) =>
                    {
                        StringBuilder title = new StringBuilder(256);
                        GetWindowText(hWnd, title, 256);
                        string windowTitle = title.ToString();

                        if (windowTitle.Contains("VirtualBox") && windowTitle.Contains(vmName))
                        {
                            allVirtualBoxWindows.Add(windowTitle);
                            Console.WriteLine($"Found matching window: '{windowTitle}'");
                            hwnd = hWnd;
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                if (hwnd == IntPtr.Zero)
                {
                    Console.WriteLine($"Window not found yet, waiting... (attempt {timeout / 5 + 1})");
                    System.Threading.Thread.Sleep(5000);
                    timeout += 5;
                }
            }

            if (hwnd == IntPtr.Zero)
            {
                Console.WriteLine($"WARNING: Could not find VM window after {maxTimeout} seconds. Continuing anyway...");
                return;
            }

            Console.WriteLine("VM window found, setting focus");
            ShowWindow(hwnd, 1);
            SetForegroundWindow(hwnd);
        }
    }
}
