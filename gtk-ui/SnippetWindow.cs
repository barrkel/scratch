using Gtk;
using Barrkel.ScratchPad;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barrkel.GtkScratchPad
{
	public class SnippetWindow : Window
	{
		TextView _textView;

		public SnippetWindow(ScratchScope settings) : base("Snippet")
		{
			Scope = settings;
			InitComponent();
		}

		public ScratchScope Scope { get; }

		private void InitComponent()
		{
			Resize(500, 500);
			_textView = new TextView
			{
				WrapMode = WrapMode.Word
			};

			Title= Scope.GetOrDefault("title", "Snippet");

			Gdk.Color backgroundColor = new Gdk.Color(255, 255, 200);
			if (Scope.TryLookup("snippet-color", out var colorSetting))
				Gdk.Color.Parse(colorSetting.StringValue, ref backgroundColor);

			var textFont = Pango.FontDescription.FromString(Scope.GetOrDefault("text-font", "Courier New"));

			_textView.ModifyBase(StateType.Normal, backgroundColor);
			_textView.ModifyFont(textFont);
			_textView.Editable = false;

			_textView.Buffer.Text = Scope.Lookup("text").StringValue;

			ScrolledWindow scrolledTextView = new ScrolledWindow();
			scrolledTextView.Add(_textView);

			Add(scrolledTextView);

			BorderWidth = 0;
		}
	}
}

