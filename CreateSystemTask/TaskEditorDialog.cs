using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CreateSystemTask
{
    internal sealed class TaskEditorDialog : Form
    {
        private readonly TaskDefinition _task;
        private TextBox _nameBox;
        private ComboBox _kindBox;
        private ComboBox _scheduleBox;
        private DateTimePicker _startPicker;
        private FlowLayoutPanel _weekDaysPanel;
        private Label _startLabel;
        private Label _weekDaysLabel;
        private ComboBox _monthlyDayBox;
        private Label _monthlyDayLabel;
        private NumericUpDown _bootDelayBox;
        private Label _bootDelayLabel;
        private GroupBox _eventGroup;
        private TextBox _eventChannelBox;
        private TextBox _eventProviderBox;
        private TextBox _eventIdBox;
        private ComboBox _eventLevelBox;
        private TextBox _folderBox;
        private TextBox _patternsBox;
        private NumericUpDown _retentionBox;
        private CheckBox _subfoldersBox;
        private CheckBox _emptyFoldersBox;
        private TextBox _programBox;
        private TextBox _argumentsBox;
        private GroupBox _cleanupGroup;
        private GroupBox _launchGroup;

        public TaskDefinition ResultTask { get; private set; }

        public TaskEditorDialog(TaskDefinition existing)
        {
            _task = existing == null ? new TaskDefinition() : existing.Copy();
            InitializeWindow();
            BuildContent();
            LoadValues();
        }

        private void InitializeWindow()
        {
            Text = _task.Id == null ? "新建任务" : (_task.Name == "新建任务" ? "新建任务" : "编辑任务");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(660, 740);
            MinimumSize = new Size(660, 660);
            BackColor = Color.FromArgb(246, 248, 251);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        }

        private void BuildContent()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22, 18, 22, 12) };
            TableLayoutPanel form = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label intro = new Label
            {
                Text = "配置要执行的操作，以及它在什么时间运行。保存后会同步到 Windows 任务计划程序。",
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                ForeColor = Color.FromArgb(94, 105, 123),
                Margin = new Padding(0, 0, 0, 16)
            };
            form.Controls.Add(intro, 0, 0);
            form.SetColumnSpan(intro, 2);

            _nameBox = CreateTextBox();
            AddField(form, "任务名称", _nameBox);

            _kindBox = CreateCombo();
            _kindBox.Items.AddRange(new object[] { "清理文件夹", "启动程序" });
            _kindBox.SelectedIndexChanged += delegate { UpdateKindPanel(); };
            AddField(form, "执行操作", _kindBox);

            _scheduleBox = CreateCombo();
            _scheduleBox.Items.AddRange(new object[] { "每天", "每周", "每月", "一次", "计算机启动时", "当前用户登录时", "特定事件被记录时" });
            _scheduleBox.SelectedIndexChanged += delegate { UpdateSchedulePanel(); };
            AddField(form, "运行频率", _scheduleBox);

            _startPicker = new DateTimePicker
            {
                Dock = DockStyle.Left,
                Width = 240,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd   HH:mm",
                ShowUpDown = false,
                Margin = new Padding(0, 2, 0, 10)
            };
            _startLabel = AddField(form, "开始时间", _startPicker);

            _weekDaysPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 10)
            };
            string[] dayNames = { "日", "一", "二", "三", "四", "五", "六" };
            for (int day = 0; day < dayNames.Length; day++)
            {
                CheckBox check = new CheckBox
                {
                    Text = dayNames[day],
                    Tag = day,
                    AutoSize = true,
                    Margin = new Padding(0, 4, 10, 0)
                };
                _weekDaysPanel.Controls.Add(check);
            }
            _weekDaysLabel = AddField(form, "重复日期", _weekDaysPanel);

            _monthlyDayBox = CreateCombo();
            for (int day = 1; day <= 31; day++) _monthlyDayBox.Items.Add(day + " 日");
            _monthlyDayBox.Items.Add("最后一天");
            _monthlyDayLabel = AddField(form, "每月日期", _monthlyDayBox);

            Panel bootDelayPanel = new Panel { Height = 52, Dock = DockStyle.Fill };
            _bootDelayBox = new NumericUpDown { Minimum = 0, Maximum = 3600, Width = 100, Value = 30, Margin = new Padding(0, 2, 8, 8) };
            bootDelayPanel.Controls.Add(_bootDelayBox);
            bootDelayPanel.Controls.Add(new Label { Text = "秒后运行（0–3600 秒）", AutoSize = true, Location = new Point(110, 7), ForeColor = Color.FromArgb(94, 105, 123) });
            bootDelayPanel.Controls.Add(new Label { Text = "需管理员授权；以后台会话运行，无法显示图形界面或访问网络/加密文件。", AutoSize = true, Location = new Point(0, 30), ForeColor = Color.FromArgb(145, 93, 18) });
            _bootDelayLabel = AddField(form, "启动后延迟", bootDelayPanel);

            _eventGroup = BuildEventGroup();
            AddFullWidthRow(form, _eventGroup);

            _cleanupGroup = BuildCleanupGroup();
            AddFullWidthRow(form, _cleanupGroup);

            _launchGroup = BuildLaunchGroup();
            AddFullWidthRow(form, _launchGroup);

            page.Controls.Add(form);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(18, 10, 22, 12), BackColor = Color.White };
            Button cancel = new Button { Text = "取消", Width = 92, Height = 36, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.White };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 229);
            Button save = new Button { Text = "保存任务", Width = 112, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White };
            save.FlatAppearance.BorderSize = 0;
            save.Click += SaveClicked;
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 220, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            footer.Controls.Add(buttons);

            Controls.Add(page);
            Controls.Add(footer);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private GroupBox BuildCleanupGroup()
        {
            GroupBox group = new GroupBox
            {
                Text = "清理策略",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 18, 12, 8),
                Margin = new Padding(0, 4, 0, 12),
                BackColor = Color.White
            };
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _folderBox = CreateTextBox();
            Button folderBrowse = CreateBrowseButton("选择文件夹…");
            folderBrowse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择需要按策略清理的文件夹";
                    if (Directory.Exists(_folderBox.Text)) dialog.SelectedPath = _folderBox.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) _folderBox.Text = dialog.SelectedPath;
                }
            };
            AddGroupField(table, "目标文件夹", AddBrowseRow(_folderBox, folderBrowse));

            _patternsBox = CreateTextBox();
            AddGroupField(table, "文件名规则", _patternsBox);

            _retentionBox = new NumericUpDown { Minimum = 0, Maximum = 36500, Width = 110, Value = 30, Margin = new Padding(0, 3, 0, 8) };
            Panel retention = new Panel { Height = 34, Dock = DockStyle.Fill };
            retention.Controls.Add(_retentionBox);
            Label retentionHint = new Label { Text = "天前修改的文件会被删除", AutoSize = true, Location = new Point(120, 7), ForeColor = Color.FromArgb(94, 105, 123) };
            retention.Controls.Add(retentionHint);
            AddGroupField(table, "保留期限", retention);

            FlowLayoutPanel options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 2, 0, 7) };
            _subfoldersBox = new CheckBox { Text = "包含子文件夹", AutoSize = true, Margin = new Padding(0, 2, 24, 0) };
            _emptyFoldersBox = new CheckBox { Text = "清理空子文件夹", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            options.Controls.Add(_subfoldersBox);
            options.Controls.Add(_emptyFoldersBox);
            AddGroupField(table, "范围", options);

            Label note = new Label
            {
                Text = "支持多个规则，以分号分隔，例如：*.tmp;*.bak。选中后会删除目标目录内已清空的子文件夹；目标文件夹本身会保留。",
                AutoSize = true,
                MaximumSize = new Size(510, 0),
                ForeColor = Color.FromArgb(94, 105, 123),
                Margin = new Padding(0, 2, 0, 4)
            };
            AddFullWidthRow(table, note);
            group.Controls.Add(table);
            return group;
        }

        private GroupBox BuildLaunchGroup()
        {
            GroupBox group = new GroupBox
            {
                Text = "启动程序",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 18, 12, 8),
                Margin = new Padding(0, 4, 0, 12),
                BackColor = Color.White
            };
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _programBox = CreateTextBox();
            Button browse = CreateBrowseButton("选择程序…");
            browse.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择要定时启动的程序";
                    dialog.Filter = "程序文件 (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|所有文件 (*.*)|*.*";
                    if (File.Exists(_programBox.Text)) dialog.FileName = _programBox.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) _programBox.Text = dialog.FileName;
                }
            };
            AddGroupField(table, "程序路径", AddBrowseRow(_programBox, browse));

            _argumentsBox = CreateTextBox();
            AddGroupField(table, "启动参数", _argumentsBox);
            Label note = new Label
            {
                Text = "启动程序以当前 Windows 用户身份运行。",
                AutoSize = true,
                ForeColor = Color.FromArgb(94, 105, 123),
                Margin = new Padding(0, 0, 0, 4)
            };
            AddFullWidthRow(table, note);
            group.Controls.Add(table);
            return group;
        }

        private GroupBox BuildEventGroup()
        {
            GroupBox group = new GroupBox
            {
                Text = "事件筛选条件",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 18, 12, 8),
                Margin = new Padding(0, 4, 0, 12),
                BackColor = Color.White
            };
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _eventChannelBox = CreateTextBox();
            AddGroupField(table, "事件日志", _eventChannelBox);
            _eventProviderBox = CreateTextBox();
            AddGroupField(table, "来源（可选）", _eventProviderBox);
            _eventIdBox = CreateTextBox();
            AddGroupField(table, "事件 ID（可选）", _eventIdBox);
            _eventLevelBox = CreateCombo();
            _eventLevelBox.Items.AddRange(new object[] { "任意级别", "关键", "错误", "警告", "信息", "详细" });
            AddGroupField(table, "级别", _eventLevelBox);

            Label note = new Label
            {
                Text = "例如：System、Application 或 Microsoft-Windows-TaskScheduler/Operational。来源、事件 ID 和级别用于进一步筛选。",
                AutoSize = true,
                MaximumSize = new Size(510, 0),
                ForeColor = Color.FromArgb(94, 105, 123),
                Margin = new Padding(0, 2, 0, 4)
            };
            AddFullWidthRow(table, note);
            group.Controls.Add(table);
            return group;
        }

        private void LoadValues()
        {
            _nameBox.Text = _task.Name ?? String.Empty;
            _kindBox.SelectedIndex = _task.Kind == TaskKinds.Launch ? 1 : 0;
            _scheduleBox.SelectedIndex = ScheduleIndex(_task.Schedule);
            DateTime initial = _task.StartAt < DateTimePicker.MinimumDateTime ? DateTime.Now.AddMinutes(5) : _task.StartAt;
            if (initial > DateTimePicker.MaximumDateTime) initial = DateTime.Now.AddMinutes(5);
            _startPicker.Value = initial;
            _folderBox.Text = _task.TargetPath ?? String.Empty;
            _patternsBox.Text = String.IsNullOrWhiteSpace(_task.FilePatterns) ? "*.*" : _task.FilePatterns;
            _retentionBox.Value = Math.Max((decimal)_retentionBox.Minimum, Math.Min((decimal)_retentionBox.Maximum, _task.RetentionDays));
            _subfoldersBox.Checked = _task.IncludeSubdirectories;
            _emptyFoldersBox.Checked = _task.DeleteEmptyFolders;
            _programBox.Text = _task.Kind == TaskKinds.Launch ? (_task.TargetPath ?? String.Empty) : String.Empty;
            _argumentsBox.Text = _task.LaunchArguments ?? String.Empty;
            int monthlyDay = _task.MonthlyDayOfMonth <= 0 ? 1 : _task.MonthlyDayOfMonth;
            _monthlyDayBox.SelectedIndex = monthlyDay >= 32 ? 31 : Math.Max(0, Math.Min(30, monthlyDay - 1));
            _bootDelayBox.Value = Math.Max(_bootDelayBox.Minimum, Math.Min(_bootDelayBox.Maximum, _task.BootDelaySeconds));
            _eventChannelBox.Text = String.IsNullOrWhiteSpace(_task.EventChannel) ? "System" : _task.EventChannel;
            _eventProviderBox.Text = _task.EventProvider ?? String.Empty;
            _eventIdBox.Text = _task.EventId ?? String.Empty;
            _eventLevelBox.SelectedIndex = _task.EventLevel >= 0 && _task.EventLevel <= 5 ? _task.EventLevel : 0;
            foreach (CheckBox check in _weekDaysPanel.Controls.OfType<CheckBox>())
            {
                int day = (int)check.Tag;
                check.Checked = (_task.WeeklyDaysMask & (1 << day)) != 0;
            }
            UpdateKindPanel();
            UpdateSchedulePanel();
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            string name = _nameBox.Text.Trim();
            if (name.Length == 0) { ShowValidation("请输入任务名称。"); _nameBox.Focus(); return; }
            if (name.Length > 100) { ShowValidation("任务名称不能超过 100 个字符。"); _nameBox.Focus(); return; }

            TaskDefinition result = _task.Copy();
            result.Name = name;
            result.Kind = _kindBox.SelectedIndex == 1 ? TaskKinds.Launch : TaskKinds.Cleanup;
            result.Schedule = ScheduleFromIndex(_scheduleBox.SelectedIndex);
            result.StartAt = _startPicker.Value;
            result.WeeklyDaysMask = 0;
            foreach (CheckBox check in _weekDaysPanel.Controls.OfType<CheckBox>())
                if (check.Checked) result.WeeklyDaysMask |= 1 << (int)check.Tag;

            if (result.Schedule == ScheduleKinds.Once && result.StartAt <= DateTime.Now)
            {
                ShowValidation("单次任务的开始时间需要晚于当前时间。");
                _startPicker.Focus();
                return;
            }
            if (result.Schedule == ScheduleKinds.Weekly && result.WeeklyDaysMask == 0)
            {
                ShowValidation("每周任务至少选择一天。");
                _weekDaysPanel.Focus();
                return;
            }
            result.MonthlyDayOfMonth = _monthlyDayBox.SelectedIndex == 31 ? 32 : Math.Max(1, _monthlyDayBox.SelectedIndex + 1);
            result.BootDelaySeconds = Decimal.ToInt32(_bootDelayBox.Value);
            result.EventChannel = _eventChannelBox.Text.Trim();
            result.EventProvider = _eventProviderBox.Text.Trim();
            result.EventId = _eventIdBox.Text.Trim();
            result.EventLevel = Math.Max(0, _eventLevelBox.SelectedIndex);
            if (result.Schedule == ScheduleKinds.OnEvent)
            {
                if (result.EventChannel.Length == 0)
                {
                    ShowValidation("请输入事件日志名称，例如 System 或 Application。");
                    _eventChannelBox.Focus();
                    return;
                }
                int eventId;
                if (result.EventId.Length > 0 && (!Int32.TryParse(result.EventId, out eventId) || eventId < 0 || eventId > 65535))
                {
                    ShowValidation("事件 ID 必须是 0 到 65535 之间的整数，或留空以匹配任意 ID。");
                    _eventIdBox.Focus();
                    return;
                }
            }

            if (result.Kind == TaskKinds.Cleanup)
            {
                string folder = _folderBox.Text.Trim();
                if (!Directory.Exists(folder)) { ShowValidation("请选择一个存在的目标文件夹。"); _folderBox.Focus(); return; }
                string fullFolder = Path.GetFullPath(folder);
                string driveRoot = Path.GetPathRoot(fullFolder);
                if (String.Equals(fullFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    (driveRoot ?? String.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    ShowValidation("不能把磁盘根目录设为清理目标。");
                    _folderBox.Focus();
                    return;
                }
                string[] patterns = (_patternsBox.Text ?? String.Empty).Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (patterns.Length == 0) patterns = new[] { "*.*" };
                char[] invalid = Path.GetInvalidFileNameChars().Where(c => c != '*' && c != '?').ToArray();
                if (patterns.Any(p => p.IndexOfAny(invalid) >= 0 || p.Contains("\\") || p.Contains("/")))
                {
                    ShowValidation("文件名规则只支持文件名和通配符，例如 *.tmp;*.bak。");
                    _patternsBox.Focus();
                    return;
                }
                result.TargetPath = fullFolder;
                result.FilePatterns = String.Join(";", patterns);
                result.RetentionDays = Decimal.ToInt32(_retentionBox.Value);
                result.IncludeSubdirectories = _subfoldersBox.Checked;
                result.DeleteEmptyFolders = _emptyFoldersBox.Checked;
            }
            else
            {
                string program = _programBox.Text.Trim();
                if (!File.Exists(program)) { ShowValidation("请选择一个存在的程序文件。"); _programBox.Focus(); return; }
                result.TargetPath = Path.GetFullPath(program);
                result.LaunchArguments = _argumentsBox.Text.Trim();
            }

            ResultTask = result;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateKindPanel()
        {
            bool cleanup = _kindBox.SelectedIndex != 1;
            _cleanupGroup.Visible = cleanup;
            _launchGroup.Visible = !cleanup;
        }

        private void UpdateSchedulePanel()
        {
            int selected = _scheduleBox.SelectedIndex;
            bool hasCalendarTime = selected >= 0 && selected <= 3;
            _startPicker.Visible = hasCalendarTime;
            _startLabel.Visible = hasCalendarTime;
            _weekDaysPanel.Visible = selected == 1;
            _weekDaysLabel.Visible = selected == 1;
            _monthlyDayBox.Visible = selected == 2;
            _monthlyDayLabel.Visible = selected == 2;
            bool atStartup = selected == 4;
            _bootDelayBox.Parent.Visible = atStartup;
            _bootDelayLabel.Visible = atStartup;
            _eventGroup.Visible = selected == 6;
        }

        private void ShowValidation(string message)
        {
            MessageBox.Show(this, message, "请检查任务设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 2, 0, 10) };
        }

        private static ComboBox CreateCombo()
        {
            return new ComboBox { Dock = DockStyle.Left, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 0, 10) };
        }

        private static Button CreateBrowseButton(string text)
        {
            return new Button { Text = text, AutoSize = true, Height = 28, FlatStyle = FlatStyle.Flat, Margin = new Padding(8, 0, 0, 0), BackColor = Color.White };
        }

        private static Control AddBrowseRow(Control textbox, Button browse)
        {
            TableLayoutPanel row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                Margin = new Padding(0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            row.Controls.Add(textbox, 0, 0);
            row.Controls.Add(browse, 1, 0);
            return row;
        }

        private static Label AddField(TableLayoutPanel table, string labelText, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label label = new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = Color.FromArgb(67, 78, 96),
                Margin = new Padding(0, 8, 8, 10),
                Anchor = AnchorStyles.Left
            };
            table.Controls.Add(label, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            table.Controls.Add(control, 1, row);
            return label;
        }

        private static void AddGroupField(TableLayoutPanel table, string labelText, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label label = new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = Color.FromArgb(67, 78, 96),
                Margin = new Padding(0, 8, 8, 8),
                Anchor = AnchorStyles.Left
            };
            table.Controls.Add(label, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            table.Controls.Add(control, 1, row);
        }

        private static void AddFullWidthRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 2);
        }

        private static int ScheduleIndex(string schedule)
        {
            if (schedule == ScheduleKinds.Weekly) return 1;
            if (schedule == ScheduleKinds.Monthly) return 2;
            if (schedule == ScheduleKinds.Once) return 3;
            if (schedule == ScheduleKinds.AtStartup) return 4;
            if (schedule == ScheduleKinds.OnLogon) return 5;
            if (schedule == ScheduleKinds.OnEvent) return 6;
            return 0;
        }

        private static string ScheduleFromIndex(int index)
        {
            switch (index)
            {
                case 1: return ScheduleKinds.Weekly;
                case 2: return ScheduleKinds.Monthly;
                case 3: return ScheduleKinds.Once;
                case 4: return ScheduleKinds.AtStartup;
                case 5: return ScheduleKinds.OnLogon;
                case 6: return ScheduleKinds.OnEvent;
                default: return ScheduleKinds.Daily;
            }
        }
    }
}
