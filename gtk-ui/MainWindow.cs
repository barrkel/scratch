using Gtk;
using Barrkel.ScratchPad;
using System;
using System.Collections.Generic;

namespace Barrkel.GtkScratchPad
{
	public class MainWindow : Window
	{
		Notebook _notebook;
		
		public MainWindow(ScratchRoot root, Settings appSettings) : base("GTK ScratchPad")
		{
			Root = root;
			AppSettings = appSettings;
			RootController = new ScratchController(Root);
			InitComponent();
		}
		
		public Settings AppSettings { get; }
		public ScratchRoot Root { get; }
		public ScratchController RootController { get; }
		
		private void InitComponent()
		{
			Resize(600, 600);

			List<BookView> views = new List<BookView>();
			_notebook = new Notebook();

			foreach (var book in Root.Books)
			{
				ScratchBookController controller = RootController.GetControllerFor(book);
				BookView view = new BookView(book, controller, AppSettings);
				views.Add(view);
				Label viewLabel = new Label { Text = book.ToString() };
				viewLabel.SetPadding(10, 2);
				_notebook.AppendPage(view, viewLabel);
			}

			Add(_notebook);

			Destroyed += (o, e) =>
			{
				// TODO: call EnsureSaved on the root controller instead
				foreach (var view in views)
					view.EnsureSaved();
				Application.Quit();
			};

			KeyPressEvent += (sender, args) =>
			{
				if (!(_notebook.CurrentPageWidget is BookView currentView))
					return;
				// TODO: implement something like this for the root controller with appropriate view interface
				var state = args.Event.State & (Gdk.ModifierType.ShiftMask | Gdk.ModifierType.Mod1Mask | 
					Gdk.ModifierType.ControlMask);
				if (state == Gdk.ModifierType.None)
				{
					switch (args.Event.Key)
					{
						case Gdk.Key.F12:
							currentView.EnsureSaved();
							object found = SearchWindow.RunSearch(this, ToSearchable(currentView.Book), AppSettings);
							if (found != null)
								currentView.JumpToPage((int) found);
							break;
					}
				}
			};
		}

		class BookSearchable : ISearchable
		{
			private ScratchBook book;

			public BookSearchable(ScratchBook book)
			{
				this.book = book;
			}

			public IEnumerable<(string, object)> Search(string text)
			{
				foreach (var kv in book.SearchTitles(text))
				{
					yield return (kv.Key, kv.Value);
				}
			}
		}

		ISearchable ToSearchable(ScratchBook book)
		{
			return new BookSearchable(book);
		}
	}
}

