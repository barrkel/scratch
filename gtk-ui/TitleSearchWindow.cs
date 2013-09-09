using Gtk;
using Barrkel.ScratchPad;
using System;
using System.Collections.Generic;

namespace Barrkel.GtkScratchPad
{
	public class TitleSearchWindow : Window
	{
		TextView _searchTextView;
		TreeView _searchResultsView;
		
		public TitleSearchWindow(ScratchBook book, Settings appSettings) : base("GTK ScratchPad")
		{
			Book = book;
			AppSettings = appSettings;
			InitComponent();
		}
		
		public static RunSearch(Window parent, ScratchBook book, Settings settings)
		{
			TitleSearchWindow window = new TitleSearchWindow(book, settings);
			window.TransientFor = parent;
			window.Modal = true;
			window.Show();
			while (Application.EventsPending && window.ModalResult != ModalResult.None)
				Application.RunIteration();
		}
		
		
		public Settings AppSettings { get; private set; }
		public ScratchBook Book { get; private set; }
		
		private void InitComponent()
		{
			Resize(600, 600);

			var vbox = new VBox();

			Gdk.Color grey = new Gdk.Color(0xA0, 0xA0, 0xA0);
			Gdk.Color lightBlue = new Gdk.Color(207, 207, 239);
			var infoFont = Pango.FontDescription.FromString(AppSettings.Get("info-font", "Verdana"));
			var textFont = Pango.FontDescription.FromString(AppSettings.Get("text-font", "Courier New"));
			
			_searchTextView = new TextView();
			_searchTextView.ModifyBase(StateType.Normal, lightBlue);
			//_searchTextView.Buffer.Changed += _text_TextChanged;
			//_searchTextView.KeyPressEvent += _searchTextView_KeyPressEvent;
			_searchTextView.ModifyFont(textFont);

			_searchResultsView = new TreeView();
			_searchResultsView.ModifyBase(StateType.Normal, lightBlue);

			var scrolledResults = new ScrolledWindow();
			scrolledResults.Add(_searchResultsView);
			
			vbox.PackStart(_searchTextView, false, false, 0);
			vbox.PackStart(scrolledResults, true, true, 0);

			Add(vbox);
			
			BorderWidth = 5;
		}
	}
}

