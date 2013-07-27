using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Barrkel.ScratchPad
{
	public partial class TitleSearchForm : Form
	{
		ScratchBook _book;
		
		public TitleSearchForm(ScratchBook book)
		{
			_book = book;
			InitializeComponent();
			UpdateSearchBox();
		}
		
		public int PageIndex
		{
			get
			{
				if (_searchResultsBox.Items.Count == 0 || _searchResultsBox.SelectedItem == null)
					return -1;
				return ((TitleSearchResult) _searchResultsBox.SelectedItem).Index;
			}
		}
		
		public static int RunSearch(IWin32Window owner, ScratchBook book)
		{
			using (TitleSearchForm f = new TitleSearchForm(book))
			{
				switch (f.ShowDialog(owner))
				{
					case DialogResult.OK:
						return f.PageIndex;
						
					default:
						return -1;
				}
			}
		}
		
		private void UpdateSearchBox()
		{
			_searchResultsBox.BeginUpdate();
			try
			{
				_searchResultsBox.Items.Clear();
				foreach (var m in _book.SearchTitles(_searchText.Text))
				{
					_searchResultsBox.Items.Add(new TitleSearchResult(m.Key, m.Value));
					if (_searchResultsBox.Items.Count > 100)
						break;
				}
				if (_searchResultsBox.Items.Count == 1)
					_searchResultsBox.SelectedIndex = 0;
				else
					_searchResultsBox.SelectedIndex = -1;
			}
			finally
			{
				_searchResultsBox.EndUpdate();
			}
		}
		
		private void _searchText_TextChanged(object sender, EventArgs e)
		{
			UpdateSearchBox();
		}
		
		private void TitleSearchForm_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Escape:
					DialogResult = DialogResult.Cancel;
					e.Handled = true;
					break;
				
				case Keys.Enter:
					DialogResult = DialogResult.OK;
					e.Handled = true;
					break;
				
				case Keys.Up:
				{
					e.Handled = true;
					if (_searchResultsBox.Items.Count > 0)
					{
						if (_searchResultsBox.SelectedIndex <= 0)
							_searchResultsBox.SelectedIndex = 0;
						else
							_searchResultsBox.SelectedIndex--;
					}
					break;
				}
				
				case Keys.Down:
				{
					e.Handled = true;
					if (_searchResultsBox.Items.Count > 0)
					{
						if (_searchResultsBox.SelectedIndex >= _searchResultsBox.Items.Count - 1)
							_searchResultsBox.SelectedIndex = _searchResultsBox.Items.Count - 1;
						else
							_searchResultsBox.SelectedIndex++;
					}
					break;
				}
			}
		}
	}
	
	class TitleSearchResult
	{
		public TitleSearchResult(string title, int index)
		{
			Title = title;
			Index = index;
		}
		
		public string Title { get; private set; }
		public int Index { get; private set; }

		public override string ToString()
		{
			return Title;
		}
	}
}
