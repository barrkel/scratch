using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace Barrkel.ScratchPad
{
	// The main root. Every directory in this directory is a tab on the main interface, like a separate
	// notebook.
	// Every file in this directory is in the main notebook.
	public class ScratchRoot
	{
		List<ScratchBook> _books;
		ReadOnlyCollection<ScratchBook> _bookCollection;
		
		public ScratchRoot(string rootDirectory)
		{
			_books = new List<ScratchBook>();
			_bookCollection = new ReadOnlyCollection<ScratchBook>(_books);
			RootDirectory = rootDirectory;
			_books.Add(new ScratchBook(rootDirectory));
			foreach (string dir in Directory.GetDirectories(rootDirectory))
				_books.Add(new ScratchBook(dir));
		}
		
		public string RootDirectory
		{
			get; private set;
		}
		
		public ReadOnlyCollection<ScratchBook> Books
		{
			get { return _bookCollection; }
		}
		
		public void SaveLatest()
		{
			foreach (var book in Books)
				book.SaveLatest();
		}
	}
	
	public class ScratchBook
	{
		List<ScratchPage> _pages = new List<ScratchPage>();
		ReadOnlyCollection<ScratchPage> _pageCollection;
		string _rootDirectory;
		
		public ScratchBook(string rootDirectory)
		{
			_pageCollection = new ReadOnlyCollection<ScratchPage>(_pages);
			_rootDirectory = rootDirectory;
			var root = new DirectoryInfo(rootDirectory);
			_pages.AddRange(root.GetFiles("*.txt")
				.Union(root.GetFiles("*.log"))
				.OrderBy(f => f.LastWriteTimeUtc)
				.Select(f => Path.ChangeExtension(f.FullName, null))
				.Distinct()
				.Select(name => new ScratchPage(name)));
		}
		
		static bool DoesBaseNameExist(string baseName)
		{
			string logFile = Path.ChangeExtension(baseName, ".log");
			string textFile = Path.ChangeExtension(baseName, ".txt");
			return File.Exists(logFile) || File.Exists(textFile);
		}
		
		string FindNewPageBaseName()
		{
			string now = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm");
			
			string stem = string.Format("page-{0}", now);
			string result = Path.Combine(_rootDirectory, stem);
			if (!DoesBaseNameExist(result))
				return result;
			
			for (int i = 1; ; ++i)
			{
				result = Path.Combine(_rootDirectory,
					string.Format("{0}-{1:000}", stem, i));
				if (!DoesBaseNameExist(result))
					return result;
			}
		}
		
		public ReadOnlyCollection<ScratchPage> Pages
		{
			get { return _pageCollection; }
		}
		
		public int MoveToEnd(int pageIndex)
		{
			var page = _pages[pageIndex];
			_pages.RemoveAt(pageIndex);
			_pages.Add(page);
			return _pages.Count - 1;
		}
		
		public ScratchPage AddPage()
		{
			ScratchPage result = new ScratchPage(FindNewPageBaseName());
			_pages.Add(result);
			return result;
		}
		
		public void SaveLatest()
		{
			foreach (var page in Pages)
				page.SaveLatest();
		}
		
		static bool IsSearchMatch(string text, string[] parts)
		{
			foreach (string part in parts)
			{
				switch (part[0])
				{
					case '-':
					{
						string neg = part.Substring(1);
						if (neg.Length == 0)
							continue;
						if (text.IndexOf(neg, StringComparison.InvariantCultureIgnoreCase) >= 0)
							return false;
						
						break;
					}
					
					default:
						if (text.IndexOf(part, StringComparison.InvariantCultureIgnoreCase) < 0)
							return false;
						break;
				}
			}
			
			return true;
		}
		
		public IEnumerable<KeyValuePair<string,int>> SearchTitles(string text)
		{
			string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			
			for (int i = 0; i < Pages.Count; ++i)
			{
				var page = Pages[i];
				
				if (IsSearchMatch(page.Title, parts))
					yield return new KeyValuePair<string,int>(page.Title, i);
			}
		}
		
		public IEnumerable<KeyValuePair<string,int>> SearchTitles(Regex re)
		{
			for (int i = 0; i < Pages.Count; ++i)
			{
				var page = Pages[i];
				if (re.Match(page.Title).Success)
					yield return new KeyValuePair<string,int>(page.Title, i);
			}
		}
		
		public override string ToString()
		{
			return Path.GetFileName(_rootDirectory);
		}
	}
	
	public class ScratchPage : ScratchPageBase
	{
		ScratchPageBase _phantomImpl;
		RealScratchPage _realImpl;
		DateTime? _logStamp;
		DateTime? _textStamp;
		string _baseName;
		
		public ScratchPage(string baseName)
		{
			_baseName = baseName;
			TextFile = new FileInfo(Path.ChangeExtension(_baseName, ".txt"));
			LogFile = new FileInfo(Path.ChangeExtension(_baseName, ".log"));
		}
		
		public override string Title
		{
			get
			{
				if (_realImpl != null)
					return _realImpl.Title;
				if (_phantomImpl != null)
					return _phantomImpl.Title;
				if (TextFile.Exists)
					using (var r = new LineReader(TextFile.FullName))
						return r.ReadLine();
				// Only log file exists => we'll need to replay it to discover title.
				// No point throwing away that work.
				return GetRealImpl().Title;
			}
		}
		
		internal FileInfo TextFile
		{
			get; set;
		}
		
		internal FileInfo LogFile
		{
			get; set;
		}
		
		ScratchPageBase GetPhantomImpl()
		{
			if (_realImpl != null)
				return _realImpl;
			if (_phantomImpl == null)
				_phantomImpl = new PhantomScratchPage(this);
			return _phantomImpl;
		}
		
		bool UnderlyingChanged()
		{
			return (TextFile.Exists && TextFile.LastWriteTimeUtc != _textStamp)
				|| (LogFile.Exists && LogFile.LastWriteTimeUtc != _logStamp);
		}
		
		internal RealScratchPage GetRealImpl()
		{
			if (_realImpl == null)
			{
				_realImpl = LoadRealImpl();
				_phantomImpl = null;
			}
			else if (UnderlyingChanged())
			{
				RealScratchPage result = LoadRealImpl();
				if (result.Text != _realImpl.Text)
					result.Text = _realImpl.Text;
				_realImpl = result;
			}
			return _realImpl;
		}
		
		RealScratchPage LoadRealImpl()
		{
			// Load up a new real implementation from disk.
			RealScratchPage result = null;
			string text = null;
			
			// Try getting it from the log.
			try
			{
				if (LogFile.Exists)
				{
					_logStamp = LogFile.LastWriteTimeUtc;
					using (var reader = new LineReader(LogFile.FullName))
						result = new RealScratchPage(reader.ReadLine);
				}
			}
			catch
			{
				result = null;
			}
			
			if (result == null)
				result = new RealScratchPage();
			
			// Try getting it from the text file.
			if (TextFile.Exists)
			{
				text = File.ReadAllText(TextFile.FullName);
				_textStamp = TextFile.LastWriteTimeUtc;
			}
			
			// If there's a conflict, the text file wins, but keep log history; and rewrite it.
			if (text != null && result.Text != text)
			{
				result.Text = text;
				using (var w = new LineWriter(LogFile.FullName, FileMode.Create))
					result.SaveAll(w.WriteLine);
				_logStamp = LogFile.LastWriteTimeUtc;
			}
			
			return result;
		}
		
		public void SaveLatest()
		{
			if (_realImpl == null)
				return;
			RealScratchPage realImpl = GetRealImpl();
			using (var w = new LineWriter(LogFile.FullName, FileMode.Append))
				realImpl.SaveLatest(w.WriteLine);
			File.WriteAllText(TextFile.FullName, _realImpl.Text);
			_logStamp = LogFile.LastWriteTimeUtc;
			_textStamp = TextFile.LastWriteTimeUtc;
		}
		
		public override ScratchIterator GetIterator()
		{
			return GetRealImpl().GetIterator();
		}
		
		public override DateTime CreationStamp
		{
			get { return GetPhantomImpl().CreationStamp; }
		}
		
		public override DateTime ChangeStamp
		{
			get { return GetPhantomImpl().ChangeStamp; }
		}
		
		public override string Text
		{
			get { return GetPhantomImpl().Text; }
			set { GetRealImpl().Text = value; }
		}
	}
	
	class LineWriter : IDisposable
	{
		TextWriter _writer;
		FileStream _file;
		
		public LineWriter(string path, FileMode mode)
		{
			_file = new FileStream(path, mode);
			_writer = new StreamWriter(_file);
		}
		
		public void WriteLine(string line)
		{
			_writer.WriteLine(StringUtil.Escape(line));
		}
		
		public void Dispose()
		{
			_writer.Flush();
			_writer.Dispose();
			_file.Dispose();
		}
	}
	
	class StringUtil
	{
		public static string Escape(string text)
		{
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < text.Length; ++i)
			{
				char ch = text[i];
				switch (ch)
				{
					case '\r':
						result.Append(@"\r");
						break;
					
					case '\n':
						result.Append(@"\n");
						break;
					
					case '\\':
						result.Append(@"\\");
						break;
					
					default:
						result.Append(ch);
						break;
				}
			}
			return result.ToString();
		}
		
		public static string Unescape(string text)
		{
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < text.Length; )
			{
				char ch = text[i++];
				if (ch == '\\' && i < text.Length)
				{
					ch = text[i++];
					switch (ch)
					{
						case '\\':
							result.Append('\\');
							break;
							
						case 'r':
							result.Append('\r');
							break;
						
						case 'n':
							result.Append('\n');
							break;
						
						default:
							result.Append('\\').Append(ch);
							break;
					}
				}
				else
					result.Append(ch);
			}
			return result.ToString();
		}
	}
	
	class LineReader : IDisposable
	{
		TextReader _reader;
		
		public LineReader(string path)
		{
			_reader = File.OpenText(path);
		}
		
		public string ReadLine()
		{
			string line = _reader.ReadLine();
			if (line == null)
				throw new EndOfStreamException();
			return StringUtil.Unescape(line);
		}
		
		public void Dispose()
		{
			_reader.Dispose();
		}
	}
	
	public abstract class ScratchPageBase
	{
		public abstract ScratchIterator GetIterator();
		public abstract DateTime CreationStamp
		{
			get;
		}
		public abstract DateTime ChangeStamp
		{
			get;
		}
		public abstract string Text
		{
			get; set;
		}
		public virtual string Title
		{
			get
			{
				string text = Text;
				int newLine = text.IndexOfAny(new[] { '\r', '\n' });
				if (newLine < 0)
					return text;
				return text.Substring(0, newLine);
			}
		}
	}
	
	public class PhantomScratchPage : ScratchPageBase
	{
		ScratchPage _page;
		string _text;
		
		public PhantomScratchPage(ScratchPage page)
		{
			_page = page;
		}
		
		public override ScratchIterator GetIterator()
		{
			return _page.GetRealImpl().GetIterator();
		}
		
		public override DateTime CreationStamp
		{
			get
			{
				if (_page.TextFile.Exists)
					return _page.TextFile.CreationTimeUtc;
				if (_page.LogFile.Exists)
					return _page.LogFile.CreationTimeUtc;
				return DateTime.UtcNow;
			}
		}
		
		public override DateTime ChangeStamp
		{
			get
			{
				if (_page.TextFile.Exists)
					return _page.TextFile.LastWriteTimeUtc;
				if (_page.LogFile.Exists)
					return _page.LogFile.LastWriteTimeUtc;
				return DateTime.UtcNow;
			}
		}
		
		public override string Text
		{
			get
			{
				if (_text != null)
					return _text;
				if (_page.TextFile.Exists)
					return File.ReadAllText(_page.TextFile.FullName);
				if (_page.LogFile.Exists)
					using (var r = new LineReader(_page.LogFile.FullName))
					{
						RealScratchPage log = new RealScratchPage(r.ReadLine);
						_text = log.Text;
						return _text;
					}
				return "";
			}
			set { throw new NotImplementedException(); }
		}
	}
	
	public class RealScratchPage : ScratchPageBase
	{
		string _text = string.Empty;
		List<ScratchUpdate> _updates = new List<ScratchUpdate>();
		int _lastSave;
		
		public RealScratchPage()
		{
		}
		
		public RealScratchPage(Func<string> source)
		{
			for (;;)
			{
				try
				{
					ScratchUpdate up = ScratchUpdate.Load(source);
					_text = up.Apply(_text);
					_updates.Add(up);
				}
				catch (EndOfStreamException)
				{
					break;
				}
			}
			_lastSave = _updates.Count;
		}
		
		public void SaveLatest(Action<string> sink)
		{
			for (int i = _lastSave; i < _updates.Count; ++i)
				_updates[i].Save(sink);
			_lastSave = _updates.Count;
		}
		
		public void SaveAll(Action<string> sink)
		{
			for (int i = 0; i < _updates.Count; ++i)
				_updates[i].Save(sink);
			_lastSave = _updates.Count;
		}
		
		public override ScratchIterator GetIterator()
		{
			return new ScratchIterator(_updates, Text);
		}
		
		public override string Text
		{
			get { return _text; }
			set
			{
				if (_text == value)
					return;
				_updates.Add(ScratchUpdate.CalcUpdate(_text, value));
				_text = value;
			}
		}

		public override DateTime CreationStamp
		{
			get
			{
				if (_updates.Count > 0)
					return _updates[0].Stamp;
				return DateTime.UtcNow;
			}
		}
		
		public override DateTime ChangeStamp
		{
			get
			{
				if (_updates.Count > 0)
					return _updates[_updates.Count - 1].Stamp;
				return DateTime.UtcNow;
			}
		}
	}
	
	public class ScratchIterator
	{
		List<ScratchUpdate> _updates;
		// invariant of _position: is at the index in _updates of the next
		// update to be applied to move forward. If at _updates.Count, then
		// is at end.
		int _position;
		
		internal ScratchIterator(List<ScratchUpdate> updates)
		{
			Text = "";
			_updates = updates;
		}
		
		internal ScratchIterator(List<ScratchUpdate> updates, string endText)
		{
			Text = endText;
			_updates = updates;
			_position = updates.Count;
		}
		
		public bool Navigate(int offset)
		{
			while (offset > 0)
			{
				if (!MoveNext())
					return false;
				--offset;
			}
			while (offset < 0)
			{
				if (!MovePrevious())
					return false;
				++offset;
			}
			return true;
		}
		
		public void MoveToStart()
		{
			_position = 0;
			Text = "";
		}
		
		public void MoveToEnd()
		{
			Navigate(Count - Position);
		}
		
		public bool MoveNext()
		{
			if (_position >= _updates.Count)
				return false;
			Text = _updates[_position].Apply(Text);
			++_position;
			return true;
		}
		
		public bool MovePrevious()
		{
			if (_position <= 0)
				return false;
			Text = _updates[_position - 1].Revert(Text);
			--_position;
			return true;
		}
		
		public int Count
		{
			get { return _updates.Count; }
		}
		
		public int Position
		{
			get { return _position; }
			set { Navigate(value - _position); }
		}
		
		public DateTime Stamp
		{
			get
			{
				if (_position > 0)
					return _updates[_position - 1].Stamp;
				if (_updates.Count > 0)
					return _updates[0].Stamp;
				return DateTime.UtcNow;
			}
		}
		
		public string Text
		{
			get; private set;
		}
	}
	
	// Represents an edit to text: an insertion, a deletion, or a batch of insertions and deletions.
	abstract class ScratchUpdate
	{
		// Try to sync longer text before shorter to avoid spurious matches; also, don't go too short.
		static readonly int[] SyncLengths = new[] { 128, 64, 32 };
		
		protected ScratchUpdate()
		{
		}
		
		ScratchUpdate(Func<string> source)
		{
		}
		
		public abstract string Apply(string oldText);
		public abstract string Revert(string newText);
		
		public virtual void Save(Action<string> sink)
		{
		}
		
		public virtual DateTime Stamp
		{
			get { return DateTime.MinValue; }
		}
		
		public static ScratchUpdate Load(Func<string> source)
		{
			string kind = source();
			switch (kind)
			{
				case "batch":
					return new ScratchBatch(source);
				case "insert":
					return new ScratchInsertion(source);
				case "delete":
					return new ScratchDeletion(source);
				default:
					throw new FormatException(
						string.Format("Unknown update kind '{0}'", kind));
			}
		}
		
		public static ScratchUpdate CalcUpdate(string oldText, string newText)
		{
			return new ScratchBatch(CalcUpdates(oldText, newText));
		}
		
		// Simplistic text diff algorithm creating insertions and deletions.
		// Doesn't try very hard to be optimal.
		static IEnumerable<ScratchUpdate> CalcUpdates(string oldText, string newText)
		{
			// invariant: oldIndex and newIndex are pointing at starts of suffix under consideration.
			int oldIndex = 0;
			int newIndex = 0;
			// resIndex is the position in the input string to be modified by the next update.
			// As updates are created, this position necessarily moves forward.
			int resIndex = 0;
			
			for (;;)
			{
			loop_top:
				// Skip common sequence
				while (oldIndex < oldText.Length && newIndex < newText.Length
					&& oldText[oldIndex] == newText[newIndex])
				{
					++oldIndex;
					++newIndex;
					++resIndex;
				}
				
				// Check for termination / truncation
				if (oldIndex == oldText.Length)
				{
					if (newIndex < newText.Length)
						yield return new ScratchInsertion(resIndex, newText.Substring(newIndex));
					break;
				}
				if (newIndex == newText.Length)
				{
					yield return new ScratchDeletion(resIndex, oldText.Substring(oldIndex));
					break;
				}
				
				// Finally, resync to next common sequence.
				// Three cases:
				// 1) Insertion; start of oldText will be found later in newText
				// 2) Deletion; start of newText will be found later in oldText
				// 3) Change; neither insertion or deletion, so record change and skip forwards
				
				// the start of the change in the result text
				int changeIndex = resIndex;
				// the prefix of the change in the new text
				int changeNew = newIndex;
				int changeOld = oldIndex;
				int changeLen = 0;
				
				for (;;)
				{
					for (int i = 0; i < SyncLengths.Length; ++i)
					{
						int syncLen = SyncLengths[i];
						
						// try to sync old with new - the insertion case
						if (syncLen <= (oldText.Length - oldIndex))
						{
							string chunk = oldText.Substring(oldIndex, syncLen);
							int found = newText.IndexOf(chunk, newIndex);
							if (found >= 0)
							{
								// We found prefix chunk of oldText inside newText.
								// That means the text at the start of newText is an insertion.
								if (changeLen > 0)
								{
									// handle any change skipping we had to do
									yield return new ScratchDeletion(changeIndex,
										oldText.Substring(changeOld, changeLen));
									yield return new ScratchInsertion(changeIndex, 
										newText.Substring(changeNew, changeLen));
								}
								int insertLen = found - newIndex;
								yield return new ScratchInsertion(resIndex, 
									newText.Substring(newIndex, insertLen));
								resIndex += insertLen;
								newIndex += insertLen;
								// Now newIndex will be pointing at the prefix that oldIndex
								// already points to, ready for another go around the loop.
								goto loop_top;
							}
						}
						
						// sync new prefix with old - deletion
						if (syncLen <= (newText.Length - newIndex))
						{
							string chunk = newText.Substring(newIndex, syncLen);
							int found = oldText.IndexOf(chunk, oldIndex);
							if (found >= 0)
							{
								// Prefix chunk of newText inside oldText => deletion.
								if (changeLen > 0)
								{
									yield return new ScratchDeletion(changeIndex,
										oldText.Substring(changeOld, changeLen));
									yield return new ScratchInsertion(changeIndex, 
										newText.Substring(changeNew, changeLen));
								}
								int deleteLen = found - oldIndex;
								yield return new ScratchDeletion(resIndex,
									oldText.Substring(oldIndex, deleteLen));
								oldIndex += deleteLen;
								goto loop_top;
							}
						}
					}
					
					// If we got here, then multiple sync prefixes failed. Take the longest
					// prefix sync and skip it as a change, then try again.
					int skipLen = SyncLengths[0];
					if (newIndex + skipLen > newText.Length)
						skipLen = newText.Length - newIndex;
					if (oldIndex + skipLen > oldText.Length)
						skipLen = oldText.Length - oldIndex;
					changeLen += skipLen;
					oldIndex += skipLen;
					newIndex += skipLen;
					resIndex += skipLen;
					if (skipLen != SyncLengths[0])
					{
						// End of old or new has been found; don't try to resync.
						yield return new ScratchDeletion(changeIndex,
							oldText.Substring(changeOld, changeLen));
						yield return new ScratchInsertion(changeIndex, 
							newText.Substring(changeNew, changeLen));
						break;
					}
				}
			}
		}
		
		class ScratchBatch : ScratchUpdate
		{
			List<ScratchUpdate> _updates = new List<ScratchUpdate>();
			DateTime _stamp;
			
			public ScratchBatch(IEnumerable<ScratchUpdate> updates)
			{
				_stamp = DateTime.UtcNow;
				_updates.AddRange(updates);
			}
			
			public ScratchBatch(Func<string> source)
				: base(source)
			{
				_stamp = DateTime.Parse(source());
				int count = int.Parse(source());
				for (int i = 0; i < count; ++i)
					_updates.Add(ScratchUpdate.Load(source));
			}
			
			public override DateTime Stamp
			{
				get { return _stamp; }
			}
			
			public override string Apply(string oldText)
			{
				foreach (var up in _updates)
					oldText = up.Apply(oldText);
				return oldText;
			}
			
			public override string Revert(string newText)
			{
				for (int i = _updates.Count - 1; i >= 0; --i)
					newText = _updates[i].Revert(newText);
				return newText;
			}
			
			public override void Save(Action<string> sink)
			{
				sink("batch");
				base.Save(sink);
				sink(Stamp.ToString("o"));
				sink(_updates.Count.ToString());
				foreach (var up in _updates)
					up.Save(sink);
			}
		}
		
		class ScratchInsertion : ScratchUpdate
		{
			public ScratchInsertion(int offset, string value)
			{
				Offset = offset;
				Value = value;
			}
			
			public ScratchInsertion(Func<string> source)
				: base(source)
			{
				Offset = int.Parse(source());
				Value = source();
			}
			
			public int Offset { get; private set; }
			public string Value { get; private set; }
			
			public override string Apply(string oldText)
			{
				return oldText.Insert(Offset, Value);
			}
			
			public override string Revert(string newText)
			{
				return newText.Remove(Offset, Value.Length);
			}
			
			public override void Save(Action<string> sink)
			{
				sink("insert");
				base.Save(sink);
				sink(Offset.ToString());
				sink(Value);
			}
		}
		
		class ScratchDeletion : ScratchUpdate
		{
			public ScratchDeletion(int offset, string value)
			{
				Offset = offset;
				Value = value;
			}

			public ScratchDeletion(Func<string> source)
				: base(source)
			{
				Offset = int.Parse(source());
				Value = source();
			}

			public int Offset { get; private set; }
			public string Value { get; private set; }
			
			public override string Apply(string oldText)
			{
				return oldText.Remove(Offset, Value.Length);
			}
			
			public override string Revert(string newText)
			{
				return newText.Insert(Offset, Value);
			}
			
			public override void Save(Action<string> sink)
			{
				sink("delete");
				base.Save(sink);
				sink(Offset.ToString());
				sink(Value);
			}
		}
	}
}
