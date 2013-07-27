using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Barrkel.ScratchPad;

namespace Barrkel.ScratchPad
{
	public partial class MainForm : Form
	{
		public MainForm()
		{
			InitializeComponent();
		}
		
		public MainForm(ScratchRoot root)
		{
			InitializeComponent();
			Root = root;
			FullUpdateView();
		}
		
		public ScratchRoot Root
		{
			get; private set;
		}
		
		TabPage CreateBookView(ScratchBook book)
		{
			TabPage result = new TabPage(book.ToString());
			BookView view = new BookView(book);
			view.Dock = DockStyle.Fill;
			result.Controls.Add(view);
			result.Tag = view;
			return result;
		}
		
		void FullUpdateView()
		{
			this.SuspendLayout();
			try
			{
				_mainTabs.TabPages.Clear();
				foreach (var book in Root.Books)
					_mainTabs.TabPages.Add(CreateBookView(book));
			}
			finally
			{
				this.ResumeLayout();
			}
		}
		
		private void _mainTabs_Selected(object sender, TabControlEventArgs e)
		{
			((BookView) e.TabPage.Tag).SetTextBoxFocus();
		}
		
		private void _mainTabs_SelectedIndexChanged(object sender, EventArgs e)
		{
			((BookView) _mainTabs.SelectedTab.Tag).SetTextBoxFocus();
		}

		private void MainForm_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.F12:
					e.IsInputKey = false;
					TitleSearchForm.RunSearch(this, ((BookView) _mainTabs.SelectedTab.Tag).Book);
					break;
				
				default:
					return;
			}
		}

		private void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.F12:
				{
					e.Handled = true;
					int found = TitleSearchForm.RunSearch(this, ((BookView) _mainTabs.SelectedTab.Tag).Book);
					if (found >= 0)
						((BookView) _mainTabs.SelectedTab.Tag).JumpToPage(found);
					break;
				}
				
				default:
					e.Handled = false;
					break;
			}
		}
	}
}
