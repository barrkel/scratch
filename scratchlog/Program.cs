using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Barrkel.ScratchPad;
using System.IO;

namespace ScratchLog
{
	class Program
	{
		static int Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("usage: {0} <root-dir>", Path.GetFileNameWithoutExtension(
					Environment.GetCommandLineArgs()[0]));
				Console.WriteLine("Output date-stamped log of modifications in order with titles at time of modification");
				return 1;
			}
			List<string> argList = new List<string>(args);
			Options options = new Options(argList);
			ScratchRoot root = new ScratchRoot(options, argList[0], NullScope.Instance);
			
			var updates = new List<Update>();
			
			foreach (ScratchBook book in root.Books)
			{
				foreach (ScratchPage page in book.Pages)
				{
					ScratchIterator iter = page.GetIterator();
					iter.MoveToEnd();
					do {
						updates.Add(new Update { Title = new StringReader(iter.Text).ReadLine(), Stamp = iter.Stamp });
					} while (iter.MovePrevious());
				}
			}
			Console.WriteLine("Gathered {0} updates", updates.Count);
			
			Update previous = null;
			Update finish = null;
			int updateCount = 0;
			foreach (var update in updates.OrderByDescending(x => x.Stamp).Where(x => x.Title != null))
			{
				if (previous == null)
				{
					previous = update;
					continue;
				}
				
				if (previous.Title == update.Title && (previous.Stamp - update.Stamp).TotalHours < 1)
				{
					// within the hour => probably the same task
					if (finish == null)
					{
						// this is the start of a range
						finish = previous;
						updateCount = 1;
					}
					else
					{
						++updateCount;
					}
				}
				else
				{
					if (finish != null)
					{
						// we've come to the start of a range, and previous was the beginning
						TimeSpan duration = finish.Stamp - previous.Stamp;
						Console.WriteLine("{0} {1} ({2}) {3}", previous.Stamp, NiceDuration(duration), updateCount, previous.Title);
					}
					else
					{
						// different task that previous, and not part of range
						Console.WriteLine("{0} {1}", previous.Stamp, previous.Title);
					}

					updateCount = 0;
					finish = null;
				}
				
				previous = update;
			}
			if (finish != null)
			{
				// we've come to the start of a range, and previous was the beginning
				TimeSpan duration = finish.Stamp - previous.Stamp;
				Console.WriteLine("{0} {1} ({2}) {3}", previous.Stamp, NiceDuration(duration), updates, previous.Title);
			}
			else
			{
				// different task that previous, and not part of range
				Console.WriteLine("{0} {1}", previous.Stamp, previous.Title);
			}
			
			
			return 0;
		}
		
		static string NiceDuration(TimeSpan span)
		{
			if (span.TotalDays > 1)
				return string.Format("{0} days {1} hours", (int) span.TotalDays, span.Hours);
			if (span.TotalHours > 1)
				return string.Format("{0} hours {1} mins", (int) span.TotalHours, span.Minutes);
			return string.Format("{0} minutes", (int) span.TotalMinutes);
		}
		
		class Update
		{
			public DateTime Stamp { get; set; }
			public string Title { get; set; }
		}
	}
}
