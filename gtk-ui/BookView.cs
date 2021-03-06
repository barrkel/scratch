using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gtk;
using Barrkel.ScratchPad;
using System.IO;

namespace Barrkel.GtkScratchPad
{
	public static class Stringhelper
	{
		public static string EscapeMarkup(this string text)
		{
			return (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;");
		}
	}
	
	public class BookView : Frame
	{
		DateTime _lastModification;
		DateTime _lastSave;
		bool _dirty;
		int _currentPage;
		string _textContents;
		// If non-null, then browsing history.
		ScratchIterator _currentIterator;
		TextView _textView;
		bool _settingText;
		Label _titleLabel;
		Label _dateLabel;
		Label _pageLabel;
		Label _versionLabel;
		
		public BookView(ScratchBook book, Settings appSettings)
		{
			AppSettings = appSettings;
			Book = book;
			InitComponent();
			_currentPage = book.Pages.Count > 0 ? book.Pages.Count - 1 : 0;
			UpdateViewLabels();
			UpdateTextBox();
		}
		
		public Settings AppSettings { get; private set; }
		
		private void UpdateTextBox()
		{
			_settingText = true;
			try
			{
				if (_currentPage >= Book.Pages.Count)
					_textView.Buffer.Text = "";
				else if (_currentIterator != null)
					_textView.Buffer.Text = _currentIterator.Text;
				else
					_textView.Buffer.Text = Book.Pages[_currentPage].Text;
				TextIter iter = _textView.Buffer.GetIterAtOffset(0);
				_textView.Buffer.SelectRange(iter, iter);
				_textView.ScrollToIter(iter, 0, false, 0, 0);
			}
			finally
			{
				_settingText = false;
			}
		}

		private void UpdateTitle()
		{
			_titleLabel.Markup = GetTitleMarkup(new StringReader(_textView.Buffer.Text).ReadLine());
		}

		private void UpdateViewLabels()
		{
			if (_currentPage >= Book.Pages.Count)
			{
				_dateLabel.Text = "";
				_pageLabel.Text = "";
				_versionLabel.Text = "";
			}
			else
			{
				_pageLabel.Markup = GetPageMarkup(_currentPage + 1, Book.Pages.Count);
				if (_currentIterator == null)
				{
					_versionLabel.Markup = GetInfoMarkup("Latest");
					_dateLabel.Markup = GetInfoMarkup(
						Book.Pages[_currentPage].ChangeStamp.ToLocalTime().ToString("F"));
				}
				else
				{
					_versionLabel.Markup = GetInfoMarkup(string.Format("Version {0} of {1}", 
						_currentIterator.Position, _currentIterator.Count));
					_dateLabel.Markup = GetInfoMarkup(_currentIterator.Stamp.ToLocalTime().ToString("F"));
				}
			}
		}

		public void EnsureSaved()
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
		
		private void SetTextBoxFocus()
		{
			// ???
		}

		private static string GetTitleMarkup(string title)
		{
			return string.Format("<span weight='ultrabold'>{0}</span>", title.EscapeMarkup());
		}
		
		private static string GetPageMarkup(int page, int total)
		{
			return string.Format("<span font='16'>Page {0} of {1}</span>", 
				page, total);
		}
	
		private static string GetInfoMarkup(string info)
		{
			return string.Format("<span font='11'>{0}</span>", info.EscapeMarkup());
		}
	
		private void InitComponent()
		{
			Gdk.Color grey = new Gdk.Color(0xA0, 0xA0, 0xA0);
			Gdk.Color lightBlue = new Gdk.Color(207, 207, 239);
			var infoFont = Pango.FontDescription.FromString(AppSettings.Get("info-font", "Verdana"));
			var textFont = Pango.FontDescription.FromString(AppSettings.Get("text-font", "Courier New"));
			
			_textView = new TextView();
			_textView.WrapMode = WrapMode.Word;
			_textView.ModifyBase(StateType.Normal, lightBlue);
			_textView.Buffer.Changed += _text_TextChanged;
			_textView.KeyPressEvent += _textView_KeyPressEvent;
			_textView.ModifyFont(textFont);

			ScrolledWindow scrolledTextView = new ScrolledWindow();
			scrolledTextView.Add(_textView);

			_titleLabel = new Label();
			_titleLabel.SetAlignment(0, 0);
			_titleLabel.Justify = Justification.Left;
			_titleLabel.SetPadding(0, 5);
			_titleLabel.ModifyFont(textFont);
			GLib.Timeout.Add((uint) TimeSpan.FromSeconds(3).TotalMilliseconds, 
				() => { return SaveTimerTick(); });
				
			EventBox titleContainer = new EventBox();
			titleContainer.Add(_titleLabel);

			// hbox
			//   vbox
			//     dateLabel
			//     versionLabel
			//   pageLabel
			HBox locationInfo = new HBox();
			VBox locationLeft = new VBox();
			
			_dateLabel = new Label();
			_dateLabel.SetAlignment(0, 0);
			_dateLabel.Justify = Justification.Left;
			_dateLabel.SetPadding(5, 5);
			_dateLabel.ModifyFont(infoFont);
			locationLeft.PackStart(_dateLabel, false, false, 0);
			
			_versionLabel = new Label();
			_versionLabel.SetAlignment(0, 0);
			_versionLabel.Justify = Justification.Left;
			_versionLabel.SetPadding(5, 5);
			_versionLabel.ModifyFont(infoFont);
			locationLeft.PackStart(_versionLabel, false, false, 0);
			
			locationInfo.PackStart(locationLeft, true, true, 0);
			
			_pageLabel = new Label();
			_pageLabel.Markup = GetPageMarkup(1, 5);
			_pageLabel.SetAlignment(1, 0.5f);
			_pageLabel.Justify = Justification.Right;
			_pageLabel.SetPadding(5, 5);
			_pageLabel.ModifyFont(infoFont);
			locationInfo.PackEnd(_pageLabel, true, true, 0);
			Pango.Context ctx = _pageLabel.PangoContext;

			VBox outerVertical = new VBox();
			outerVertical.PackStart(titleContainer, false, false, 0);
			outerVertical.PackStart(scrolledTextView, true, true, 0);
			outerVertical.PackEnd(locationInfo, false, false, 0);

			Add(outerVertical);
			
			BorderWidth = 5;
		}

		void _textView_KeyPressEvent(object o, KeyPressEventArgs args)
		{
			// Mod1 => alt
			var state = args.Event.State & (Gdk.ModifierType.ShiftMask | 
				Gdk.ModifierType.Mod1Mask | Gdk.ModifierType.ControlMask);
				
			if (state == Gdk.ModifierType.Mod1Mask)
			{
				switch (args.Event.Key)
				{
					case Gdk.Key.Home:
					case Gdk.Key.Up:
						PreviousVersion();
						break;
						
					case Gdk.Key.End:
					case Gdk.Key.Down:
						NextVersion();
						break;
						
					case Gdk.Key.Page_Up:
					case Gdk.Key.Left:
						PreviousPage();
						break;
						
					case Gdk.Key.Page_Down:
					case Gdk.Key.Right:
						NextPage();
						break;
						
					default:
						return;
				}
			}
		}

		private void _text_TextChanged(object sender, EventArgs e)
		{
			UpdateTitle();
			if (_settingText)
				return;

			if (!_dirty)
				_lastSave = DateTime.UtcNow;
			_lastModification = DateTime.UtcNow;
			if (_textView.Buffer.Text == "")
				EnsureSaved();
			_textContents = _textView.Buffer.Text;
			if (_textContents == "")
			{
				_currentPage = Book.Pages.Count;
				Book.AddPage();
				UpdateViewLabels();
			}
			_currentIterator = null;
			_dirty = true;
		}
		
		private bool SaveTimerTick()
		{
			if (!_dirty)
				return true;
				
			TimeSpan span = DateTime.UtcNow - _lastModification;
			if (span > TimeSpan.FromSeconds(5))
			{
				EnsureSaved();
				return true;
			}
			
			span = DateTime.UtcNow - _lastSave;
			if (span > TimeSpan.FromSeconds(20))
				EnsureSaved();
			
			return true;
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
		
		public ScratchBook Book
		{
			get; private set;
		}
	}
}
