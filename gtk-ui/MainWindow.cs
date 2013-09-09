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
		
		private void FullUpdateView()
		{
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

			//KeyPressEvent += (sender, args) =>
			//{
			//    Console.WriteLine("Key = {0}, State = {1}", args.Event.Key, args.Event.State);
			//};
		}
	}
}

