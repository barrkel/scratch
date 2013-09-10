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
			InitComponent();
		}
		
		public Settings AppSettings { get; private set; }
		public ScratchRoot Root { get; private set; }
		
		private void InitComponent()
		{
			Resize(600, 600);

			List<BookView> views = new List<BookView>();
			_notebook = new Notebook();
			foreach (var book in Root.Books)
			{
				BookView view = new BookView(book, AppSettings);
				views.Add(view);
				Label viewLabel = new Label { Text = book.ToString() };
				viewLabel.SetPadding(10, 2);
				_notebook.AppendPage(view, viewLabel);
			}

			Add(_notebook);

			Destroyed += (o, e) =>
			{
				foreach (var view in views)
					view.EnsureSaved();
				Application.Quit();
			};

			KeyPressEvent += (sender, args) =>
			{
				BookView currentView = _notebook.CurrentPageWidget as BookView;
				if (currentView == null)
					return;

				if (args.Event.State == Gdk.ModifierType.None)
				{
					switch (args.Event.Key)
					{
						case Gdk.Key.F12:
							TitleSearchWindow.RunSearch(this, currentView.Book, AppSettings);
							break;
					}
				}
			};
		}
	}
}

