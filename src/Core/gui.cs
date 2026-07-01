/*
 * class that controls the ui portion of vulnvm
 * todo -- add minimums and maximums to integer related variables (storage, ram, cpu). create sliders for integer related 
 * variables rather than manually typing them (or have both)
*/

using helpers;
using vm;

namespace gui
{
    public class IsoInterface(Form interfaceState, VmSettings vmset, guiHelpers helper, RunProcess process, VBoxController vm)
    {
        // initalizes all needed user interactive features
        TextBox VmNameTextBox = new TextBox();
        TextBox StorageTextBox = new TextBox();
        TextBox RamTextBox = new TextBox();
        TextBox CpuTextBox = new TextBox();
        Label IsoStatusLabel = new Label();
        Panel IsoDragPanel = new Panel();
        Button BrowseIsoButton = new Button();
        Button CreateVmButton = new Button();
        ListBox VmListBox = new ListBox();
        Button StopVmButton = new Button();
        Panel VmFileDropPanel = new Panel();

        // list of all available vms that are in the users virtualbox directory
        readonly List<string> _vmNames = new List<string>();

        // places the interactable features/ non-interactable features onto the ui for user interaction
        public void InitializeComponent()
        {
            // sets the name of the ui
            interfaceState.Text = "vulnVM";
            interfaceState.AutoScroll = true;

            // this is the portion of the ui that is primarily used by the user to create the vm to their liking
            var sectionNew = new Label {Text = "New VM", AutoSize = true, Location = new Point(20, 20)};

            var nameLabel = new Label {Text = "Name", AutoSize = true, Location = new Point(20, 38)};
            var storageLabel = new Label {Text = "Storage (GB)", AutoSize = true, Location = new Point(172, 38)};
            var ramLabel = new Label {Text = "RAM (GB)", AutoSize = true, Location = new Point(274, 38)};
            var cpuLabel = new Label {Text = "CPUs", AutoSize = true, Location = new Point(376, 38)};

            VmNameTextBox = new TextBox {PlaceholderText = "ostextbox", Location = new Point(20, 60), Width = 140};
            StorageTextBox = new TextBox {PlaceholderText = "80", Location = new Point(172, 60), Width = 90};
            RamTextBox = new TextBox {PlaceholderText = "4", Location = new Point(274, 60), Width = 90};
            CpuTextBox = new TextBox {PlaceholderText = "4", Location = new Point(376, 60), Width = 70};

            // creates the iso image drag and drop feature to create ease of installation
            var isoLabel = new Label {Text = "ISO image", AutoSize = true, Location = new Point(20, 95)};

            IsoDragPanel = helper.CreateNewPanel(null, new Point(20, 120), path =>
            {
                vmset.IsoPath = path;
                IsoStatusLabel.Text = Path.GetFileName(path);
            });

            // renders a box around the interactable drop field
            IsoDragPanel.Size = new Size(360, 40);
            IsoDragPanel.BackColor = SystemColors.Control;
            IsoDragPanel.BorderStyle = BorderStyle.FixedSingle;

            // accessibility feature if the user does not want to drag and drop the .iso file
            BrowseIsoButton = new Button {Text = "Browse…", Location = new Point(390, 120), Size = new Size(80, 40)};
            BrowseIsoButton.Click += BrowseIso;

            // base label for when an iso image is not used NOTE: if the user already has a vm created, than this can remain as-is
            IsoStatusLabel = new Label {Text = "No ISO selected", AutoSize = true, Location = new Point(20, 170)};

            // runs the vm from the ui itself rather in virtualbox
            CreateVmButton = new Button {Text = "Create and run VM", Location = new Point(20, 200), Size = new Size(148, 26)};
            CreateVmButton.Click += RunVm;

            var divider1 = new Panel {Location = new Point(20, 240), Size = new Size(560, 1), BackColor = SystemColors.ControlDark};

            var sectionVms = new Label {Text = "VMs  —  double-click to boot", AutoSize = true, Location = new Point(20, 250)};

            // displays the list of ALL available virtualbox vms on the host machine
            VmListBox = new ListBox
            {
                Location = new Point(20, 275),
                Size = new Size(560, 140),
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            VmListBox.DoubleClick += BootVm;

            // stops the vm that is selected in the list
            StopVmButton = new Button {Text = "Stop selected VM", Location = new Point(20, 425), Size = new Size(148, 26)};
            StopVmButton.Click += StopVm;

            var divider2 = new Panel {Location = new Point(20, 465), Size = new Size(560, 1), BackColor = SystemColors.ControlDark};

            var sectionDrop = new Label {Text = "Inject file into running VM", AutoSize = true, Location = new Point(20, 475)};

            // panel that allows the user to drop samples from the host machine to the vm for analysis
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
                VmFileDropPanel
            });

            LoadVmList();
        }

        // method to load the vm list and display them individually
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

        // method to enable the user to find their .iso image and use it
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

        void RunVm(object? sender, EventArgs e)
        {
            var missing = new List<string>();

            // checks to see what needed entries the user is missing and appends them to the missing list
            if (string.IsNullOrWhiteSpace(VmNameTextBox.Text)) missing.Add("VM name");
            if (!int.TryParse(StorageTextBox.Text, out int storage) || storage <= 0) missing.Add("Storage (GB)");
            if (!int.TryParse(RamTextBox.Text, out int ram) || ram <= 0) missing.Add("RAM (GB)");
            if (!int.TryParse(CpuTextBox.Text, out int cpus) || cpus <= 0) missing.Add("CPUs");
            if (string.IsNullOrWhiteSpace(vmset.IsoPath)) missing.Add("ISO image");

            // displays a message saying what parameters the user is missing IF there are any
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
            // converts the ram and storage from gb to mb 
            vmset.StorageGB = storage * 1024;
            vmset.RamGB = ram * 1024;
            vmset.CpuCount = cpus;

            // creates the vm with the specified parameters
            VBoxInit init = new VBoxInit(process, vm, vmset);
            init.VBoxCreateFromIso();

            LoadVmList();
        }

        // boots the vm
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

        // stops the vm upon confirmation by the user
        void StopVm(object? sender, EventArgs e)
        {
            // prompts the user to select a vm from the vmlist list
            if (VmListBox.SelectedIndex < 0)
            {
                MessageBox.Show("Select a VM from the list first.", "No selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // powers off the vm that is selected and if it is in the vmlist list
            string name = _vmNames[VmListBox.SelectedIndex];
            if (MessageBox.Show($"Power off \"{name}\"?", "Stop VM", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                vmset.VmName = name;
                vm.StopVM();
            }
        }

        // configures and launches the ui
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
