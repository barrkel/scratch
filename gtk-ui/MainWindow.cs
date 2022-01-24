using Gtk;
using Barrkel.ScratchPad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
				BookView view = new BookView(book, controller, AppSettings, this);
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
		}
	}
}

