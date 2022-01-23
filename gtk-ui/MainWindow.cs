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
							{
								currentView.EnsureSaved();
								if (SearchWindow.RunSearch(this, text => currentView.Book.SearchTitles(text).Take(100),
										AppSettings, out int found))
								{
									currentView.JumpToPage(found);
								}
								break;
							}

						case Gdk.Key.F11:
							{
								currentView.EnsureSaved();
								if (SearchWindow.RunSearch(this, text => TrySearch(currentView.Book, text), AppSettings, out int found))
									currentView.JumpToPage(found);
								break;
							}
					}
				}
			};
		}

		private IEnumerable<(string, int)> TrySearch(ScratchBook book, string text)
		{
			try
			{
				Regex regex = new Regex(text, RegexOptions.IgnoreCase);
				return book.SearchText(regex).Take(100);
			}
			catch (ArgumentException)
			{
				return Enumerable.Empty<(string, int)>();
			}
		}
	}
}

