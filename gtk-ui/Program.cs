using System;
using Gtk;
using Barrkel.ScratchPad;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Barrkel.GtkScratchPad
{
	static class Program
	{
		public static int Main(string[] args)
		{
			try
			{
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

		private static void NormalizeLineEndings(DirectoryInfo root)
		{
			Console.WriteLine("Processing notes in {0}", root.FullName);
			foreach (string baseName in root.GetFiles("*.txt")
				.Union(root.GetFiles("*.log"))
				.Select(f => Path.ChangeExtension(f.FullName, null))
				.Distinct())
			{
				Console.WriteLine("Normalizing {0}", baseName);
				ScratchPage.NormalizeLineEndings(baseName);
			}
			foreach (DirectoryInfo child in root.GetDirectories())
				if (child.Name != "." && child.Name != "..")
					NormalizeLineEndings(child);
		}

		public static int AppMain(string[] argArray)
		{
			List<string> args = new List<string>(argArray);
			Options options = new Options(args);

			string[] stub = Array.Empty<string>();
			Application.Init("GtkScratchPad", ref stub);

			if (args.Count != 1)
			{
				Console.WriteLine("Expected argument: storage directory");
				return 1;
			}

			if (options.NormalizeFiles)
			{
				NormalizeLineEndings(new DirectoryInfo(args[0]));
			}

			ScratchScope rootScope = ScratchScope.CreateRoot();
			rootScope.Load(LegacyLibrary.Instance);
			rootScope.Load(ScratchLib.Instance);

			ScratchRoot root = new ScratchRoot(options, args[0], rootScope);
			ScratchLib.Instance.LoadConfig(root);

			MainWindow window = new MainWindow(root);
			window.ShowAll();

			Application.Run();
			return 0;
		}
	}
}

