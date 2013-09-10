using Gtk;
using Barrkel.ScratchPad;
using System;
using System.Collections.Generic;

namespace Barrkel.GtkScratchPad
{
	public enum ModalResult
	{
		None,
		OK,
		Cancel
	}
	
	public class ModalWindow : Window, IDisposable
	{
		public ModalWindow(string title) : base(title)
		{
		}

		protected override void OnHidden()
		{
			base.OnHidden();
			if (ModalResult == ModalResult.None)
				ModalResult = ModalResult.Cancel;
		}
		
		public override void Dispose()
		{
			base.Destroy();
			base.Dispose();
		}
		
		public ModalResult ModalResult
		{
			get; protected set;
		}
		
		public ModalResult ShowModal(Window parent)
		{
			ModalResult = ModalResult.None;
			Modal = true;
			TransientFor = parent;
			ShowAll();
			while (ModalResult == ModalResult.None)
				Application.RunIteration(true);
			return ModalResult;
		}

		protected override bool OnKeyPressEvent(Gdk.EventKey evnt)
		{
			switch (evnt.Key)
			{
				case Gdk.Key.Escape:
					ModalResult = ModalResult.Cancel;
					return false;
					
				default:
					return base.OnKeyPressEvent(evnt);
			}
		}
	}
	
	public class TitleSearchWindow : ModalWindow
	{
		TextView _searchTextView;
		TreeView _searchResultsView;
		ListStore _searchResultsStore;
		
		public TitleSearchWindow(ScratchBook book, Settings appSettings) : base("GTK ScratchPad")
		{
			Book = book;
			AppSettings = appSettings;
			InitComponent();
		}
		
		public static int RunSearch(Window parent, ScratchBook book, Settings settings)
		{
			using (TitleSearchWindow window = new TitleSearchWindow(book, settings))
			{
				switch (window.ShowModal(parent))
				{
					case ModalResult.OK:
						return 0;

					default:
						return -1;
				}
			}
		}
		
		public Settings AppSettings { get; private set; }
		public ScratchBook Book { get; private set; }
		
		private void InitComponent()
		{
			Resize(500, 500);

			var vbox = new VBox();

			Gdk.Color grey = new Gdk.Color(0xA0, 0xA0, 0xA0);
			Gdk.Color lightBlue = new Gdk.Color(207, 207, 239);
			var infoFont = Pango.FontDescription.FromString(AppSettings.Get("info-font", "Verdana"));
			var textFont = Pango.FontDescription.FromString(AppSettings.Get("text-font", "Courier New"));
			
			_searchTextView = new TextView();
			_searchTextView.ModifyBase(StateType.Normal, lightBlue);
			//_searchTextView.Buffer.Changed += _text_TextChanged;
			_searchTextView.KeyPressEvent += _searchTextView_KeyPressEvent;
			//_searchTextView.KeyPressEvent += _searchTextView_KeyPressEvent;
			_searchTextView.ModifyFont(textFont);
			_searchTextView.Buffer.Text = "Foo Bar Baz";

			
			_searchResultsStore = new ListStore(typeof(TitleSearchResult));
			for (int i = 0; i < 50; ++i)
				_searchResultsStore.SetValue(_searchResultsStore.Append(), 0, new TitleSearchResult("Item " + i, i));
			
			_searchResultsView = new TreeView(_searchResultsStore);
			TreeViewColumn column = new TreeViewColumn();
			column.Title = "Value";
			var valueRenderer = new CellRendererText();
			column.PackStart(valueRenderer, true);
			column.SetCellDataFunc(valueRenderer, (TreeViewColumn col, CellRenderer cell, TreeModel model, TreeIter iter) => 
			{
				TitleSearchResult item = (TitleSearchResult) model.GetValue(iter, 0);
				((CellRendererText)cell).Text = item.Title;
			});
			_searchResultsView.AppendColumn(column);
			
			
			_searchResultsView.ModifyBase(StateType.Normal, lightBlue);
			_searchResultsView.ButtonPressEvent += _searchResultsView_ButtonPressEvent;
			
			var scrolledResults = new ScrolledWindow();
			scrolledResults.Add(_searchResultsView);
			
			vbox.PackStart(_searchTextView, false, false, 0);
			vbox.PackStart(scrolledResults, true, true, 0);

			Add(vbox);
			
			BorderWidth = 5;
		}

		[GLib.ConnectBefore]
		void _searchTextView_KeyPressEvent(object o, KeyPressEventArgs args)
		{
			TreeSelection selection = _searchResultsView.Selection;
			Console.WriteLine("mode: {0}", selection.Mode);
			Console.WriteLine("count: {0}", selection.CountSelectedRows());
			TreeIter iter;
			if (selection.GetSelected(out iter))
			{
				object value = _searchResultsStore.GetValue(iter, 0);
				Console.WriteLine("value: {0}", value);
			}
			
			switch (args.Event.Key)
			{
				case Gdk.Key.Return:
					ModalResult = ModalResult.OK;
					args.RetVal = true;
					break;
					
				case Gdk.Key.Up:
					Console.WriteLine("up");
					args.RetVal = true;
					break;
					
				case Gdk.Key.Down:
					Console.WriteLine("down");
					args.RetVal = true;
					break;
			}
		}

		[GLib.ConnectBefore]
		void _searchResultsView_ButtonPressEvent(object o, ButtonPressEventArgs args)
		{
			Console.WriteLine("press {0}", args.Event.Type);
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

