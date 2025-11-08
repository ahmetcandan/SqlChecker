namespace SqlChecker
{
    partial class FrmSqlCode
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmSqlCode));
            btnReview = new Button();
            sqlInputBox = new RichTextBox();
            resultsGrid = new DataGridView();
            cmbScheme = new ComboBox();
            cmbObjectName = new ComboBox();
            txtLineNumber = new RichTextBox();
            btnRefreshObj = new Button();
            ((System.ComponentModel.ISupportInitialize)resultsGrid).BeginInit();
            SuspendLayout();
            // 
            // btnReview
            // 
            btnReview.BackgroundImage = Properties.Resources.analysis;
            btnReview.BackgroundImageLayout = ImageLayout.Zoom;
            btnReview.Location = new Point(323, 5);
            btnReview.Name = "btnReview";
            btnReview.Size = new Size(43, 33);
            btnReview.TabIndex = 0;
            btnReview.UseVisualStyleBackColor = true;
            btnReview.Click += BtnReview_Click;
            // 
            // sqlInputBox
            // 
            sqlInputBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            sqlInputBox.Font = new Font("Hermit", 9.75F);
            sqlInputBox.Location = new Point(38, 41);
            sqlInputBox.Name = "sqlInputBox";
            sqlInputBox.ReadOnly = true;
            sqlInputBox.Size = new Size(841, 391);
            sqlInputBox.TabIndex = 1;
            sqlInputBox.Text = "";
            sqlInputBox.WordWrap = false;
            sqlInputBox.VScroll += SqlInputBox_VScroll;
            // 
            // resultsGrid
            // 
            resultsGrid.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            resultsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            resultsGrid.Location = new Point(12, 438);
            resultsGrid.Name = "resultsGrid";
            resultsGrid.ReadOnly = true;
            resultsGrid.Size = new Size(867, 183);
            resultsGrid.TabIndex = 2;
            resultsGrid.CellDoubleClick += ResultsGrid_CellDoubleClick;
            resultsGrid.DoubleClick += ResultsGrid_DoubleClick;
            // 
            // cmbScheme
            // 
            cmbScheme.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScheme.FormattingEnabled = true;
            cmbScheme.Location = new Point(12, 12);
            cmbScheme.Name = "cmbScheme";
            cmbScheme.Size = new Size(91, 23);
            cmbScheme.TabIndex = 3;
            cmbScheme.SelectedIndexChanged += CmbScheme_SelectedIndexChanged;
            // 
            // cmbObjectName
            // 
            cmbObjectName.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbObjectName.FormattingEnabled = true;
            cmbObjectName.Location = new Point(135, 12);
            cmbObjectName.Name = "cmbObjectName";
            cmbObjectName.Size = new Size(182, 23);
            cmbObjectName.TabIndex = 3;
            cmbObjectName.SelectedIndexChanged += CmbObjectName_SelectedIndexChanged;
            // 
            // txtLineNumber
            // 
            txtLineNumber.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            txtLineNumber.BackColor = Color.LightGray;
            txtLineNumber.BorderStyle = BorderStyle.FixedSingle;
            txtLineNumber.Enabled = false;
            txtLineNumber.Font = new Font("Hermit", 9.75F);
            txtLineNumber.ForeColor = Color.Teal;
            txtLineNumber.ImeMode = ImeMode.NoControl;
            txtLineNumber.Location = new Point(12, 41);
            txtLineNumber.Name = "txtLineNumber";
            txtLineNumber.ReadOnly = true;
            txtLineNumber.RightToLeft = RightToLeft.Yes;
            txtLineNumber.ScrollBars = RichTextBoxScrollBars.None;
            txtLineNumber.Size = new Size(27, 391);
            txtLineNumber.TabIndex = 4;
            txtLineNumber.Text = "";
            txtLineNumber.WordWrap = false;
            // 
            // btnRefreshObj
            // 
            btnRefreshObj.BackgroundImage = Properties.Resources.refresh1;
            btnRefreshObj.BackgroundImageLayout = ImageLayout.Zoom;
            btnRefreshObj.Location = new Point(109, 12);
            btnRefreshObj.Name = "btnRefreshObj";
            btnRefreshObj.Size = new Size(27, 23);
            btnRefreshObj.TabIndex = 0;
            btnRefreshObj.UseVisualStyleBackColor = true;
            btnRefreshObj.Click += BtnRefreshObj_Click;
            // 
            // FrmSqlCode
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(891, 633);
            Controls.Add(resultsGrid);
            Controls.Add(txtLineNumber);
            Controls.Add(cmbObjectName);
            Controls.Add(sqlInputBox);
            Controls.Add(cmbScheme);
            Controls.Add(btnRefreshObj);
            Controls.Add(btnReview);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(771, 578);
            Name = "FrmSqlCode";
            Text = "SQL Code Review";
            WindowState = FormWindowState.Maximized;
            Load += FrmSqlCode_Load;
            ((System.ComponentModel.ISupportInitialize)resultsGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Button btnReview;
        private RichTextBox sqlInputBox;
        private DataGridView resultsGrid;
        private ComboBox cmbScheme;
        private ComboBox cmbObjectName;
        private RichTextBox txtLineNumber;
        private Button btnRefreshObj;
    }
}
