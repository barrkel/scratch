using System;
using Gtk;
using Barrkel.ScratchPad;
using System.Collections.Generic;
using System.IO;

namespace Barrkel.GtkScratchPad
{
	static class Program
	{

		public static int Main(string[] args)
		{
			try
			{
				Console.WriteLine("Hello");
				return AppMain(args);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Error: {0}", ex.Message);
				Console.Error.WriteLine("Stack trace: {0}", ex.StackTrace);
				Console.ReadLine();
				return 1;
			}
		}

		public static int AppMain(string[] args)
		{
			string settingsFile = Path.ChangeExtension(Environment.GetCommandLineArgs()[0], ".settings");
			Settings settings;
			if (File.Exists(settingsFile))
				using (TextReader reader = File.OpenText(settingsFile))
					settings = new Settings(reader);
			else
				settings = new Settings();
			
			Application.Init("GtkScratchPad", ref args);
			
			if (args.Length != 1)
			{
				Console.WriteLine("Expected argument: storage directory");
				return 1;
			}

			ScratchRoot root = new ScratchRoot(args[0]);
			MainWindow window = new MainWindow(root, settings);
			window.ShowAll();
			
			Application.Run();
			return 0;
		}
	}
}

