/*
 * class that controls the ui portion of vulnvm
 * todo -- add minimums and maximums to integer related variables (storage, ram, cpu). create sliders for integer related 
 * variables rather than manually typing them (or have both)
*/

using helpers;
using vm;
using System.Windows.Forms.Design;

namespace gui
{
    public class IsoInterface(Form interfaceState, VmSettings vmset, guiHelpers helper, RunProcess process, VBoxController vm)
    {
        TextBox VmNameTextBox = new TextBox();
        TextBox StorageTextBox = new TextBox();
        TextBox RamTextBox = new TextBox();
        TextBox CpuTextBox = new TextBox();
        Label IsoStatusLabel = new Label();
        Panel IsoDragPanel = new Panel();
        Button BrowseIsoButton = new Button();
        Button OpenLogs = new Button();
        Button CreateVmButton = new Button();
        ListBox VmListBox = new ListBox();
        Button StopVmButton = new Button();
        Panel VmFileDropPanel = new Panel();

        readonly List<string> _vmNames = new List<string>();

        public void InitializeComponent()
        {
=           interfaceState.Text = "vulnVM";
            interfaceState.AutoScroll = true;

            var sectionNew = new Label {Text = "New VM", AutoSize = true, Location = new Point(20, 20)};
            var nameLabel = new Label {Text = "Name", AutoSize = true, Location = new Point(20, 38)};
            var storageLabel = new Label {Text = "Storage (GB)", AutoSize = true, Location = new Point(172, 38)};
            var ramLabel = new Label {Text = "RAM (GB)", AutoSize = true, Location = new Point(274, 38)};
            var cpuLabel = new Label { Text = "CPUs", AutoSize = true, Location = new Point(376, 38) };

            VmNameTextBox = new TextBox {PlaceholderText = "ostextbox", Location = new Point(20, 60), Width = 140};
            StorageTextBox = new TextBox {PlaceholderText = "80", Location = new Point(172, 60), Width = 90};
            RamTextBox = new TextBox {PlaceholderText = "4", Location = new Point(274, 60), Width = 90};
            CpuTextBox = new TextBox { PlaceholderText = "4", Location = new Point(376, 60), Width = 70 };

            var isoLabel = new Label {Text = "ISO image", AutoSize = true, Location = new Point(20, 95)};

            IsoDragPanel = helper.CreateNewPanel(null, new Point(20, 120), path =>
            {
                vmset.IsoPath = path;
                IsoStatusLabel.Text = Path.GetFileName(path);
            });

            IsoDragPanel.Size = new Size(360, 40);
            IsoDragPanel.BackColor = SystemColors.Control;
            IsoDragPanel.BorderStyle = BorderStyle.FixedSingle;

            BrowseIsoButton = new Button {Text = "Browse…", Location = new Point(390, 120), Size = new Size(80, 40)};
            BrowseIsoButton.Click += BrowseIso;

            IsoStatusLabel = new Label {Text = "No ISO selected", AutoSize = true, Location = new Point(20, 170)};

            CreateVmButton = new Button {Text = "Create and run VM", Location = new Point(20, 200), Size = new Size(148, 26)};
            CreateVmButton.Click += RunVm;

            OpenLogs = new Button {Text = "Logs", AutoSize = true, Location = new Point(478, 60), Width=70};
            OpenLogs.Click += openLog;

            var divider1 = new Panel {Location = new Point(20, 240), Size = new Size(560, 1), BackColor = SystemColors.ControlDark};

            var sectionVms = new Label {Text = "VMs  —  double-click to boot", AutoSize = true, Location = new Point(20, 250)};

            VmListBox = new ListBox
            {
                Location = new Point(20, 275),
                Size = new Size(560, 140),
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            VmListBox.DoubleClick += BootVm;

            StopVmButton = new Button {Text = "Stop selected VM", Location = new Point(20, 425), Size = new Size(148, 26)};
            StopVmButton.Click += StopVm;

            var divider2 = new Panel {Location = new Point(20, 465), Size = new Size(560, 1), BackColor = SystemColors.ControlDark};

            var sectionDrop = new Label {Text = "Inject file into running VM", AutoSize = true, Location = new Point(20, 475)};

            VmFileDropPanel = helper.CreateNewPanel(null, new Point(20, 500), path =>
            {
                VBoxInit init = new VBoxInit(process, vm, vmset);
                if (!init.VBoxCheckExist())
                {
                    MessageBox.Show("No VM is running. Boot one from the list above first.", "VM not ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                vm.DropFileIntoVm(path);
            });
            VmFileDropPanel.Size = new Size(560, 40);
            VmFileDropPanel.BackColor = SystemColors.Control;
            VmFileDropPanel.BorderStyle = BorderStyle.FixedSingle;

            interfaceState.Controls.AddRange(new Control[]
            {
                sectionNew,
                nameLabel, storageLabel, ramLabel, cpuLabel,
                VmNameTextBox, StorageTextBox, RamTextBox, CpuTextBox,
                isoLabel,
                IsoDragPanel, BrowseIsoButton,
                IsoStatusLabel,
                CreateVmButton,
                divider1,
                sectionVms,
                VmListBox,
                StopVmButton,
                divider2,
                sectionDrop,
                VmFileDropPanel,
                OpenLogs
            });

            LoadVmList();
        }

        void LoadVmList()
        {
            VmListBox.Items.Clear();
            _vmNames.Clear();

            var (output, _) = process.DoCommand("list vms");
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"")) continue;
                int closeQuote = trimmed.IndexOf('"', 1);
                if (closeQuote < 0) continue;
                string name = trimmed.Substring(1, closeQuote - 1);
                _vmNames.Add(name);
                VmListBox.Items.Add(name);
            }
        }

        void BrowseIso(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select a Windows ISO",
                Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                vmset.IsoPath = dlg.FileName;
                IsoStatusLabel.Text = Path.GetFileName(dlg.FileName);
            }
        }

        void openLog(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "C:\\vulnVMAgent\\log.txt",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void RunVm(object? sender, EventArgs e)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(VmNameTextBox.Text)) missing.Add("VM name");
            if (!int.TryParse(StorageTextBox.Text, out int storage) || storage <= 0) missing.Add("Storage (GB)");
            if (!int.TryParse(RamTextBox.Text, out int ram) || ram <= 0) missing.Add("RAM (GB)");
            if (!int.TryParse(CpuTextBox.Text, out int cpus) || cpus <= 0) missing.Add("CPUs");
            if (string.IsNullOrWhiteSpace(vmset.IsoPath)) missing.Add("ISO image");

            if (missing.Count > 0)
            {
                MessageBox.Show(
                    "Fill in the following before creating a VM:\n\n• " + string.Join("\n• ", missing),
                    "Missing information",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            vmset.VmName = VmNameTextBox.Text.Trim();
            vmset.StorageGB = storage * 1024;
            vmset.RamGB = ram * 1024;
            vmset.CpuCount = cpus;

            VBoxInit init = new VBoxInit(process, vm, vmset);
            Task.Run(() => init.VBoxCreateFromIso());

            LoadVmList();
        }

        void BootVm(object? sender, EventArgs e)
        {
            if (VmListBox.SelectedIndex < 0) return;

            string name = _vmNames[VmListBox.SelectedIndex];
            vmset.VmName = name;

            VBoxInit init = new VBoxInit(process, vm, vmset);
            if (!init.VBoxCheckExist())
            {
                MessageBox.Show($"\"{name}\" was not found in VirtualBox.", "VM not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadVmList();
                return;
            }

            // NOTE: the controller also performs these operations as a fallback for compatibility with older program versions
            vm.RestoreSnapShot();
            vm.StartVM();
        }

        void StopVm(object? sender, EventArgs e)
        {
            if (VmListBox.SelectedIndex < 0)
            {
                MessageBox.Show("Select a VM from the list first.", "No selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = _vmNames[VmListBox.SelectedIndex];
            if (MessageBox.Show($"Power off \"{name}\"?", "Stop VM", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                vmset.VmName = name;
                vm.StopVM();
            }
        }

        public void startGUI()
        {
            interfaceState.Height = 620;
            interfaceState.Width = 624;
            InitializeComponent();
            Application.EnableVisualStyles();
            Application.Run(interfaceState);
        }
    }
}
