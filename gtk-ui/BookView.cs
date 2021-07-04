using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gtk;
using Barrkel.ScratchPad;
using System.IO;

namespace Barrkel.GtkScratchPad
{
	public static class StringHelper
	{
		public static string EscapeMarkup(this string text)
		{
			return (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;");
		}
	}

	public static class GdkHelper
	{
		private static Dictionary<Gdk.Key, string> _keyNameMap = BuildKeyNameMap();

		public static bool TryGetKeyName(Gdk.Key key, out string name)
		{
			return _keyNameMap.TryGetValue(key, out name);
		}

		private static Dictionary<Gdk.Key, string> BuildKeyNameMap()
		{
			Dictionary<Gdk.Key, string> result = new Dictionary<Gdk.Key, string>();
			Dictionary<string, Gdk.Key> enumMap = new Dictionary<string, Gdk.Key>();

			void tryAddNamed(string enumName, string name)
			{
				if (enumMap.TryGetValue(enumName, out Gdk.Key key))
					result.Add(key, name);
			}

			void tryAdd(string name)
			{
				tryAddNamed(name, name);
			}

			foreach (Gdk.Key key in Enum.GetValues(typeof(Gdk.Key)))
			{
				string enumName = Enum.GetName(typeof(Gdk.Key), key);
				enumMap[enumName] = key;
			}
			for (int i = 1; i <= 12; ++i)
			{
				string name = string.Format("F{0}", i);
				tryAdd(name);
			}
			for (char ch = 'A'; ch <= 'Z'; ++ch)
			{
				string name = ch.ToString();
				// Copy emacs logic for shift on normal characters.
				// map A to A
				tryAdd(name);
				// and a to a
				tryAdd(name.ToLower());
			}
			for (char ch = '0'; ch <= '9'; ++ch)
			{
				string name = ch.ToString();
				tryAddNamed("Key_" + name, name);
			}
			result.Add(Gdk.Key.quoteleft, "`");
			result.Add(Gdk.Key.quoteright, "'");
			result.Add(Gdk.Key.quotedbl, "\"");
			result.Add(Gdk.Key.exclam, "!");
			result.Add(Gdk.Key.at, "@");
			result.Add(Gdk.Key.numbersign, "#");
			result.Add(Gdk.Key.dollar, "$");
			result.Add(Gdk.Key.percent, "%");
			result.Add(Gdk.Key.asciicircum, "^");
			result.Add(Gdk.Key.ampersand, "&");
			result.Add(Gdk.Key.asterisk, "*");
			result.Add(Gdk.Key.parenleft, "(");
			result.Add(Gdk.Key.parenright, ")");
			result.Add(Gdk.Key.bracketleft, "[");
			result.Add(Gdk.Key.bracketright, "]");
			result.Add(Gdk.Key.braceleft, "{");
			result.Add(Gdk.Key.braceright, "}");
			result.Add(Gdk.Key.plus, "+");
			result.Add(Gdk.Key.minus, "-");
			result.Add(Gdk.Key.underscore, "_");
			result.Add(Gdk.Key.equal, "=");
			result.Add(Gdk.Key.slash, "/");
			result.Add(Gdk.Key.backslash, "\\");
			result.Add(Gdk.Key.bar, "|");
			result.Add(Gdk.Key.period, ".");
			result.Add(Gdk.Key.comma, ",");
			result.Add(Gdk.Key.less, "<");
			result.Add(Gdk.Key.greater, ">");
			result.Add(Gdk.Key.colon, ":");
			result.Add(Gdk.Key.semicolon, ";");
			result.Add(Gdk.Key.Escape, "Esc");
			result.Add(Gdk.Key.asciitilde, "~");
			result.Add(Gdk.Key.Page_Up, "PgUp");
			result.Add(Gdk.Key.Page_Down, "PgDn");
			result.Add(Gdk.Key.Home, "Home");
			result.Add(Gdk.Key.End, "End");

			result.Add(Gdk.Key.Up, "Up");
			result.Add(Gdk.Key.Down, "Down");
			result.Add(Gdk.Key.Left, "Left");
			result.Add(Gdk.Key.Right, "Right");

			result.Add(Gdk.Key.Return, "Return");
			result.Add(Gdk.Key.Delete, "Delete");
			result.Add(Gdk.Key.Insert, "Insert");
			result.Add(Gdk.Key.BackSpace, "BackSpace");
			result.Add(Gdk.Key.space, "Space");
			result.Add(Gdk.Key.Tab, "Tab");
			// this comes out with S-Tab
			result.Add(Gdk.Key.ISO_Left_Tab, "Tab");

			return result;
		}
	}

	public delegate void KeyEventHandler(Gdk.EventKey evnt, ref bool handled);

	public class MyTextView : TextView
	{
		public event KeyEventHandler KeyDownEvent;

		[GLib.DefaultSignalHandler(Type = typeof(Widget), ConnectionMethod = "OverrideKeyPressEvent")]
		protected override bool OnKeyPressEvent(Gdk.EventKey evnt)
		{
			bool handled = false;
			KeyDownEvent?.Invoke(evnt, ref handled);
			if (handled)
				return true;
			else
				return base.OnKeyPressEvent(evnt);
		}

	}

	public class BookView : Frame, IScratchBookView
	{
		DateTime _lastModification;
		DateTime _lastSave;
		bool _dirty;
		int _currentPage;
		string _textContents;
		// If non-null, then browsing history.
		ScratchIterator _currentIterator;
		MyTextView _textView;
		bool _settingText;
		Label _titleLabel;
		Label _dateLabel;
		Label _pageLabel;
		Label _versionLabel;
		List<System.Action> _deferred = new List<System.Action>();
		ScratchBookController _controller;
		
		public BookView(ScratchBook book, ScratchBookController controller, Settings appSettings)
		{
			AppSettings = appSettings;
			Book = book;
			InitComponent();
			_currentPage = book.Pages.Count > 0 ? book.Pages.Count - 1 : 0;
			_controller = controller;
			UpdateViewLabels();
			UpdateTextBox();

			_controller.ConnectView(this);
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
			Book.EnsureSaved();
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
			// TODO: load these settings from a config page
			var infoFont = Pango.FontDescription.FromString(AppSettings.Get("info-font", "Verdana"));
			var textFont = Pango.FontDescription.FromString(AppSettings.Get("text-font", "Courier New"));

			_textView = new MyTextView
			{
				WrapMode = WrapMode.Word
			};
			_textView.ModifyBase(StateType.Normal, lightBlue);
			_textView.Buffer.Changed += _text_TextChanged;
			_textView.KeyDownEvent += _textView_KeyDownEvent;
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

			_dateLabel = new Label
			{
				Justify = Justification.Left
			};
			_dateLabel.SetAlignment(0, 0);
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

			_pageLabel = new Label
			{
				Markup = GetPageMarkup(1, 5),
				Justify = Justification.Right
			};
			_pageLabel.SetAlignment(1, 0.5f);
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

		void Defer(System.Action action)
		{
			_deferred.Add(action);
			GLib.Idle.Add(DrainDeferred);
		}

		bool DrainDeferred()
		{
			if (_deferred.Count == 0)
				return false;

			System.Action defer = _deferred[_deferred.Count - 1];
			_deferred.RemoveAt(_deferred.Count - 1);
			defer();

			return true;
		}

		void _textView_KeyDownEvent(Gdk.EventKey evnt, ref bool handled)
		{
			// FIXME: this event actually doesn't grab that much.
			// E.g. normal Up, Down, Ctrl-Left, Ctrl-Right etc. are not seen here
			// It doesn't see typed characters. Effectively it only sees F-keys and keys pressed with Alt.
			bool ctrl = (evnt.State & Gdk.ModifierType.ControlMask) != 0;
			bool alt = (evnt.State & Gdk.ModifierType.Mod1Mask) != 0;
			bool shift = (evnt.State & Gdk.ModifierType.ShiftMask) != 0;

			if (GdkHelper.TryGetKeyName(evnt.Key, out string keyName))
			{
				handled = _controller.InformKeyStroke(this, keyName, ctrl, alt, shift);
			}
			else 
			{
				Console.WriteLine("Not mapped: {0}", evnt.Key);
			}

			var state = evnt.State & Gdk.ModifierType.Mod1Mask;
			switch (state)
			{
				case Gdk.ModifierType.Mod1Mask:
					switch (evnt.Key)
					{
						case Gdk.Key.Home: // M-Home
						case Gdk.Key.Up: // M-Up
							PreviousVersion();
							break;
						
						case Gdk.Key.End: // M-End
						case Gdk.Key.Down: // M-Down
							NextVersion();
							break;
						
						case Gdk.Key.Page_Up: // M-PgUp
						case Gdk.Key.Left: // M-Left
							PreviousPage();
							break;
						
						case Gdk.Key.Page_Down: // M-PgDn
						case Gdk.Key.Right: // M-Right
							NextPage();
							break;

						default:
							return;
					}
					break;
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
				SetSelection(_currentIterator.UpdatedFrom, _currentIterator.UpdatedTo);
				// This scroll needs to be deferred to the idle loop
				// the UI hasn't updated yet and the scroll command is effectively discarded
				SetScrollPos(_currentIterator.UpdatedFrom);
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
				SetSelection(_currentIterator.UpdatedFrom, _currentIterator.UpdatedTo);
				SetScrollPos(_currentIterator.UpdatedFrom);
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
			_currentPage = Book.MoveToEnd(pageIndex);
			UpdateTextBox();
			UpdateTitle();
			UpdateViewLabels();
		}

		public void InsertText(string text)
		{
			_textView.Buffer.InsertAtCursor(text);
		}

		public void SetScrollPos(int pos)
		{
			Defer(() =>
			{
				TextIter iter = _textView.Buffer.GetIterAtOffset(pos);
				_textView.ScrollToIter(iter, 0, false, 0, 0);
			});
		}

		public void SetSelection(int from, int to)
		{
			// hack: just highlight first few characters
			to = from + 4;
			if (from > to)
			{
				int t = from;
				from = to;
				to = t;
			}
			if (from == to)
			{
				to = from + 1;
			}
			_textView.Buffer.MoveMark("insert", _textView.Buffer.GetIterAtOffset(from));
			_textView.Buffer.MoveMark("selection_bound", _textView.Buffer.GetIterAtOffset(to));
		}

		public void AddRepeatingTimer(int millis, string actionName)
		{
			GLib.Timeout.Add((uint) millis, () => 
			{
				_controller.InvokeAction(this, actionName, EmptyArray<string>.Value);
				return true; 
			});
		}

		public int CurrentPosition
		{
			get => _textView.Buffer.CursorPosition;
			set => _textView.Buffer.MoveMark("insert", _textView.Buffer.GetIterAtOffset(value));
		}

		public int CurrentPageIndex
		{
			get => _currentPage;
		}

		public ScratchBook Book
		{
			get; private set;
		}

		string IScratchBookView.CurrentText
		{
			get => _textView.Buffer.Text;
		}
	}
}
