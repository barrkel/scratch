using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Barrkel.ScratchPad
{
	public partial class BookView : UserControl
	{
		DateTime _lastModification;
		DateTime _lastSave;
		bool _dirty;
		int _currentPage;
		string _textContents;
		// if iterator is non-null, then we are browsing history
		ScratchIterator _currentIterator;
		bool _settingText;
		
		public BookView()
		{
			InitializeComponent();
		}
		
		public BookView(ScratchBook book)
		{
			InitializeComponent();
			Book = book;
			_currentPage = book.Pages.Count > 0 ? book.Pages.Count - 1 : 0;
			UpdateViewLabels();
			UpdateTextBox();
		}
		
		public ScratchBook Book
		{
			get; private set;
		}
		
		public void SetTextBoxFocus()
		{
			_text.Focus();
			_text.ScrollToCaret();
		}
		
		void UpdateTextBox()
		{
			_settingText = true;
			try
			{
				if (_currentPage >= Book.Pages.Count)
					_text.Text = "";
				else if (_currentIterator != null)
					_text.Text = _currentIterator.Text;
				else
					_text.Text = Book.Pages[_currentPage].Text;
				_text.Select(_text.Text.Length, 0);
				_text.ScrollToCaret();
			}
			finally
			{
				_settingText = false;
			}
		}
		
		void UpdateTitle()
		{
			_titleLabel.Text = new StringReader(_text.Text).ReadLine();
		}
		
		void UpdateViewLabels()
		{
			if (_currentPage >= Book.Pages.Count)
			{
				_dateLabel.Text = "";
				_pageLabel.Text = "";
				_versionLabel.Text = "";
			}
			else
			{
				_pageLabel.Text = string.Format("Page {0} of {1}", _currentPage + 1, Book.Pages.Count);
				if (_currentIterator == null)
				{
					_versionLabel.Text = "Latest";
					_dateLabel.Text = Book.Pages[_currentPage].ChangeStamp.ToLocalTime().ToString("F");
				}
				else
				{
					_versionLabel.Text = string.Format("Version {0} of {1}", _currentIterator.Position,
						_currentIterator.Count);
					_dateLabel.Text = _currentIterator.Stamp.ToLocalTime().ToString("F");
				}
			}
		}
		
		private void EnsureSaved()
		{
			if (!_dirty)
				return;
			if (_currentPage >= Book.Pages.Count)
			{
				Book.AddPage();
				UpdateViewLabels();
			}
			Book.Pages[_currentPage].Text = _textContents;
			Book.SaveLatest();
			_lastSave = DateTime.UtcNow;
			_dirty = false;
			_currentPage = Book.MoveToEnd(_currentPage);
			UpdateViewLabels();
		}
		
		private void _text_TextChanged(object sender, EventArgs e)
		{
			UpdateTitle();
			if (_settingText)
				return;
			
			if (!_dirty)
				_lastSave = DateTime.UtcNow;
			_lastModification = DateTime.UtcNow;
			if (_text.Text == "")
				EnsureSaved();
			_textContents = _text.Text;
			if (_textContents == "")
			{
				_currentPage = Book.Pages.Count;
				Book.AddPage();
				UpdateViewLabels();
			}
			_currentIterator = null;
			_dirty = true;
		}
		
		private void _saveTimer_Tick(object sender, EventArgs e)
		{
			if (!_dirty)
				return;
			TimeSpan span = DateTime.UtcNow - _lastModification;
			if (span > TimeSpan.FromSeconds(5))
			{
				EnsureSaved();
				return;
			}
			span = DateTime.UtcNow - _lastSave;
			if (span > TimeSpan.FromSeconds(20))
			{
				EnsureSaved();
				return;
			}
		}
		
		void PreviousVersion()
		{
			EnsureSaved();
			if (_currentPage >= Book.Pages.Count)
				return;
			if (_currentIterator == null)
			{
				_currentIterator = Book.Pages[_currentPage].GetIterator();
				_currentIterator.MoveToEnd();
			}
			if (_currentIterator.MovePrevious())
			{
				UpdateTextBox();
				UpdateViewLabels();
			}
		}
		
		void NextVersion()
		{
			EnsureSaved();
			if (_currentPage >= Book.Pages.Count)
				return;
			if (_currentIterator == null)
				return;
			if (_currentIterator.MoveNext())
			{
				UpdateTextBox();
				UpdateViewLabels();
			}
		}
		
		void PreviousPage()
		{
			_currentIterator = null;
			if (_currentPage > 0)
			{
				EnsureSaved();
				--_currentPage;
				UpdateTextBox();
				UpdateViewLabels();
			}
		}
		
		void NextPage()
		{
			_currentIterator = null;
			EnsureSaved();
			if (_currentPage < Book.Pages.Count && Book.Pages[_currentPage].Text != "")
			{
				++_currentPage;
				UpdateTextBox();
				UpdateViewLabels();
			}
		}
		
		public void JumpToPage(int pageIndex)
		{
			if (pageIndex < 0 || pageIndex >= Book.Pages.Count)
				return;
			EnsureSaved();
			_currentIterator = null;
			_currentPage = pageIndex;
			UpdateTextBox();
			UpdateTitle();
			UpdateViewLabels();
		}

		private void BookView_KeyDown(object sender, KeyEventArgs e)
		{
			e.Handled = false;
			
			if (e.Modifiers == Keys.Control)
			{
				switch (e.KeyCode)
				{
					case Keys.A:
						_text.SelectAll();
						break;
						
					default:
						return;
				}
			}
			else if (e.Modifiers == Keys.Alt)
			{
				switch (e.KeyCode)
				{
					case Keys.Home:
					case Keys.Up:
						PreviousVersion();
						break;
					
					case Keys.End:
					case Keys.Down:
						NextVersion();
						break;
						
					case Keys.PageUp:
					case Keys.Left:
						PreviousPage();
						break;

					case Keys.PageDown:
					case Keys.Right:
						NextPage();
						break;

					default:
						return;
				}
			}
			else
				return;
			
			e.Handled = true;
		}
		
		private void _text_MouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Middle)
			{
				if (_text.SelectionLength > 0)
					Clipboard.SetText(_text.SelectedText);
				else if (Clipboard.ContainsText())
				{
					_text.SelectedText = Clipboard.GetText();
					_text.SelectionStart = _text.SelectionStart + _text.SelectionLength;
					_text.SelectionLength = 0;
					_text.ScrollToCaret();
				}
			}
		}
	}
}
