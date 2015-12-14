namespace Barrkel.ScratchPad
{
	partial class BookView
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
			if (disposing)
			{
				EnsureSaved();
			}
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this._statusPanel = new System.Windows.Forms.Panel();
			this._versionLabel = new System.Windows.Forms.Label();
			this._dateLabel = new System.Windows.Forms.Label();
			this._pageLabel = new System.Windows.Forms.Label();
			this._text = new System.Windows.Forms.TextBox();
			this._saveTimer = new System.Windows.Forms.Timer(this.components);
			this._titleLabel = new System.Windows.Forms.Label();
			this._statusPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// _statusPanel
			// 
			this._statusPanel.Controls.Add(this._versionLabel);
			this._statusPanel.Controls.Add(this._dateLabel);
			this._statusPanel.Controls.Add(this._pageLabel);
			this._statusPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
			this._statusPanel.Location = new System.Drawing.Point(0, 304);
			this._statusPanel.Name = "_statusPanel";
			this._statusPanel.Size = new System.Drawing.Size(467, 49);
			this._statusPanel.TabIndex = 0;
			// 
			// _versionLabel
			// 
			this._versionLabel.Font = new System.Drawing.Font("Verdana", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this._versionLabel.Location = new System.Drawing.Point(3, 26);
			this._versionLabel.Name = "_versionLabel";
			this._versionLabel.Size = new System.Drawing.Size(313, 23);
			this._versionLabel.TabIndex = 0;
			this._versionLabel.Text = "Version 10 of 100";
			// 
			// _dateLabel
			// 
			this._dateLabel.Font = new System.Drawing.Font("Verdana", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this._dateLabel.Location = new System.Drawing.Point(3, 3);
			this._dateLabel.Name = "_dateLabel";
			this._dateLabel.Size = new System.Drawing.Size(313, 23);
			this._dateLabel.TabIndex = 0;
			this._dateLabel.Text = "Wednesday, 25 August 2010, 23:11";
			// 
			// _pageLabel
			// 
			this._pageLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this._pageLabel.Font = new System.Drawing.Font("Verdana", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this._pageLabel.Location = new System.Drawing.Point(207, 9);
			this._pageLabel.Name = "_pageLabel";
			this._pageLabel.Size = new System.Drawing.Size(257, 23);
			this._pageLabel.TabIndex = 1;
			this._pageLabel.Text = "Page 3 of 10";
			this._pageLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// _text
			// 
			this._text.AcceptsReturn = true;
			this._text.Dock = System.Windows.Forms.DockStyle.Fill;
			this._text.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this._text.Location = new System.Drawing.Point(0, 25);
			this._text.MaxLength = 0;
			this._text.Multiline = true;
			this._text.Name = "_text";
			this._text.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this._text.Size = new System.Drawing.Size(467, 279);
			this._text.TabIndex = 0;
			this._text.TextChanged += new System.EventHandler(this._text_TextChanged);
			this._text.KeyDown += new System.Windows.Forms.KeyEventHandler(this.BookView_KeyDown);
			this._text.MouseUp += new System.Windows.Forms.MouseEventHandler(this._text_MouseUp);
			// 
			// _saveTimer
			// 
			this._saveTimer.Enabled = true;
			this._saveTimer.Interval = 1000;
			this._saveTimer.Tick += new System.EventHandler(this._saveTimer_Tick);
			// 
			// _titleLabel
			// 
			this._titleLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this._titleLabel.Dock = System.Windows.Forms.DockStyle.Top;
			this._titleLabel.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this._titleLabel.Location = new System.Drawing.Point(0, 0);
			this._titleLabel.Name = "_titleLabel";
			this._titleLabel.Size = new System.Drawing.Size(467, 25);
			this._titleLabel.TabIndex = 1;
			this._titleLabel.Text = "Title";
			this._titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// BookView
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._text);
			this.Controls.Add(this._titleLabel);
			this.Controls.Add(this._statusPanel);
			this.Name = "BookView";
			this.Size = new System.Drawing.Size(467, 353);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.BookView_KeyDown);
			this._statusPanel.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Panel _statusPanel;
		private System.Windows.Forms.Label _pageLabel;
		private System.Windows.Forms.Label _dateLabel;
		private System.Windows.Forms.TextBox _text;
		private System.Windows.Forms.Label _versionLabel;
		private System.Windows.Forms.Timer _saveTimer;
		private System.Windows.Forms.Label _titleLabel;
	}
}
