using Gtk;
using Barrkel.ScratchPad;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barrkel.GtkScratchPad
{
	public class MainWindow : Window
	{
		Notebook _notebook;

		public MainWindow(ScratchRootController rootController) : base(WindowType.Toplevel)
		{
			RootController = rootController;
			rootController.ExitHandler += (sender, e) => Destroy();
			InitComponent();
		}

		public ScratchRootController RootController { get; }
		
		private void InitComponent()
		{
			Title = RootController.RootScope.GetOrDefault("app-title", "GTK ScratchPad");
			Resize(600, 600);

			List<BookView> views = new List<BookView>();
			_notebook = new Notebook();

			foreach (var book in RootController.Root.Books)
			{
				ScratchBookController controller = RootController.GetControllerFor(book);
				BookView view = new BookView(book, controller, this);
				views.Add(view);
				_notebook.AppendPage(view, CreateTabLabel(book.Name));
			}
			var logView = new LogView(RootController.RootScope, this);
			_notebook.AppendPage(logView, CreateTabLabel("log"));
			Log.Handler = logView.AppendLine;

			Add(_notebook);

			Destroyed += (o, e) =>
			{
				// TODO: call EnsureSaved on the root controller instead
				foreach (var view in views)
					view.EnsureSaved();
				Application.Quit();
			};
		}

		private static Label CreateTabLabel(string title)
		{
			Label viewLabel = new Label { Text = title };
			viewLabel.SetPadding(10, 2);
			return viewLabel;
		}
	}
}

