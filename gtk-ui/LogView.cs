using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gtk;
using Barrkel.ScratchPad;
using System.IO;

namespace Barrkel.GtkScratchPad
{
	public class LogView : Frame
	{
		static readonly int MaxLength = 100_000;
		static readonly int TrimLength = 50_000;
		TextView _textView;
		int _length;

		public LogView(ScratchScope settings, Window appWindow)
		{
			AppSettings = settings;
			AppWindow = appWindow;
			InitComponent();
		}

		public Window AppWindow { get; private set; }
		public ScratchScope AppSettings { get; private set; }
		
		private void InitComponent()
		{
			Gdk.Color grey = new Gdk.Color(0xF0, 0xF0, 0xF0);
			var textFont = Pango.FontDescription.FromString(AppSettings.GetOrDefault("log-font", null));
			if (textFont == null)
				textFont = Pango.FontDescription.FromString(AppSettings.GetOrDefault("text-font", "Courier New"));

			_textView = new MyTextView
			{
				WrapMode = WrapMode.Word
			};
			_textView.ModifyBase(StateType.Normal, grey);
			// _textView.Buffer.Changed += _text_TextChanged;
			// _textView.KeyDownEvent += _textView_KeyDownEvent;
			_textView.Editable = false;
			_textView.ModifyFont(textFont);

			ScrolledWindow scrolledTextView = new ScrolledWindow();
			scrolledTextView.Add(_textView);

			VBox outerVertical = new VBox();
			outerVertical.PackStart(scrolledTextView, true, true, 0);

			Add(outerVertical);
			
			BorderWidth = 5;
		}

		public void AppendLine(string text)
		{
			text += "\n";
			TextIter end = _textView.Buffer.GetIterAtOffset(_length);
			_length += text.Length;
			_textView.Buffer.Insert(ref end, text);
			if (_length > MaxLength)
				Trim();
			_textView.ScrollToIter(
				_textView.Buffer.GetIterAtOffset(_length), 0, false, 0, 0);
		}

		public void Trim()
		{
			string text = _textView.Buffer.Text;
			string[] lines = text.Split(new char[] { '\n' }, StringSplitOptions.None);

			int startLine = 0;
			int len = 0;
			for (int i = lines.Length - 1; i >= 0; --i)
			{
				string line = lines[i];
				len += line.Length;
				if (len > TrimLength)
				{
					startLine = i + 1;
					break;
				}
			}
			StringBuilder result = new StringBuilder();
			for (int i = startLine; i < lines.Length; ++i)
				result.AppendLine(lines[i]);
			_length = result.Length;
			_textView.Buffer.Text = result.ToString();
		}
	}
}
