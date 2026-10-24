using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CreateSystemTask
{
    public partial class Form1 : Form
    {
        private readonly Color _ink = Color.FromArgb(32, 43, 61);
        private readonly Color _muted = Color.FromArgb(105, 117, 137);
        private readonly Color _blue = Color.FromArgb(37, 99, 235);
        private DataGridView _grid;
        private Label _emptyLabel;
        private Label _summaryTotal;
        private Label _summaryEnabled;
        private Label _summaryLastRun;
        private Label _detailName;
        private Label _detailKind;
        private Label _detailSchedule;
        private Label _detailTarget;
        private Label _detailPolicy;
        private Label _detailLastRun;
        private Label _detailResult;
        private Label _detailStatus;
        private Button _runButton;
        private Button _editButton;
        private Button _deleteButton;
        private bool _refreshing;
        private string _selectedId;

        public Form1()
        {
            InitializeComponent();
            BuildWindow();
            Load += delegate { RefreshTasks(null); };
        }

        private void BuildWindow()
        {
            Text = "系统任务中心";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1020, 680);
            ClientSize = new Size(1180, 780);
            BackColor = Color.FromArgb(244, 247, 251);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(26, 18, 26, 22),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildSummary(), 0, 1);
            root.Controls.Add(BuildWorkspace(), 0, 2);
            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            TableLayoutPanel header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            FlowLayoutPanel titleStack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            Label title = new Label { Text = "系统任务中心", AutoSize = true, Font = new Font(Font.FontFamily, 22F, FontStyle.Bold), ForeColor = _ink, Margin = new Padding(0, 0, 0, 5) };
            Label subtitle = new Label { Text = "集中创建、查看和管理 Windows 定时任务", AutoSize = true, Font = new Font(Font.FontFamily, 9.5F), ForeColor = _muted, Margin = new Padding(1, 0, 0, 0) };
            titleStack.Controls.Add(title);
            titleStack.Controls.Add(subtitle);
            Button add = CreatePrimaryButton("＋  新建任务", 142, 42);
            add.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            add.Margin = new Padding(0, 13, 0, 0);
            add.Click += AddTaskClicked;
            header.Controls.Add(titleStack, 0, 0);
            header.Controls.Add(add, 1, 0);
            return header;
        }

        private Control BuildSummary()
        {
            FlowLayoutPanel cards = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 5, 0, 10) };
            _summaryTotal = new Label();
            _summaryEnabled = new Label();
            _summaryLastRun = new Label();
            cards.Controls.Add(CreateSummaryCard("任务总数", _summaryTotal, "全部任务配置"));
            cards.Controls.Add(CreateSummaryCard("已启用", _summaryEnabled, "由 Windows 计划程序运行"));
            cards.Controls.Add(CreateSummaryCard("最近运行", _summaryLastRun, "显示最近一次执行时间"));
            return cards;
        }

        private Control CreateSummaryCard(string caption, Label value, string note)
        {
            Panel card = new Panel { Width = 245, Height = 76, BackColor = Color.White, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(14, 9, 12, 6) };
            Label title = new Label { Text = caption, AutoSize = true, ForeColor = _muted, Location = new Point(14, 7) };
            value.Text = "—";
            value.AutoSize = true;
            value.Font = new Font(Font.FontFamily, 17F, FontStyle.Bold);
            value.ForeColor = _ink;
            value.Location = new Point(14, 27);
            Label description = new Label { Text = note, AutoSize = true, ForeColor = Color.FromArgb(139, 149, 165), Font = new Font(Font.FontFamily, 8F), Location = new Point(14, 56) };
            card.Controls.Add(title);
            card.Controls.Add(value);
            card.Controls.Add(description);
            return card;
        }

        private Control BuildWorkspace()
        {
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Size = new Size(1128, 600),
                FixedPanel = FixedPanel.Panel2,
                BackColor = Color.FromArgb(229, 234, 242),
                Panel1MinSize = 560,
                Panel2MinSize = 340,
                SplitterDistance = 700,
                IsSplitterFixed = false
            };
            split.Panel1.Padding = new Padding(0, 0, 12, 0);
            split.Panel2.Padding = new Padding(12, 0, 0, 0);
            split.Panel1.Controls.Add(BuildTaskList());
            split.Panel2.Controls.Add(BuildDetails());
            return split;
        }

        private Control BuildTaskList()
        {
            Panel surface = CreateSurface();
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(16, 14, 16, 14) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel listHeader = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
            Label heading = new Label { Text = "任务列表", AutoSize = true, Font = new Font(Font.FontFamily, 12F, FontStyle.Bold), ForeColor = _ink, Margin = new Padding(0, 7, 8, 0) };
            Label hint = new Label { Text = "点击开关控制单项任务", AutoSize = true, ForeColor = _muted, Font = new Font(Font.FontFamily, 8.5F), Margin = new Padding(0, 11, 0, 0) };
            listHeader.Controls.Add(heading);
            listHeader.Controls.Add(hint);
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                GridColor = Color.FromArgb(237, 240, 245),
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 48 }
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = _muted;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
            _grid.DefaultCellStyle.BackColor = Color.White;
            _grid.DefaultCellStyle.ForeColor = _ink;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(236, 243, 255);
            _grid.DefaultCellStyle.SelectionForeColor = _ink;
            _grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "启用", FillWeight = 45, FlatStyle = FlatStyle.Flat, ThreeState = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "任务名称", FillWeight = 112, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = "操作", FillWeight = 72, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Schedule", HeaderText = "运行时间", FillWeight = 104, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Result", HeaderText = "最近结果", FillWeight = 82, ReadOnly = true });
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellValueChanged += GridCellValueChanged;
            _grid.SelectionChanged += delegate { if (!_refreshing) ShowSelectedTask(); };
            _grid.CellDoubleClick += delegate { EditSelectedTask(); };
            _grid.DataError += delegate { };

            _emptyLabel = new Label
            {
                Text = "还没有任务\r\n点击右上角“新建任务”开始添加",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                ForeColor = _muted,
                BackColor = Color.White,
                Font = new Font(Font.FontFamily, 10F)
            };
            Panel listArea = new Panel { Dock = DockStyle.Fill };
            listArea.Controls.Add(_grid);
            listArea.Controls.Add(_emptyLabel);
            layout.Controls.Add(listHeader, 0, 0);
            layout.Controls.Add(listArea, 0, 1);
            surface.Controls.Add(layout);
            return surface;
        }

        private Control BuildDetails()
        {
            Panel surface = CreateSurface();
            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(18, 16, 18, 16) };
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            FlowLayoutPanel body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            Label eyebrow = new Label { Text = "任务详情", AutoSize = true, Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold), ForeColor = _muted, Margin = new Padding(0, 0, 0, 8) };
            _detailName = new Label { Text = "选择一个任务", AutoSize = true, MaximumSize = new Size(295, 0), Font = new Font(Font.FontFamily, 17F, FontStyle.Bold), ForeColor = _ink, Margin = new Padding(0, 0, 0, 7) };
            _detailStatus = new Label { Text = "", AutoSize = true, ForeColor = Color.FromArgb(22, 130, 91), BackColor = Color.FromArgb(227, 247, 238), Padding = new Padding(8, 4, 8, 4), Margin = new Padding(0, 0, 0, 20) };
            _detailKind = AddDetailLine(body, "执行操作", "—");
            _detailSchedule = AddDetailLine(body, "运行时间", "—");
            _detailTarget = AddDetailLine(body, "目标", "—");
            _detailPolicy = AddDetailLine(body, "策略", "—");
            _detailLastRun = AddDetailLine(body, "最近运行", "—");
            _detailResult = AddDetailLine(body, "运行结果", "—");
            body.Controls.Add(eyebrow);
            body.Controls.Add(_detailName);
            body.Controls.Add(_detailStatus);
            body.Controls.Add(_detailKind.Parent);
            body.Controls.Add(_detailSchedule.Parent);
            body.Controls.Add(_detailTarget.Parent);
            body.Controls.Add(_detailPolicy.Parent);
            body.Controls.Add(_detailLastRun.Parent);
            body.Controls.Add(_detailResult.Parent);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _runButton = CreateSecondaryButton("立即运行", 82);
            _editButton = CreateSecondaryButton("编辑", 68);
            _deleteButton = CreateSecondaryButton("删除", 68);
            _runButton.Click += RunSelectedTask;
            _editButton.Click += delegate { EditSelectedTask(); };
            _deleteButton.Click += DeleteSelectedTask;
            actions.Controls.Add(_runButton);
            actions.Controls.Add(_editButton);
            actions.Controls.Add(_deleteButton);
            content.Controls.Add(body, 0, 0);
            content.Controls.Add(actions, 0, 1);
            surface.Controls.Add(content);
            EnableDetails(false);
            return surface;
        }

        private Label AddDetailLine(FlowLayoutPanel parent, string title, string initial)
        {
            Panel row = new Panel { Width = 300, Height = 58, Margin = new Padding(0, 0, 0, 4) };
            Label caption = new Label { Text = title, AutoSize = true, ForeColor = _muted, Location = new Point(0, 2), Font = new Font(Font.FontFamily, 8.5F) };
            Label value = new Label { Text = initial, AutoSize = true, MaximumSize = new Size(290, 0), ForeColor = _ink, Location = new Point(0, 22) };
            row.Controls.Add(caption);
            row.Controls.Add(value);
            parent.Controls.Add(row);
            return value;
        }

        private void RefreshTasks(string selectId)
        {
            List<TaskDefinition> tasks;
            try { tasks = TaskStore.GetAll().OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "读取任务列表失败。" + Environment.NewLine + ex.Message, "任务列表", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _refreshing = true;
            _grid.Rows.Clear();
            int selectedIndex = -1;
            DateTime? mostRecent = null;
            foreach (TaskDefinition task in tasks)
            {
                int row = _grid.Rows.Add(task.Enabled, task.Name, KindLabel(task), ScheduleLabel(task), ResultLabel(task));
                _grid.Rows[row].Tag = task;
                if (task.Id == (selectId ?? _selectedId)) selectedIndex = row;
                if (task.LastRunAt.HasValue && (!mostRecent.HasValue || task.LastRunAt.Value > mostRecent.Value)) mostRecent = task.LastRunAt;
            }
            _emptyLabel.Visible = tasks.Count == 0;
            _summaryTotal.Text = tasks.Count.ToString();
            _summaryEnabled.Text = tasks.Count(t => t.Enabled).ToString();
            _summaryLastRun.Text = mostRecent.HasValue ? mostRecent.Value.ToString("MM-dd HH:mm") : "—";
            if (selectedIndex >= 0)
            {
                _grid.ClearSelection();
                _grid.Rows[selectedIndex].Selected = true;
                _grid.CurrentCell = _grid.Rows[selectedIndex].Cells[1];
                _selectedId = ((TaskDefinition)_grid.Rows[selectedIndex].Tag).Id;
            }
            else if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[1];
                _selectedId = ((TaskDefinition)_grid.Rows[0].Tag).Id;
            }
            else
            {
                _selectedId = null;
            }
            _refreshing = false;
            ShowSelectedTask();
        }

        private void ShowSelectedTask()
        {
            TaskDefinition task = GetSelectedTask();
            if (task == null)
            {
                _detailName.Text = "选择一个任务";
                _detailStatus.Text = String.Empty;
                _detailKind.Text = "—";
                _detailSchedule.Text = "—";
                _detailTarget.Text = "—";
                _detailPolicy.Text = "—";
                _detailLastRun.Text = "—";
                _detailResult.Text = "—";
                EnableDetails(false);
                return;
            }
            _selectedId = task.Id;
            _detailName.Text = task.Name;
            _detailStatus.Text = task.Enabled ? "●  已启用" : "○  已停用";
            _detailStatus.ForeColor = task.Enabled ? Color.FromArgb(22, 130, 91) : Color.FromArgb(107, 117, 133);
            _detailStatus.BackColor = task.Enabled ? Color.FromArgb(227, 247, 238) : Color.FromArgb(239, 242, 247);
            _detailKind.Text = KindLabel(task);
            _detailSchedule.Text = ScheduleLabel(task);
            _detailTarget.Text = task.TargetPath ?? "—";
            _detailPolicy.Text = task.Kind == TaskKinds.Cleanup
                ? "匹配 " + (String.IsNullOrWhiteSpace(task.FilePatterns) ? "*.*" : task.FilePatterns) + "；超过 " + task.RetentionDays + " 天" +
                    (task.IncludeSubdirectories ? "；包含子文件夹" : "；仅当前文件夹") + (task.DeleteEmptyFolders ? "；清理空子文件夹" : String.Empty)
                : (String.IsNullOrWhiteSpace(task.LaunchArguments) ? "无启动参数" : "参数：" + task.LaunchArguments);
            _detailLastRun.Text = task.LastRunAt.HasValue ? task.LastRunAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "尚未运行";
            _detailResult.Text = task.LastRunResult ?? "—";
            EnableDetails(true);
        }

        private void EnableDetails(bool enabled)
        {
            if (_runButton == null) return;
            _runButton.Enabled = enabled;
            _editButton.Enabled = enabled;
            _deleteButton.Enabled = enabled;
        }

        private TaskDefinition GetSelectedTask()
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as TaskDefinition;
        }

        private void GridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_refreshing || e.RowIndex < 0 || e.ColumnIndex != 0) return;
            TaskDefinition task = _grid.Rows[e.RowIndex].Tag as TaskDefinition;
            if (task == null) return;
            bool enabled = Convert.ToBoolean(_grid.Rows[e.RowIndex].Cells[0].Value);
            if (task.Enabled == enabled) return;

            try
            {
                TaskSchedulerService.SetEnabled(task, enabled);
                task.Enabled = enabled;
                TaskStore.Update(task);
                RefreshTasks(task.Id);
            }
            catch (Exception ex)
            {
                try { TaskSchedulerService.SetEnabled(task, task.Enabled); }
                catch { }
                MessageBox.Show(this, "切换任务状态失败，任务配置已恢复。" + Environment.NewLine + ex.Message, "任务状态", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RefreshTasks(task.Id);
            }
        }

        private void AddTaskClicked(object sender, EventArgs e)
        {
            using (TaskEditorDialog dialog = new TaskEditorDialog(null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                TaskDefinition task = dialog.ResultTask;
                try
                {
                    TaskSchedulerService.Register(task);
                    try { TaskStore.Add(task); }
                    catch
                    {
                        try { TaskSchedulerService.Delete(task); }
                        catch { }
                        throw;
                    }
                    RefreshTasks(task.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "创建任务失败。" + Environment.NewLine + ex.Message, "创建任务", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EditSelectedTask()
        {
            TaskDefinition current = GetSelectedTask();
            if (current == null) return;
            using (TaskEditorDialog dialog = new TaskEditorDialog(current))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                TaskDefinition updated = dialog.ResultTask;
                try
                {
                    TaskSchedulerService.Register(updated);
                    try { TaskStore.Update(updated); }
                    catch
                    {
                        try { TaskSchedulerService.Register(current); }
                        catch { }
                        throw;
                    }
                    RefreshTasks(updated.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "保存任务修改失败。" + Environment.NewLine + ex.Message, "编辑任务", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteSelectedTask(object sender, EventArgs e)
        {
            TaskDefinition task = GetSelectedTask();
            if (task == null) return;
            DialogResult answer = MessageBox.Show(this,
                "确定删除任务“" + task.Name + "”吗？这也会移除对应的 Windows 计划任务。",
                "删除任务", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
            try
            {
                TaskSchedulerService.Delete(task);
                try { TaskStore.Delete(task.Id); }
                catch
                {
                    try { TaskSchedulerService.Register(task); }
                    catch { }
                    throw;
                }
                _selectedId = null;
                RefreshTasks(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "删除任务失败。" + Environment.NewLine + ex.Message, "删除任务", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSelectedTask(object sender, EventArgs e)
        {
            TaskDefinition task = GetSelectedTask();
            if (task == null) return;
            _runButton.Enabled = false;
            _runButton.Text = "运行中…";
            Task.Run(() => TaskExecutor.RunNow(task.Id)).ContinueWith(delegate(Task<bool> run)
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    _runButton.Text = "立即运行";
                    RefreshTasks(task.Id);
                    if (run.IsFaulted)
                    {
                        MessageBox.Show(this, "运行任务时发生错误。" + Environment.NewLine + run.Exception.GetBaseException().Message,
                            "运行任务", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
            });
        }

        private static string KindLabel(TaskDefinition task)
        {
            return task.Kind == TaskKinds.Launch ? "启动程序" : "清理文件夹";
        }

        private static string ScheduleLabel(TaskDefinition task)
        {
            string time = task.StartAt.ToString("HH:mm");
            if (task.Schedule == ScheduleKinds.Once) return "单次 · " + task.StartAt.ToString("MM-dd HH:mm");
            if (task.Schedule == ScheduleKinds.Monthly)
                return "每月 · " + (task.MonthlyDayOfMonth >= 32 ? "最后一天" : Math.Max(1, task.MonthlyDayOfMonth) + " 日") + " · " + time;
            if (task.Schedule == ScheduleKinds.Weekly)
            {
                string[] days = { "日", "一", "二", "三", "四", "五", "六" };
                string repeat = String.Join(" ", Enumerable.Range(0, 7).Where(d => (task.WeeklyDaysMask & (1 << d)) != 0).Select(d => "周" + days[d]));
                return "每周 " + repeat + " · " + time;
            }
            if (task.Schedule == ScheduleKinds.AtStartup)
                return "计算机启动" + (task.BootDelaySeconds > 0 ? " · 延迟 " + task.BootDelaySeconds + " 秒" : String.Empty);
            if (task.Schedule == ScheduleKinds.OnLogon) return "当前用户登录时";
            if (task.Schedule == ScheduleKinds.OnEvent)
            {
                string description = "事件 · " + (String.IsNullOrWhiteSpace(task.EventChannel) ? "System" : task.EventChannel);
                if (!String.IsNullOrWhiteSpace(task.EventId)) description += " · ID " + task.EventId;
                if (!String.IsNullOrWhiteSpace(task.EventProvider)) description += " · " + task.EventProvider;
                if (task.EventLevel >= 1 && task.EventLevel <= 5)
                {
                    string[] levels = { "", "关键", "错误", "警告", "信息", "详细" };
                    description += " · " + levels[task.EventLevel];
                }
                return description;
            }
            return "每天 · " + time;
        }

        private static string ResultLabel(TaskDefinition task)
        {
            if (!task.LastRunAt.HasValue) return "尚未运行";
            string result = task.LastRunResult ?? String.Empty;
            if (result.StartsWith("有错误", StringComparison.Ordinal) || result.StartsWith("执行失败", StringComparison.Ordinal)) return "有错误";
            return "成功 · " + task.LastRunAt.Value.ToString("MM-dd HH:mm");
        }

        private static Panel CreateSurface()
        {
            return new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0) };
        }

        private Button CreatePrimaryButton(string text, int width, int height)
        {
            Button button = new Button { Text = text, Width = width, Height = height, FlatStyle = FlatStyle.Flat, BackColor = _blue, ForeColor = Color.White, Font = new Font(Font.FontFamily, 9F, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private Button CreateSecondaryButton(string text, int width)
        {
            Button button = new Button { Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = _ink, Font = new Font(Font.FontFamily, 8.5F), Margin = new Padding(0, 6, 7, 0), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = Color.FromArgb(216, 223, 233);
            return button;
        }
    }
}
