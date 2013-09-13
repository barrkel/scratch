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
		int _searchResultsStoreCount; // asinine results store
		
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
						return window.SelectedItem.Value;

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
			_searchTextView.Buffer.Changed += (s, e) => { UpdateSearchBox(); };
			_searchTextView.KeyPressEvent += _searchTextView_KeyPressEvent;
			_searchTextView.ModifyFont(textFont);
			_searchTextView.Buffer.Text = "";

			
			_searchResultsStore = new ListStore(typeof(TitleSearchResult));
			
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

			UpdateSearchBox();
		}

		private void UpdateSearchBox()
		{
			_searchResultsStore.Clear();
			_searchResultsStoreCount = 0;
			
			foreach (var m in Book.SearchTitles(_searchTextView.Buffer.Text))
			{
				_searchResultsStore.SetValue(_searchResultsStore.Append(), 0, 
					new TitleSearchResult(m.Key, _searchResultsStoreCount, m.Value));
				++_searchResultsStoreCount;
				
				if (_searchResultsStoreCount >= 100)
					break;
			}
			if (_searchResultsStoreCount == 1)
				SelectedIndex = 0;
			else
				SelectedIndex = -1;
		}
		
		public int SelectedIndex
		{
			get
			{
				TreeIter iter;
				if (_searchResultsView.Selection.GetSelected(out iter))
					return ((TitleSearchResult) _searchResultsStore.GetValue(iter, 0)).Index;
				return -1;
			}
			set
			{
				var selection = _searchResultsView.Selection;
				if (value < 0 || value >= _searchResultsStoreCount)
				{
					selection.UnselectAll();
					return;
				}
				
				TreeIter iter;
				if (!_searchResultsStore.IterNthChild(out iter, value))
					return;
				
				selection.SelectIter(iter);
			}
		}
		
		internal TitleSearchResult SelectedItem
		{
			get
			{
				TreeIter iter;
				if (_searchResultsView.Selection.GetSelected(out iter))
					return (TitleSearchResult) _searchResultsStore.GetValue(iter, 0);
				return null;
			}
		}

		[GLib.ConnectBefore]
		void _searchTextView_KeyPressEvent(object o, KeyPressEventArgs args)
		{
			int selectedIndex = SelectedIndex;
			
			switch (args.Event.Key)
			{
				case Gdk.Key.Return:
					ModalResult = ModalResult.OK;
					args.RetVal = true;
					break;
					
				case Gdk.Key.Up:
					// FIXME: make these things scroll the selection into view
					args.RetVal = true;
					if (_searchResultsStoreCount > 0)
					{
						--selectedIndex;
						if (selectedIndex <= 0)
							selectedIndex = 0;
						SelectedIndex = selectedIndex;
					}
					break;
					
				case Gdk.Key.Down:
					args.RetVal = true;
					if (_searchResultsStoreCount > 0)
					{
						++selectedIndex;
						if (selectedIndex >= _searchResultsStoreCount)
							selectedIndex = _searchResultsStoreCount - 1;
						SelectedIndex = selectedIndex;
					}
					break;
			}
		}

		[GLib.ConnectBefore]
		void _searchResultsView_ButtonPressEvent(object o, ButtonPressEventArgs args)
		{
			// consider handling double-click
		}
	}
	
	class TitleSearchResult
	{
		public TitleSearchResult(string title, int index, int value)
		{
			Title = title;
			Index = index;
			Value = value;
		}
		
		public string Title { get; private set; }
		public int Index { get; private set; }
		public int Value { get; private set; }

		public override string ToString()
		{
			return Title;
		}
	}
}

