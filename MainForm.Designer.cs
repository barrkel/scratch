namespace Barrkel.ScratchPad
{
	partial class MainForm
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
			this._mainTabs = new System.Windows.Forms.TabControl();
			this.SuspendLayout();
			// 
			// _mainTabs
			// 
			this._mainTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this._mainTabs.Location = new System.Drawing.Point(12, 12);
			this._mainTabs.Name = "_mainTabs";
			this._mainTabs.SelectedIndex = 0;
			this._mainTabs.Size = new System.Drawing.Size(723, 459);
			this._mainTabs.TabIndex = 0;
			this._mainTabs.TabStop = false;
			this._mainTabs.Selected += new System.Windows.Forms.TabControlEventHandler(this._mainTabs_Selected);
			this._mainTabs.SelectedIndexChanged += new System.EventHandler(this._mainTabs_SelectedIndexChanged);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(747, 483);
			this.Controls.Add(this._mainTabs);
			this.KeyPreview = true;
			this.Name = "MainForm";
			this.Text = "ScratchPad";
			this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.MainForm_PreviewKeyDown);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyDown);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TabControl _mainTabs;
	}
}

