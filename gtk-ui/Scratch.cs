using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Barrkel.ScratchPad
{
	public interface IScratchBookView
	{
		// Get the book this view is for; the view only ever shows a single page from a book
		ScratchBook Book { get; }

		// Inserts text at CurrentPosition
		void InsertText(string text);
		// Gets 0-based position in text
		int CurrentPosition { get; set; }
		// Sets both position and selection, highlighting the text
		void SetSelection(int from, int to);
		// Ensures position is scrolled into view
		void SetScrollPos(int pos);

		// View should call InvokeAction with actionName every millis milliseconds
		void AddRepeatingTimer(int millis, string actionName);
	}

	// Controller for behaviour. UI should receive this and send keystrokes and events to it, along with view callbacks.
	// The view should be updated via the callbacks.
	// Much of it is stringly typed for a dynamically bound future.
	public class ScratchController
	{
		// Keyboard bindings to actions. Keyboard uses Emacs-style names; C-M-S-X is Ctrl-Alt-Shift-X.
		Dictionary<string, string> _bindings = new Dictionary<string, string>();

		Dictionary<string, Action<ScratchBookController,IScratchBookView, string[]>> _actions =
			new Dictionary<string, Action<ScratchBookController,IScratchBookView, string[]>>();

		Dictionary<ScratchBook, ScratchBookController> _controllerMap = new Dictionary<ScratchBook, ScratchBookController>();

		public ScratchRoot Root { get; }

		public ScratchBookController GetControllerFor(ScratchBook book)
		{
			if (_controllerMap.TryGetValue(book, out var result))
			{
				return result;
			}
			result = new ScratchBookController(this, book);
			_controllerMap.Add(book, result);
			return result;
		}

		public ScratchController(ScratchRoot root)
		{
			Root = root;

			foreach (var member in typeof(ScratchBookController).GetMembers())
			{
				if (member.MemberType != MemberTypes.Method)
					continue;
				MethodInfo method = (MethodInfo) member;
				foreach (ActionAttribute attr in Attribute.GetCustomAttributes(member, typeof(ActionAttribute)))
				{
					_actions.Add(attr.Name, (controller, view, args) =>
						method.Invoke(controller, new object[] { view, args }));
				}
			}

			// TODO: parse these from a config page
			_bindings.Add("F4", "insert-date");
			_bindings.Add("S-F4", "insert-datetime");
		}

		public bool TryGetBinding(string key, out string actionName)
		{
			return _bindings.TryGetValue(key, out actionName);
		}

		public bool TryGetAction(string actionName, out Action<ScratchBookController,IScratchBookView,string[]> action)
		{
			return _actions.TryGetValue(actionName, out action);
		}

	}

	static class EmptyArray<T>
	{
		public static readonly T[] Value = new T[0];
	}

	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
	public class ActionAttribute : Attribute
	{
		public ActionAttribute(string name)
		{
			Name = name;
		}

		public string Name { get; }
	}

	public class ScratchBookController
	{
		public ScratchBook Book { get; }
		public ScratchController RootController { get; }

		public ScratchBookController(ScratchController rootController, ScratchBook book)
		{
			Book = book;
			RootController = rootController;
		}

		public void ConnectView(IScratchBookView view)
		{
			view.AddRepeatingTimer(3000, "check-for-save");
		}

		[Action("check-for-save")]
		public void CheckForSave(IScratchBookView view, string[] _)
		{
			// ...
		}

		public bool InformKeyStroke(IScratchBookView view, string keyName, bool ctrl, bool alt, bool shift)
		{
			string ctrlPrefix = ctrl ? "C-" : "";
			string altPrefix = alt ? "M-" : "";
			// Convention: self-printing keys have a single character. Exceptions are Return and Space.
			// Within this convention, don't use S- if shift was pressed to access the key. M-A is M-S-a, but M-A is emacs style.
			string shiftPrefix = (keyName.Length > 1 && shift) ? "S-" : "";
			string key = string.Concat(ctrlPrefix, altPrefix, shiftPrefix, keyName);

			// TODO: consider page / book-specific binds and actions
			// This is modal behaviour, a direction we probably don't need to go in
			if (RootController.TryGetBinding(key, out var actionName))
			{
				if (RootController.TryGetAction(actionName, out var action))
				{
					action(this, view, EmptyArray<string>.Value);
					return true;
				}
			}

			return false;
		}

		public void InvokeAction(IScratchBookView view, string actionName, string[] args)
		{
			if (RootController.TryGetAction(actionName, out var action))
			{
				action(this, view, args);
			}
		}

		public void InformEvent(IScratchBookView view, string eventName, string[] args)
		{
			if (RootController.TryGetAction("on-" + eventName, out var action))
			{
				action(this, view, args);
			}
		}

		[Action("insert-date")]
		public void DoInsertDate(IScratchBookView view, string[] _)
		{
			view.InsertText(DateTime.Today.ToString("yyyy-MM-dd"));
		}

		[Action("insert-datetime")]
		public void DoInsertDateTime(IScratchBookView view, string[] _)
		{
			view.InsertText(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
		}

		[Action("on-text-changed")]
		public void OnTextChanged(IScratchBookView view, string[] args)
		{
			// text has changed
		}
	}

	// The main root. Every directory in this directory is a tab on the main interface, like a separate
	// notebook.
	// Every file in this directory is in the main notebook.
	public class ScratchRoot
	{
		readonly List<ScratchBook> _books;

		public ScratchRoot(string rootDirectory)
		{
			_books = new List<ScratchBook>();
			Books = new ReadOnlyCollection<ScratchBook>(_books);
			RootDirectory = rootDirectory;
			_books.Add(new ScratchBook(rootDirectory));
			foreach (string dir in Directory.GetDirectories(rootDirectory))
				_books.Add(new ScratchBook(dir));
		}
		
		public string RootDirectory { get; }

		public ReadOnlyCollection<ScratchBook> Books { get; }

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

	// Simple text:text cache with timestamp-based invalidation.
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
					DateTime timestamp = DateTime.Parse(r.ReadLine());
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
					w.WriteLine(timestamp.ToString());
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
	}

	public class ScratchBook
	{
		List<ScratchPage> _pages = new List<ScratchPage>();
		string _rootDirectory;
		LineCache _titleCache;

		public ScratchBook(string rootDirectory)
		{
			Pages = new ReadOnlyCollection<ScratchPage>(_pages);
			_rootDirectory = rootDirectory;
			UnixLineEndings = File.Exists(Path.Combine(rootDirectory, ".unix"));
			var root = new DirectoryInfo(rootDirectory);
			_titleCache = new LineCache(Path.Combine(_rootDirectory, "title_cache.text"));
			_pages.AddRange(root.GetFiles("*.txt")
				.Union(root.GetFiles("*.log"))
				.OrderBy(f => f.LastWriteTimeUtc)
				.Select(f => Path.ChangeExtension(f.FullName, null))
				.Distinct()
				.Select(name => new ScratchPage(_titleCache, name)));
		}

		public bool UnixLineEndings { get; }

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
			ScratchPage result = new ScratchPage(_titleCache, FindNewPageBaseName());
			_pages.Add(result);
			return result;
		}
		
		// This is called periodically, and on every modification.
		public void EnsureSaved()
		{
			_titleCache.Save();
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
		
		public IEnumerable<(string,int)> SearchTitles(string text)
		{
			string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			
			for (int i = 0; i < Pages.Count; ++i)
			{
				var page = Pages[i];
				
				if (IsSearchMatch(page.Title, parts))
					yield return (page.Title, i);
			}
		}

		public IEnumerable<(string, int)> SearchText(Regex re)
		{
			for (int i = 0; i < Pages.Count; ++i)
			{
				var page = Pages[i];
				if (re.Match(page.Text).Success)
					yield return (page.Title, i);
			}
		}

		public IEnumerable<(string,int)> SearchTitles(Regex re)
		{
			for (int i = 0; i < Pages.Count; ++i)
			{
				var page = Pages[i];
				if (re.Match(page.Title).Success)
					yield return (page.Title, i);
			}
		}
		
		public override string ToString()
		{
			return Path.GetFileName(_rootDirectory);
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
		
		public ScratchPage(LineCache titleCache, string baseName)
		{
			_baseName = baseName;
			_shortName = Path.GetFileNameWithoutExtension(_baseName);
			_titleCache = titleCache;
			TextFile = new FileInfo(Path.ChangeExtension(_baseName, ".txt"));
			LogFile = new FileInfo(Path.ChangeExtension(_baseName, ".log"));
		}

		public string Title => _titleCache.Get(_shortName, ChangeStamp, () => GetReadOnlyPage().Title);

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
				realImpl.SaveLatest(w.WriteLine);
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
	// Should not be instantiated if we only have the log file, but it'll cope.
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
		
		public int UpdatedFrom { get => _updatedFrom; }
		public int UpdatedTo { get => _updatedTo; }

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
			
			public int Offset { get; private set; }
			public string Value { get; private set; }
			
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

			public int Offset { get; private set; }
			public string Value { get; private set; }
			
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

