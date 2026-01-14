namespace SAM.Picker
{
	partial class GamePicker
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ToolStripSeparator _ToolStripSeparator1;
            System.Windows.Forms.ToolStripSeparator _ToolStripSeparator2;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GamePicker));
            this._LogoImageList = new System.Windows.Forms.ImageList(this.components);
            this._CallbackTimer = new System.Windows.Forms.Timer(this.components);
            this._PickerToolStrip = new System.Windows.Forms.ToolStrip();
            this._RefreshGamesButton = new System.Windows.Forms.ToolStripButton();
            this.unlockAllGames = new System.Windows.Forms.ToolStripButton();
            this._AddGameTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this._FilterDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this._FilterGamesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._FilterDemosMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._FilterModsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._FilterJunkMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._PickerStatusStrip = new System.Windows.Forms.StatusStrip();
            this._PickerStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this._DownloadStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.unlockAllProgress = new System.Windows.Forms.ToolStripProgressBar();
            this._LogoWorker = new System.ComponentModel.BackgroundWorker();
            this._ListWorker = new System.ComponentModel.BackgroundWorker();
            this._GameListView = new SAM.Picker.DoubleBufferedListView();
            _ToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            _ToolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this._PickerToolStrip.SuspendLayout();
            this._PickerStatusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // _ToolStripSeparator1
            // 
            _ToolStripSeparator1.Name = "_ToolStripSeparator1";
            _ToolStripSeparator1.Size = new System.Drawing.Size(6, 23);
            _ToolStripSeparator1.Visible = false;
            // 
            // _ToolStripSeparator2
            // 
            _ToolStripSeparator2.Name = "_ToolStripSeparator2";
            _ToolStripSeparator2.Size = new System.Drawing.Size(6, 23);
            _ToolStripSeparator2.Visible = false;
            // 
            // _LogoImageList
            // 
            this._LogoImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            this._LogoImageList.ImageSize = new System.Drawing.Size(184, 69);
            this._LogoImageList.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // _CallbackTimer
            // 
            this._CallbackTimer.Enabled = true;
            this._CallbackTimer.Tick += new System.EventHandler(this.OnTimer);
            // 
            // _PickerToolStrip
            // 
            this._PickerToolStrip.BackColor = System.Drawing.Color.Black;
            this._PickerToolStrip.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this._PickerToolStrip.CanOverflow = false;
            this._PickerToolStrip.GripMargin = new System.Windows.Forms.Padding(0);
            this._PickerToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this._PickerToolStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this._PickerToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._RefreshGamesButton,
            this.unlockAllGames,
            this._AddGameTextBox,
            _ToolStripSeparator1,
            this.toolStripButton1,
            _ToolStripSeparator2,
            this._FilterDropDownButton});
            this._PickerToolStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this._PickerToolStrip.Location = new System.Drawing.Point(0, 0);
            this._PickerToolStrip.Name = "_PickerToolStrip";
            this._PickerToolStrip.Padding = new System.Windows.Forms.Padding(0);
            this._PickerToolStrip.Size = new System.Drawing.Size(812, 27);
            this._PickerToolStrip.TabIndex = 1;
            this._PickerToolStrip.Text = "toolStrip1";
            // 
            // _RefreshGamesButton
            // 
            this._RefreshGamesButton.ForeColor = System.Drawing.Color.White;
            this._RefreshGamesButton.Image = ((System.Drawing.Image)(resources.GetObject("_RefreshGamesButton.Image")));
            this._RefreshGamesButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._RefreshGamesButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._RefreshGamesButton.Name = "_RefreshGamesButton";
            this._RefreshGamesButton.Size = new System.Drawing.Size(24, 24);
            this._RefreshGamesButton.Click += new System.EventHandler(this.OnRefresh);
            // 
            // unlockAllGames
            // 
            this.unlockAllGames.Enabled = false;
            this.unlockAllGames.ForeColor = System.Drawing.Color.White;
            this.unlockAllGames.Image = ((System.Drawing.Image)(resources.GetObject("unlockAllGames.Image")));
            this.unlockAllGames.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.unlockAllGames.Name = "unlockAllGames";
            this.unlockAllGames.Size = new System.Drawing.Size(24, 24);
            this.unlockAllGames.Click += new System.EventHandler(this.unlockAllGames_Click);
            // 
            // _AddGameTextBox
            // 
            this._AddGameTextBox.BackColor = System.Drawing.Color.Black;
            this._AddGameTextBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._AddGameTextBox.ForeColor = System.Drawing.Color.White;
            this._AddGameTextBox.Name = "_AddGameTextBox";
            this._AddGameTextBox.Size = new System.Drawing.Size(116, 23);
            this._AddGameTextBox.Visible = false;
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.ForeColor = System.Drawing.Color.White;
            this.toolStripButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton1.Image")));
            this.toolStripButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButton1.Name = "toolStripButton1";
            this.toolStripButton1.Size = new System.Drawing.Size(55, 24);
            this.toolStripButton1.Text = "New";
            this.toolStripButton1.Visible = false;
            // 
            // _FilterDropDownButton
            // 
            this._FilterDropDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._FilterDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._FilterGamesMenuItem,
            this._FilterDemosMenuItem,
            this._FilterModsMenuItem,
            this._FilterJunkMenuItem});
            this._FilterDropDownButton.ForeColor = System.Drawing.Color.Black;
            this._FilterDropDownButton.Image = ((System.Drawing.Image)(resources.GetObject("_FilterDropDownButton.Image")));
            this._FilterDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._FilterDropDownButton.Name = "_FilterDropDownButton";
            this._FilterDropDownButton.Size = new System.Drawing.Size(33, 24);
            this._FilterDropDownButton.Text = "Game filtering";
            // 
            // _FilterGamesMenuItem
            // 
            this._FilterGamesMenuItem.BackColor = System.Drawing.Color.Black;
            this._FilterGamesMenuItem.Checked = true;
            this._FilterGamesMenuItem.CheckOnClick = true;
            this._FilterGamesMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this._FilterGamesMenuItem.ForeColor = System.Drawing.Color.White;
            this._FilterGamesMenuItem.Name = "_FilterGamesMenuItem";
            this._FilterGamesMenuItem.Size = new System.Drawing.Size(142, 22);
            this._FilterGamesMenuItem.Text = "Show &games";
            this._FilterGamesMenuItem.CheckedChanged += new System.EventHandler(this.OnFilterUpdate);
            // 
            // _FilterDemosMenuItem
            // 
            this._FilterDemosMenuItem.BackColor = System.Drawing.Color.Black;
            this._FilterDemosMenuItem.Checked = true;
            this._FilterDemosMenuItem.CheckOnClick = true;
            this._FilterDemosMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this._FilterDemosMenuItem.ForeColor = System.Drawing.Color.White;
            this._FilterDemosMenuItem.Name = "_FilterDemosMenuItem";
            this._FilterDemosMenuItem.Size = new System.Drawing.Size(142, 22);
            this._FilterDemosMenuItem.Text = "Show &demos";
            this._FilterDemosMenuItem.CheckedChanged += new System.EventHandler(this.OnFilterUpdate);
            // 
            // _FilterModsMenuItem
            // 
            this._FilterModsMenuItem.BackColor = System.Drawing.Color.Black;
            this._FilterModsMenuItem.Checked = true;
            this._FilterModsMenuItem.CheckOnClick = true;
            this._FilterModsMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this._FilterModsMenuItem.ForeColor = System.Drawing.Color.White;
            this._FilterModsMenuItem.Name = "_FilterModsMenuItem";
            this._FilterModsMenuItem.Size = new System.Drawing.Size(142, 22);
            this._FilterModsMenuItem.Text = "Show &mods";
            this._FilterModsMenuItem.CheckedChanged += new System.EventHandler(this.OnFilterUpdate);
            // 
            // _FilterJunkMenuItem
            // 
            this._FilterJunkMenuItem.BackColor = System.Drawing.Color.Black;
            this._FilterJunkMenuItem.Checked = true;
            this._FilterJunkMenuItem.CheckOnClick = true;
            this._FilterJunkMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this._FilterJunkMenuItem.ForeColor = System.Drawing.Color.White;
            this._FilterJunkMenuItem.Name = "_FilterJunkMenuItem";
            this._FilterJunkMenuItem.Size = new System.Drawing.Size(142, 22);
            this._FilterJunkMenuItem.Text = "Show &junk";
            this._FilterJunkMenuItem.CheckedChanged += new System.EventHandler(this.OnFilterUpdate);
            // 
            // _PickerStatusStrip
            // 
            this._PickerStatusStrip.BackColor = System.Drawing.Color.Black;
            this._PickerStatusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this._PickerStatusStrip.ImeMode = System.Windows.Forms.ImeMode.Off;
            this._PickerStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._PickerStatusLabel,
            this._DownloadStatusLabel,
            this.unlockAllProgress});
            this._PickerStatusStrip.Location = new System.Drawing.Point(0, 631);
            this._PickerStatusStrip.Name = "_PickerStatusStrip";
            this._PickerStatusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 16, 0);
            this._PickerStatusStrip.Size = new System.Drawing.Size(812, 22);
            this._PickerStatusStrip.SizingGrip = false;
            this._PickerStatusStrip.TabIndex = 2;
            this._PickerStatusStrip.Text = "statusStrip";
            // 
            // _PickerStatusLabel
            // 
            this._PickerStatusLabel.BackColor = System.Drawing.Color.Black;
            this._PickerStatusLabel.ForeColor = System.Drawing.Color.White;
            this._PickerStatusLabel.Name = "_PickerStatusLabel";
            this._PickerStatusLabel.Size = new System.Drawing.Size(795, 17);
            this._PickerStatusLabel.Spring = true;
            this._PickerStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _DownloadStatusLabel
            // 
            this._DownloadStatusLabel.BackColor = System.Drawing.Color.Black;
            this._DownloadStatusLabel.ForeColor = System.Drawing.Color.White;
            this._DownloadStatusLabel.Image = ((System.Drawing.Image)(resources.GetObject("_DownloadStatusLabel.Image")));
            this._DownloadStatusLabel.Name = "_DownloadStatusLabel";
            this._DownloadStatusLabel.Size = new System.Drawing.Size(20, 20);
            this._DownloadStatusLabel.Visible = false;
            // 
            // unlockAllProgress
            // 
            this.unlockAllProgress.ForeColor = System.Drawing.SystemColors.WindowText;
            this.unlockAllProgress.Name = "unlockAllProgress";
            this.unlockAllProgress.Size = new System.Drawing.Size(117, 19);
            this.unlockAllProgress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.unlockAllProgress.Visible = false;
            // 
            // _LogoWorker
            // 
            this._LogoWorker.WorkerSupportsCancellation = true;
            this._LogoWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.DoDownloadLogo);
            this._LogoWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.OnDownloadLogo);
            // 
            // _ListWorker
            // 
            this._ListWorker.WorkerSupportsCancellation = true;
            this._ListWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.DoDownloadList);
            this._ListWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.OnDownloadList);
            // 
            // _GameListView
            // 
            this._GameListView.BackColor = System.Drawing.Color.Black;
            this._GameListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._GameListView.ForeColor = System.Drawing.Color.White;
            this._GameListView.HideSelection = false;
            this._GameListView.LargeImageList = this._LogoImageList;
            this._GameListView.Location = new System.Drawing.Point(0, 27);
            this._GameListView.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this._GameListView.MultiSelect = false;
            this._GameListView.Name = "_GameListView";
            this._GameListView.Size = new System.Drawing.Size(812, 604);
            this._GameListView.SmallImageList = this._LogoImageList;
            this._GameListView.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this._GameListView.TabIndex = 0;
            this._GameListView.TileSize = new System.Drawing.Size(184, 69);
            this._GameListView.UseCompatibleStateImageBehavior = false;
            this._GameListView.VirtualMode = true;
            this._GameListView.ItemActivate += new System.EventHandler(this.OnActivateGame);
            this._GameListView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OnSelectGame);
            this._GameListView.RetrieveVirtualItem += new System.Windows.Forms.RetrieveVirtualItemEventHandler(this.OnGameListViewRetrieveVirtualItem);
            // 
            // GamePicker
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(812, 653);
            this.Controls.Add(this._GameListView);
            this.Controls.Add(this._PickerStatusStrip);
            this.Controls.Add(this._PickerToolStrip);
            this.Font = new System.Drawing.Font("Verdana", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "GamePicker";
            this.Text = "  SAM-ABB   ·   Loading...   ·   1.0.3-beta (ik UI is trash)";
            this.Load += new System.EventHandler(this.GamePicker_Load);
            this._PickerToolStrip.ResumeLayout(false);
            this._PickerToolStrip.PerformLayout();
            this._PickerStatusStrip.ResumeLayout(false);
            this._PickerStatusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private DoubleBufferedListView _GameListView;
		private System.Windows.Forms.ImageList _LogoImageList;
		private System.Windows.Forms.Timer _CallbackTimer;
		private System.Windows.Forms.ToolStripButton _RefreshGamesButton;
		private System.Windows.Forms.ToolStripTextBox _AddGameTextBox;
        private System.Windows.Forms.ToolStripDropDownButton _FilterDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem _FilterGamesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _FilterJunkMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _FilterDemosMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _FilterModsMenuItem;
        private System.Windows.Forms.StatusStrip _PickerStatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _DownloadStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel _PickerStatusLabel;
        private System.ComponentModel.BackgroundWorker _LogoWorker;
        private System.ComponentModel.BackgroundWorker _ListWorker;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.ToolStripButton unlockAllGames;
        private System.Windows.Forms.ToolStripProgressBar unlockAllProgress;
        private System.Windows.Forms.ToolStrip _PickerToolStrip;
    }
}

