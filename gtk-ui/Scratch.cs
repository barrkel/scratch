using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections;

namespace Barrkel.ScratchPad
{
	public static class Log
	{
		public static Action<string> Handler { get; set; } = Console.Error.WriteLine;

		public static void Out(string line)
		{
			Handler(line);
		}
	}

	public interface IScratchScope
	{
		IScratchScope CreateChild(string name);
	}

	public class NullScope : IScratchScope
	{
		public static readonly IScratchScope Instance = new NullScope();

		private NullScope()
		{
		}

		public IScratchScope CreateChild(string name)
		{
			return this;
		}
	}

	public class Options
	{
		public Options(List<string> args)
		{
			NormalizeFiles = ParseFlag(args, "normalize");
		}

		static bool MatchFlag(string arg, string name)
		{
			return arg == "--" + name;
		}

		static bool ParseFlag(List<string> args, string name)
		{
			return args.RemoveAll(arg => MatchFlag(arg, name)) > 0;
		}

		public bool NormalizeFiles { get; }
	}

	// The main root. Every directory in this directory is a tab on the main interface, like a separate
	// notebook.
	// Every file in this directory is in the main notebook.
	public class ScratchRoot
	{
		readonly List<ScratchBook> _books;

		public ScratchRoot(Options options, string rootDirectory, IScratchScope rootScope)
		{
			RootScope = rootScope;
			Options = options;
			_books = new List<ScratchBook>();
			Books = new ReadOnlyCollection<ScratchBook>(_books);
			RootDirectory = rootDirectory;
			_books.Add(new ScratchBook(this, rootDirectory));
			foreach (string dir in Directory.GetDirectories(rootDirectory))
				_books.Add(new ScratchBook(this, dir));
		}

		public Options Options { get; }

		public string RootDirectory { get; }

		public ReadOnlyCollection<ScratchBook> Books { get; }

		public IScratchScope RootScope { get; }

		public void SaveLatest()
		{
			foreach (var book in Books)
				book.SaveLatest();
		}
	}

	static class DictionaryExtension
	{
		public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> self, out TKey key, out TValue value)
		{
			key = self.Key;
			value = self.Value;
		}
	}

	// Simple text key to text value cache with timestamp-based invalidation.
	public class LineCache
	{
		string _storeFile;
		Dictionary<string, (DateTime, string)> _cache = new Dictionary<string, (DateTime, string)>();
		bool _dirty;

		public LineCache(string storeFile)
		{
			_storeFile = storeFile;
			Load();
		}

		private void Load()
		{
			_cache.Clear();
			if (!File.Exists(_storeFile))
				return;
			using (var r = new LineReader(_storeFile))
			{
				int count = int.Parse(r.ReadLine());
				for (int i = 0; i < count; ++i)
				{
					string name = StringUtil.Unescape(r.ReadLine());
					// This is parsing UTC but it doesn't return UTC!
					DateTime timestamp = DateTime.Parse(r.ReadLine()).ToUniversalTime();
					string line = StringUtil.Unescape(r.ReadLine());
					_cache[name] = (timestamp, line);
				}
			}
		}

		public void Save()
		{
			if (!_dirty)
				return;
			using (var w = new LineWriter(_storeFile, FileMode.Create))
			{
				w.WriteLine(_cache.Count.ToString());
				foreach (var (name, (timestamp, line)) in _cache)
				{
					w.WriteLine(StringUtil.Escape(name));
					w.WriteLine(timestamp.ToString("o"));
					w.WriteLine(StringUtil.Escape(line));
				}
			}
			_dirty = false;
		}

		public string Get(string name, DateTime timestamp, Func<string> fetch)
		{
			if (_cache.TryGetValue(name, out var entry))
			{
				var (cacheTs, line) = entry;
				TimeSpan age = timestamp - cacheTs;
				// 1 second leeway because of imprecision in file system timestamps etc.
				if (age < TimeSpan.FromSeconds(1))
					return line;
			}
			var update = fetch();
			Put(name, timestamp, update);
			return update;
		}

		public void Put(string name, DateTime timestamp, string line)
		{
			_cache[name] = (timestamp, line);
			_dirty = true;
		}

		public void EnumerateValues(Action<string> callback)
		{
			foreach (var (_, line) in _cache.Values)
				callback(line);
		}
	}

	public class ScratchBook
	{
		List<ScratchPage> _pages = new List<ScratchPage>();
		string _rootDirectory;

		public ScratchBook(ScratchRoot root, string rootDirectory)
		{
			Root = root;
			_rootDirectory = rootDirectory;
			Scope = root.RootScope.CreateChild(Name);
			Pages = new ReadOnlyCollection<ScratchPage>(_pages);
			var rootDir = new DirectoryInfo(rootDirectory);
			TitleCache = new LineCache(Path.Combine(_rootDirectory, "title_cache.text"));
			_pages.AddRange(rootDir.GetFiles("*.txt")
				.Union(rootDir.GetFiles("*.log"))
				.OrderBy(f => f.LastWriteTimeUtc)
				.Select(f => Path.ChangeExtension(f.FullName, null))
				.Distinct()
				.Select(name => new ScratchPage(TitleCache, name)));
		}

		public ScratchRoot Root { get; }

		public IScratchScope Scope { get; }

		internal LineCache TitleCache { get; }

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

		public ReadOnlyCollection<ScratchPage> Pages { get; }

		public int MoveToEnd(int pageIndex)
		{
			var page = _pages[pageIndex];
			_pages.RemoveAt(pageIndex);
			_pages.Add(page);
			return _pages.Count - 1;
		}
		
		public ScratchPage AddPage()
		{
			ScratchPage result = new ScratchPage(TitleCache, FindNewPageBaseName());
			_pages.Add(result);
			return result;
		}
		
		// This is called periodically, and on every modification.
		public void EnsureSaved()
		{
			TitleCache.Save();
		}

		// This is only called if content has been modified.
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
		
		public IEnumerable<(string, int)> SearchTitles(string text)
		{
			string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			
			for (int i = Pages.Count - 1; i >= 0; --i)
			{
				var page = Pages[i];
				
				if (IsSearchMatch(page.Title, parts))
					yield return (page.Title, i);
			}
		}

		public IEnumerable<(string, int)> SearchText(Regex re)
		{
			for (int i = Pages.Count - 1; i >= 0; --i)
			{
				var page = Pages[i];
				if (re.Match(page.Text).Success)
					yield return (page.Title, i);
			}
		}

		/// <summary>
		/// Returns (title, index) of each match
		/// </summary>
		public IEnumerable<(string, int)> SearchTitles(Regex re)
		{
			for (int i = Pages.Count - 1; i >= 0; --i)
			{
				var page = Pages[i];
				if (re.Match(page.Title).Success)
					yield return (page.Title, i);
			}
		}

		public string Name => Path.GetFileName(_rootDirectory);
		
		public override string ToString()
		{
			return Name;
		}
	}

	interface IReadOnlyPage
	{
		DateTime ChangeStamp { get; }
		string Title { get; }
		string Text { get; }
	}

	public class ScratchPage
	{
		LiteScratchPage _liteImpl;
		RealScratchPage _realImpl;
		// These timestamps are for detecting out of process modifications to txt and log files
		DateTime? _logStamp;
		DateTime? _textStamp;
		string _baseName;
		string _shortName;
		LineCache _titleCache;
		Dictionary<object, object> _viewState = new Dictionary<object, object>();

		public ScratchPage(LineCache titleCache, string baseName)
		{
			_baseName = baseName;
			_shortName = Path.GetFileNameWithoutExtension(_baseName);
			_titleCache = titleCache;
			TextFile = new FileInfo(Path.ChangeExtension(_baseName, ".txt"));
			LogFile = new FileInfo(Path.ChangeExtension(_baseName, ".log"));
		}

		public bool IsNew => _realImpl != null && _realImpl.IsEmpty;

		public static void NormalizeLineEndings(string baseName)
		{
			FileInfo textFile = new FileInfo(Path.ChangeExtension(baseName, ".txt"));
			FileInfo logFile = new FileInfo(Path.ChangeExtension(baseName, ".log"));

			string srcTextFinal = null;
			if (textFile.Exists)
				srcTextFinal = File.ReadAllText(textFile.FullName);
			var srcUpdates = new List<ScratchUpdate>();
			if (logFile.Exists)
			{
				using (var reader = new LineReader(logFile.FullName))
					while (true)
						try
						{
							srcUpdates.Add(ScratchUpdate.Load(reader.ReadLine));
						}
						catch (EndOfStreamException)
						{
							break;
						}
			}

			// replay source and transcribe to destination
			string srcText = "";
			string dstText = "";
			var dstUpdates = new List<ScratchUpdate>();

			bool diff = false;
			foreach (ScratchUpdate srcUp in srcUpdates)
			{
				srcText = srcUp.Apply(srcText, out _, out _);
				string newDest = srcText.Replace("\r\n", "\n");
				ScratchUpdate dstUp = ScratchUpdate.CalcUpdate(dstText, newDest, srcUp.Stamp);
				dstText = newDest;
				dstUpdates.Add(dstUp);
				diff |= srcText != dstText;
			}
			if (srcTextFinal != null && srcTextFinal != srcText)
			{
				string newDest = srcTextFinal.Replace("\r\n", "\n");
				ScratchUpdate dstUp = ScratchUpdate.CalcUpdate(dstText, newDest, textFile.LastWriteTimeUtc);
				dstText = newDest;
				dstUpdates.Add(dstUp);
			}

			// rewrite data only if necessary
			if (!diff)
				return;

			using (var writer = new LineWriter(logFile.FullName, FileMode.Create))
				foreach (var up in dstUpdates)
					up.Save(writer.WriteLine);
			File.WriteAllText(textFile.FullName, dstText);
		}

		public string Title => _titleCache.Get(_shortName, ChangeStamp, () => GetReadOnlyPage().Title);

		public bool IsEmpty => _realImpl == null || _realImpl.IsEmpty;

		internal T GetViewState<T>(object view, Func<T> ctor)
		{
			if (!_viewState.TryGetValue(view, out var result))
			{
				result = ctor();
				_viewState.Add(view, result);
			}
			return (T) result;
		}

		internal FileInfo TextFile
		{
			get; set;
		}
		
		internal FileInfo LogFile
		{
			get; set;
		}

		// Any operations we can perform without loading the full page (mutation or history) should come through here.
		IReadOnlyPage GetReadOnlyPage()
		{
			if (_realImpl != null || !TextFile.Exists)
				return GetRealPage();
			return LoadLiteImplIfNecessary();
		}

		bool UnderlyingChanged()
		{
			return (TextFile.Exists && TextFile.LastWriteTimeUtc != _textStamp)
				|| (LogFile.Exists && LogFile.LastWriteTimeUtc != _logStamp);
		}
		
		internal RealScratchPage GetRealPage()
		{
			if (_realImpl == null)
			{
				_realImpl = LoadRealImpl();
				_liteImpl = null;
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

		LiteScratchPage LoadLiteImplIfNecessary()
		{
			if (_liteImpl != null && !UnderlyingChanged())
				return _liteImpl;
			_liteImpl = new LiteScratchPage(this);
			_textStamp = TextFile.LastWriteTimeUtc;
			if (LogFile.Exists)
				_logStamp = LogFile.LastWriteTimeUtc;
			return _liteImpl;
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
			RealScratchPage realImpl = GetRealPage();
			using (var w = new LineWriter(LogFile.FullName, FileMode.Append))
				if (!realImpl.SaveLatest(w.WriteLine))
				{
					Console.WriteLine("Dodged a write!");
					return;
				}
			File.WriteAllText(TextFile.FullName, _realImpl.Text);
			_logStamp = LogFile.LastWriteTimeUtc;
			_textStamp = TextFile.LastWriteTimeUtc;
		}

		public ScratchIterator GetIterator() => GetRealPage().GetIterator();

		public DateTime ChangeStamp => GetReadOnlyPage().ChangeStamp;

		public string Text
		{
			get { return GetReadOnlyPage().Text; }
			set { GetRealPage().Text = value; }
		}
	}

	// Lightweight read-only current-version-only view of a page.
	// Should not be instantiated if we only have the log file, but it'll cope (it'll replay log).
	public class LiteScratchPage : IReadOnlyPage
	{
		ScratchPage _page;
		string _text;

		public LiteScratchPage(ScratchPage page)
		{
			_page = page;
		}

		public string Title
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

		public DateTime ChangeStamp
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

		public string Text
		{
			get
			{
				if (_text != null)
					return _text;
				if (_page.TextFile.Exists)
				{
					_text = File.ReadAllText(_page.TextFile.FullName);
					return _text;
				}
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

	// Full-fat page with navigable history, mutation and change tracking.
	public class RealScratchPage : IReadOnlyPage
	{
		string _text = string.Empty;
		List<ScratchUpdate> _updates = new List<ScratchUpdate>();
		int _lastSave;

		public RealScratchPage()
		{
		}

		public bool IsEmpty => _updates.Count == 0 && _text == "";

		public string Title
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

		public RealScratchPage(Func<string> source)
		{
			for (; ; )
			{
				try
				{
					ScratchUpdate up = ScratchUpdate.Load(source);
					_text = up.Apply(_text, out _, out _);
					_updates.Add(up);
				}
				catch (EndOfStreamException)
				{
					break;
				}
			}
			_lastSave = _updates.Count;
		}

		public bool SaveLatest(Action<string> sink)
		{
			for (int i = _lastSave; i < _updates.Count; ++i)
				_updates[i].Save(sink);
			_lastSave = _updates.Count;
			return _updates.Count > 0;
		}

		public bool SaveAll(Action<string> sink)
		{
			for (int i = 0; i < _updates.Count; ++i)
				_updates[i].Save(sink);
			_lastSave = _updates.Count;
			return _updates.Count > 0;
		}

		public ScratchIterator GetIterator()
		{
			return new ScratchIterator(_updates, Text);
		}

		public string Text
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

		public DateTime ChangeStamp
		{
			get
			{
				if (_updates.Count > 0)
					return _updates[_updates.Count - 1].Stamp;
				return DateTime.UtcNow;
			}
		}
	}

	class LineWriter : IDisposable
	{
		Func<FileStream> _fileCtor;
		TextWriter _writer;
		FileStream _file;
		
		public LineWriter(string path, FileMode mode)
		{
			// Lazily construct file so we only write non-empty files.
			_fileCtor = () => new FileStream(path, mode);
		}
		
		public void WriteLine(string line)
		{
			if (_writer != null || line != "")
				GetWriter().WriteLine(StringUtil.Escape(line));
		}

		private TextWriter GetWriter()
		{
			if (_writer == null)
			{
				_file = _fileCtor();
				_writer = new StreamWriter(_file);
			}
			return _writer;
		}

		public void Dispose()
		{
			if (_writer != null)
			{
				_writer.Flush();
				_writer.Dispose();
				_file.Dispose();
			}
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
	
	public class ScratchIterator
	{
		List<ScratchUpdate> _updates;
		// invariant of _position: is at the index in _updates of the next
		// update to be applied to move forward. If at _updates.Count, then
		// is at end.
		int _position;
		int _updatedFrom;
		int _updatedTo;
		
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
			Text = _updates[_position].Apply(Text, out _updatedFrom, out _updatedTo);
			++_position;
			return true;
		}
		
		public bool MovePrevious()
		{
			if (_position <= 0)
				return false;
			Text = _updates[_position - 1].Revert(Text, out _updatedFrom, out _updatedTo);
			--_position;
			return true;
		}
		
		public int UpdatedFrom => _updatedFrom;
		public int UpdatedTo => _updatedTo;
		public int Count => _updates.Count;
		
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
		
		public abstract string Apply(string oldText, out int from, out int to);
		public abstract string Revert(string newText, out int from, out int to);
		
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

		public static ScratchUpdate CalcUpdate(string oldText, string newText, DateTime stamp)
		{
			return new ScratchBatch(CalcUpdates(oldText, newText), stamp);
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
				: this(updates, DateTime.UtcNow)
			{
			}

			public ScratchBatch(IEnumerable<ScratchUpdate> updates, DateTime stamp)
			{
				_stamp = stamp;
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
			
			public override string Apply(string oldText, out int from, out int to)
			{
				from = int.MaxValue;
				to = -1;
				foreach (var up in _updates)
				{
					oldText = up.Apply(oldText, out int partFrom, out int partTo);
					from = Math.Min(from, partFrom);
					to = Math.Max(to, partTo);
				}
				return oldText;
			}
			
			public override string Revert(string newText, out int from, out int to)
			{
				from = int.MaxValue;
				to = -1;
				for (int i = _updates.Count - 1; i >= 0; --i)
				{
					var up = _updates[i];
					newText = up.Revert(newText, out int partFrom, out int partTo);
					from = Math.Min(from, partFrom);
					to = Math.Max(to, partTo);
				}
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
			
			public int Offset { get; }
			public string Value { get; }
			
			public override string Apply(string oldText, out int from, out int to)
			{
				from = Offset;
				to = Offset + Value.Length;
				return oldText.Insert(Offset, Value);
			}
			
			public override string Revert(string newText, out int from, out int to)
			{
				from = Offset;
				to = Offset;
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

			public int Offset { get; }
			public string Value { get; }
			
			public override string Apply(string oldText, out int from, out int to)
			{
				from = Offset;
				to = Offset;
				return oldText.Remove(Offset, Value.Length);
			}
			
			public override string Revert(string newText, out int from, out int to)
			{
				from = Offset;
				to = Offset + Value.Length;
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

