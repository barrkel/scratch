using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

namespace Barrkel.ScratchPad
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static int Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			if (args.Length != 1)
			{
				MessageBox.Show("Expected: root directory argument");
				return 1;
			}
			if (!Directory.Exists(args[0]))
			{
				MessageBox.Show(string.Format("Directory '{0}' not found", args[0]));
				return 1;
			}
			
			ScratchRoot root = new ScratchRoot(args[0]);
			Application.Run(new MainForm(root));
			return 0;
		}
	}
}
