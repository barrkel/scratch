namespace Barrkel.ScratchPad
{
	partial class TitleSearchForm
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
			this._searchResultsBox = new System.Windows.Forms.ListBox();
			this._searchText = new System.Windows.Forms.TextBox();
			this.SuspendLayout();
			// 
			// _searchResultsBox
			// 
			this._searchResultsBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this._searchResultsBox.FormattingEnabled = true;
			this._searchResultsBox.Location = new System.Drawing.Point(12, 38);
			this._searchResultsBox.Name = "_searchResultsBox";
			this._searchResultsBox.Size = new System.Drawing.Size(676, 329);
			this._searchResultsBox.TabIndex = 1;
			// 
			// _searchText
			// 
			this._searchText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this._searchText.Location = new System.Drawing.Point(12, 12);
			this._searchText.Name = "_searchText";
			this._searchText.Size = new System.Drawing.Size(676, 20);
			this._searchText.TabIndex = 0;
			this._searchText.TextChanged += new System.EventHandler(this._searchText_TextChanged);
			// 
			// TitleSearchForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(700, 379);
			this.Controls.Add(this._searchText);
			this.Controls.Add(this._searchResultsBox);
			this.KeyPreview = true;
			this.Name = "TitleSearchForm";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Search By Title";
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TitleSearchForm_KeyDown);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ListBox _searchResultsBox;
		private System.Windows.Forms.TextBox _searchText;
	}
}