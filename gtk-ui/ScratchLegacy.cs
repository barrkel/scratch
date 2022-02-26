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


	public class LegacyLibrary : ScratchLibrary
	{
		public static readonly LegacyLibrary Instance = new LegacyLibrary();

		static readonly char[] SmartChars = { '{', '[', '(' };
		static readonly char[] SmartInversion = { '}', ']', ')' };

		private LegacyLibrary() : base("legacy")
		{
			// TODO: parse these from a config page
			// the convention for keys is that they do a two-step
			// Look up key name and get an action name.
			// Look up action name and invoke that.
			// This makes binding multiple keys to the same action easier,
			// and potential debugging of key bindings easier.
			Bind("F4", new ScratchValue("insert-date"));
			Bind("S-F4", new ScratchValue("insert-datetime"));
			Bind("Return", new ScratchValue("autoindent-return"));
			Bind("Tab", new ScratchValue("indent-block"));
			Bind("S-Tab", new ScratchValue("unindent-block"));
			Bind("C-v", new ScratchValue("smart-paste"));
			Bind("M-/", new ScratchValue("complete"));
			Bind("C-a", new ScratchValue("goto-sol"));
			Bind("C-e", new ScratchValue("goto-eol"));
			Bind("M-o", new ScratchValue("occur"));
			Bind("F12", new ScratchValue("navigate-title"));
			Bind("F11", new ScratchValue("navigate-contents"));
			Bind("C-t", new ScratchValue("navigate-todo"));
			Bind("C-n", new ScratchValue("add-new-page"));
		}

		[Action("insert-date")]
		public void DoInsertDate(IScratchBookView view, IList<ScratchValue> args)
		{
			view.InsertText(DateTime.Today.ToString("yyyy-MM-dd"));
		}

		[Action("insert-datetime")]
		public void DoInsertDateTime(IScratchBookView view, IList<ScratchValue> args)
		{
			view.InsertText(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
		}

		[Action("autoindent-return")]
		public void DoAutoindentReturn(IScratchBookView view, IList<ScratchValue> args)
		{
			string text = view.CurrentText;
			int pos = view.CurrentPosition;
			string indent = GetCurrentIndent(text, pos);

			switch (IsSmartDelimiter(text, pos - 1, out char closer))
			{
				case SmartDelimiter.No:
					view.InsertText(string.Format("\n{0}", indent));
					break;

				case SmartDelimiter.Yes:
					// smart { etc.
					view.InsertText(string.Format("\n{0}  \n{0}{1}", indent, closer));
					view.CurrentPosition -= (1 + indent.Length + 1);
					view.Selection = (view.CurrentPosition, view.CurrentPosition);
					break;

				case SmartDelimiter.IndentOnly:
					view.InsertText(string.Format("\n{0}  ", indent));
					break;
			}
			view.ScrollIntoView(view.CurrentPosition);
		}

		[Action("smart-paste")]
		public void DoSmartPaste(IScratchBookView view, IList<ScratchValue> args)
		{
			view.SelectedText = "";
			string textToPaste = view.Clipboard;
			if (string.IsNullOrEmpty(textToPaste))
				return;
			string indent = GetCurrentIndent(view.CurrentText, view.CurrentPosition);
			if (indent.Length > 0)
				// Remove existing indent if pasted to an indent
				view.InsertText(AddIndent(indent, ResetIndent(textToPaste), IndentOptions.SkipFirst));
			else
				// Preserve existing indent if from col 0
				view.InsertText(AddIndent(indent, textToPaste, IndentOptions.SkipFirst));
			view.ScrollIntoView(view.CurrentPosition);
		}

		private int GetIndent(string text)
		{
			int result = 0;
			foreach (char ch in text)
			{
				if (!char.IsWhiteSpace(ch))
					return result;
				if (ch == '\t')
					result += (8 - result % 8);
				else
					++result;
			}
			return result;
		}

		private const int DefaultContextLength = 40;

		// Given a line and a match, add prefix and postfix text as necessary, and mark up match with [].
		// This logic is sufficiently UI-specific that it should be factored out somehow.
		static string Contextualize(string line, SimpleMatch match, int contextLength = DefaultContextLength)
		{
			StringBuilder result = new StringBuilder();
			// ... preamble [match] postamble ...
			if (match.Start > contextLength)
			{
				result.Append("...");
				result.Append(line.Substring(match.Start - contextLength + 3, contextLength - 3));
			}
			else
			{
				result.Append(line.Substring(0, match.Start));
			}
			result.Append('[').Append(match.Value).Append(']');
			if (match.End + contextLength < line.Length)
			{
				result.Append(line.Substring(match.End, contextLength - 3));
				result.Append("...");
			}
			else
			{
				result.Append(line.Substring(match.End));
			}
			return result.ToString();
		}

		struct SimpleMatch
		{
			public SimpleMatch(string text, Match match)
			{
				Text = text;
				if (!match.Success)
				{
					Start = 0;
					End = -1;
				}
				else
				{
					Start = match.Index;
					End = match.Index + match.Value.Length;
				}
			}

			private SimpleMatch(string text, int start, int end)
			{
				Start = start;
				End = end;
				Text = text;
			}

			public SimpleMatch Extend(SimpleMatch other)
			{
				if (!object.ReferenceEquals(Text, other.Text))
					throw new ArgumentException("Extend may only be called with match over same text");
				return new SimpleMatch(Text, Math.Min(Start, other.Start), Math.Max(End, other.End));
			}

			public int Start { get; }
			public int End { get; }
			public int Length => End - Start;
			private string Text { get; }
			public string Value => Text.Substring(Start, Length);
			// We're not interested in 0-length matches
			public bool Success => Length > 0;
		}

		private static List<SimpleMatch> MergeMatches(List<SimpleMatch> matches)
		{
			matches.Sort((a, b) => a.Start.CompareTo(b.Start));
			List<SimpleMatch> result = new List<SimpleMatch>();
			foreach (SimpleMatch m in matches)
			{
				if (result.Count == 0 || result[result.Count - 1].End < m.Start)
					result.Add(m);
				else
					result[result.Count - 1] = result[result.Count - 1].Extend(m);
			}
			return result;
		}

		static List<SimpleMatch> MatchRegexList(List<Regex> regexes, string text)
		{
			var result = new List<SimpleMatch>();
			foreach (Regex regex in regexes)
			{
				var matches = regex.Matches(text);
				if (matches.Count == 0)
					return new List<SimpleMatch>();
				result.AddRange(matches.Cast<Match>().Select(x => new SimpleMatch(text, x)));
			}
			return MergeMatches(result);
		}

		// Given a list of (lineOffset, line) and a pattern, return UI-suitable strings with associated (offset, length) pairs.
		static IEnumerable<(string, (int, int))> FindMatchingLocations(List<(int, string)> lines, List<Regex> regexes)
		{
			int count = 0;
			int linum = 0;
			// We cap at 1000 just in case caller doesn't limit us
			while (count < 1000 && linum < lines.Count)
			{
				var (lineOfs, line) = lines[linum];
				++linum;

				// Default case: empty pattern. Special case this one, we don't need to split every character.
				if (regexes.Count == 0 || regexes[0].IsMatch(""))
				{
					++count;
					yield return (line, (lineOfs, 0));
					continue;
				}

				var matches = MatchRegexList(regexes, line);
				if (matches.Count == 0)
				{
					continue;
				}
				count += matches.Count;
				// if multiple matches in a line, break them out
				// if a single match, keep as is
				string prefix = "";
				if (matches.Count > 1)
				{
					prefix = "   ";
					yield return (line, (lineOfs + matches[0].Start, matches[0].Length));
				}
				foreach (SimpleMatch match in matches)
				{
					yield return (prefix + Contextualize(line, match), (lineOfs + match.Start, match.Length));
				}
			}
		}

		// Returns (lineOffset, line) without trailing line separators.
		// lineOffset is the character offset of the start of the line in text.
		static IEnumerable<(int, string)> GetNonEmptyLines(string text)
		{
			int pos = 0;
			while (pos < text.Length)
			{
				char ch = text[pos];
				int start = pos;
				++pos;
				if (ch == '\n' || ch == '\r' || pos == text.Length)
					continue;

				while (pos < text.Length)
				{
					ch = text[pos];
					++pos;
					if (ch == '\n' || ch == '\r')
						break;
				}
				yield return (start, text.Substring(start, pos - start - 1));
			}
		}

		private bool AnyCaps(string value)
		{
			foreach (char ch in value)
				if (char.IsUpper(ch))
					return true;
			return false;
		}

		public List<Regex> ParseRegexList(string pattern, RegexOptions options)
		{
			if (!AnyCaps(pattern))
				options |= RegexOptions.IgnoreCase;

			Regex parsePart(string part)
			{
				if (part.StartsWith("*"))
					part = "\\" + part;
				try
				{
					return new Regex(part, options);
				}
				catch (ArgumentException)
				{
					return new Regex(Regex.Escape(part), options);
				}
			}

			return pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(x => parsePart(x))
				.ToList();
		}

		[Action("occur")]
		public void Occur(IScratchBookView view, IList<ScratchValue> args)
		{
			var lines = GetNonEmptyLines(view.CurrentText).ToList();
			if (view.RunSearch(pattern => FindMatchingLocations(lines, ParseRegexList(pattern, RegexOptions.Singleline)),
				out var pair))
			{
				var (pos, len) = pair;
				view.ScrollIntoView(pos);
				view.Selection = (pos, pos + len);
			}
		}

		[Action("navigate-title")]
		public void NavigateTitle(IScratchBookView view, IList<ScratchValue> args)
		{
			view.EnsureSaved();
			if (view.RunSearch(text => view.Book.SearchTitles(text).Take(100), out int found))
				view.JumpToPage(found);
		}

		[Action("navigate-contents")]
		public void NavigateContents(IScratchBookView view, IList<ScratchValue> args)
		{
			view.EnsureSaved();
			if (view.RunSearch(text => TrySearch(view.Book, text).Take(50), out var triple))
			{
				var (page, pos, len) = triple;
				view.JumpToPage(page);
				// these should probably be part of JumpToPage, to avoid the default action
				view.ScrollIntoView(pos);
				view.Selection = (pos, pos + len);
			}
		}

		[Action("navigate-todo")]
		public void NavigateTodo(IScratchBookView view, IList<ScratchValue> args)
		{
			NavigateSigil(view, "=>");
		}

		[Action("add-new-page")]
		public void AddNewPage(IScratchBookView view, IList<ScratchValue> args)
		{
			view.AddNewPage();
		}

		[Action("on-text-changed")]
		public void OnTextChanged(IScratchBookView view, string[] args)
		{
			// text has changed
		}

		[Action("indent-block")]
		public void DoIndentBlock(IScratchBookView view, IList<ScratchValue> args)
		{
			string text = view.SelectedText;
			if (string.IsNullOrEmpty(text))
			{
				view.InsertText("  ");
				return;
			}
			view.SelectedText = AddIndent("  ", text, IndentOptions.SkipTrailingEmpty);
		}

		[Action("unindent-block")]
		public void DoUnindentBlock(IScratchBookView view, IList<ScratchValue> args)
		{
			// remove 2 spaces or a tab or one space from every line
			string text = view.SelectedText;
			if (string.IsNullOrEmpty(text))
			{
				// We insert a literal tab, but we could consider unindenting line.
				view.InsertText("\t");
				return;
			}
			string[] lines = text.Split('\r', '\n');
			for (int i = 0; i < lines.Length; ++i)
			{
				string line = lines[i];
				if (line.Length == 0)
					continue;
				if (line.StartsWith("  "))
					lines[i] = line.Substring(2);
				else if (line.StartsWith("\t"))
					lines[i] = line.Substring(1);
				else if (line.StartsWith(" "))
					lines[i] = line.Substring(1);
			}
			view.SelectedText = string.Join("\n", lines);
		}

		[Action("exit-page")]
		public void ExitPage(IScratchBookView view, IList<ScratchValue> args)
		{
			ScratchPage page = GetPage(view.Book, view.CurrentPageIndex);
			if (page == null)
				return;
			PageViewState state = page.GetViewState(view);
			state.CurrentSelection = view.Selection;
			state.CurrentScrollPos = view.ScrollPos;
		}

		[Action("enter-page")]
		public void EnterPage(IScratchBookView view, IList<ScratchValue> args)
		{
			ScratchPage page = GetPage(view.Book, view.CurrentPageIndex);
			if (page == null)
				return;
			PageViewState state = page.GetViewState(view);
			if (state.CurrentSelection.HasValue)
				view.Selection = state.CurrentSelection.Value;
			if (state.CurrentScrollPos.HasValue)
				view.ScrollPos = state.CurrentScrollPos.Value;
		}

		[Flags]
		enum SearchOptions
		{
			None = 0,
			TitleLinkToFirstResult = 1
		}

		// returns (UI line, (page, pos, len))
		private IEnumerable<(string, (int, int, int))> TrySearch(ScratchBook book, string pattern,
			SearchOptions options = SearchOptions.None)
		{
			List<Regex> re, fullRe;
			try
			{
				re = ParseRegexList(pattern, RegexOptions.Singleline);
				fullRe = ParseRegexList(pattern, RegexOptions.Multiline);
			}
			catch (ArgumentException)
			{
				yield break;
			}

			// Keep most recent pagesfirst
			for (int i = book.Pages.Count - 1; i >= 0; --i)
			{
				var page = book.Pages[i];
				if (fullRe.Count > 0 && !fullRe[0].Match(page.Text).Success)
					continue;

				if (pattern.Length == 0)
				{
					yield return (page.Title, (i, 0, 0));
					continue;
				}

				var lines = GetNonEmptyLines(page.Text).ToList();
				bool isFirst = true;
				foreach (var match in FindMatchingLocations(lines, re))
				{
					var (uiLine, (pos, len)) = match;
					if (isFirst)
					{
						if (options.HasFlag(SearchOptions.TitleLinkToFirstResult))
						{
							yield return (page.Title, (i, pos, len));
						}
						else
						{
							yield return (page.Title, (i, 0, 0));
						}
						isFirst = false;
					}
					yield return ("    " + uiLine, (i, pos, len));
				}
			}
		}

		[Action("goto-sol")]
		public void GotoSol(IScratchBookView view, IList<ScratchValue> args)
		{
			// NOTE: does not extend selection
			string text = view.CurrentText;
			int pos = view.CurrentPosition;
			int sol = GetLineStart(text, pos);
			view.Selection = (sol, sol);
		}

		[Action("goto-eol")]
		public void GotoEol(IScratchBookView view, IList<ScratchValue> args)
		{
			string text = view.CurrentText;
			int pos = view.CurrentPosition;
			int eol = GetLineEnd(text, pos);
			view.Selection = (eol, eol);
		}

		// starting at position-1, keep going backwards until test fails
		private string GetStringBackwards(string text, int position, Predicate<char> test)
		{
			if (position == 0)
				return "";
			if (position > text.Length)
				position = text.Length;
			int start = position - 1;
			while (start >= 0 && start < text.Length && test(text[start]))
				--start;
			if (position - start == 0)
				return "";
			return text.Substring(start + 1, position - start - 1);
		}

		private void GetTitleCompletions(ScratchBook book, Predicate<char> test, Action<string> add)
		{
			book.TitleCache.EnumerateValues(value => GetTextCompletions(value, test, add));
		}

		private void GetTextCompletions(string text, Predicate<char> test, Action<string> add)
		{
			int start = -1;
			for (int i = 0; i < text.Length; ++i)
			{
				if (test(text[i]))
				{
					if (start < 0)
						start = i;
				}
				else if (start >= 0)
				{
					add(text.Substring(start, i - start));
					start = -1;
				}
			}
			if (start > 0)
				add(text.Substring(start, text.Length - start));
		}

		// TODO: consider getting completions in a different order; e.g. working backwards from a position
		private List<string> GetCompletions(ScratchBook book, string text, Predicate<char> test)
		{
			var unique = new HashSet<string>();
			var result = new List<string>();
			void add(string candidate)
			{
				if (!unique.Contains(candidate))
				{
					unique.Add(candidate);
					result.Add(candidate);
				}
			}

			GetTextCompletions(text, test, add);
			GetTitleCompletions(book, test, add);

			result.ForEach(Console.WriteLine);
			return result;
		}

		[Action("check-for-save")]
		internal ScratchValue CheckForSave(IScratchBookView view, IList<ScratchValue> args)
		{
			// ...
			return ScratchValue.Null;
		}

		[Action("complete")]
		public void CompleteAtPoint(IScratchBookView view, IList<ScratchValue> args)
		{
			// Emacs-style complete-at-point
			// foo| -> find symbols starting with foo and complete first found (e.g. 'bar')
			// foo[bar]| -> after completing [bar], find symbols starting with foo and complete first after 'bar'
			// If cursor isn't exactly at the end of a completion, we don't resume; we try from scratch.
			// Completion symbols come from all words ([A-Za-z0-9_-]+) in the document.
			var page = GetPage(view.Book, view.CurrentPageIndex);
			string text = view.CurrentText;
			var state = page.GetViewState(view);
			var (currentStart, currentEnd) = state.CurrentCompletion.GetValueOrDefault();
			var currentPos = view.CurrentPosition;
			string prefix, suffix;
			if (currentStart < currentEnd && currentPos == currentEnd)
			{
				prefix = GetStringBackwards(text, currentStart, char.IsLetterOrDigit);
				suffix = text.Substring(currentStart, currentEnd - currentStart);
			}
			else
			{
				prefix = GetStringBackwards(text, currentPos, char.IsLetterOrDigit);
				suffix = "";
				currentStart = currentPos;
			}
			List<string> completions = GetCompletions(view.Book, text, char.IsLetterOrDigit);

			int currentIndex = completions.IndexOf(prefix + suffix);
			if (currentIndex == -1)
				return;

			// find the next completion
			string nextSuffix = "";
			for (int i = (currentIndex + 1) % completions.Count; i != currentIndex; i = (i + 1) % completions.Count)
				if (completions[i].StartsWith(prefix))
				{
					nextSuffix = completions[i].Substring(prefix.Length);
					break;
				}
			if (suffix.Length > 0)
				view.DeleteTextBackwards(suffix.Length);
			view.InsertText(nextSuffix);
			state.CurrentCompletion = (currentStart, currentStart + nextSuffix.Length);
		}

		private ScratchPage GetPage(ScratchBook book, int index)
		{
			if (index < 0 || index >= book.Pages.Count)
				return null;
			return book.Pages[index];
		}

		private void NavigateSigil(IScratchBookView view, string sigil)
		{
			view.EnsureSaved();
			if (view.RunSearch(text => TrySearch(view.Book, $"^\\s*{Regex.Escape(sigil)}.*{text}",
				SearchOptions.TitleLinkToFirstResult).Take(50), out var triple))
			{
				var (page, pos, len) = triple;
				view.JumpToPage(page);
				// these should probably be part of JumpToPage, to avoid the default action
				view.ScrollIntoView(pos);
				view.Selection = (pos, pos + len);
			}
		}

		private string ResetIndent(string text)
		{
			string[] lines = text.Split('\r', '\n');
			int minIndent = int.MaxValue;
			// TODO: make this tab-aware
			foreach (string line in lines)
			{
				int indent = GetWhitespace(line, 0, line.Length).Length;
				// don't count empty lines, or lines with only whitespace
				if (indent == line.Length)
					continue;
				minIndent = Math.Min(minIndent, indent);
			}
			if (minIndent == 0)
				return text;
			for (int i = 0; i < lines.Length; ++i)
				if (minIndent >= lines[i].Length)
					lines[i] = "";
				else
					lines[i] = lines[i].Substring(minIndent);
			return string.Join("\n", lines);
		}

		enum IndentOptions
		{
			None,
			SkipFirst,
			SkipTrailingEmpty
		}

		private string AddIndent(string indent, string text, IndentOptions options = IndentOptions.None)
		{
			string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
			int firstLine = options == IndentOptions.SkipFirst ? 1 : 0;
			int lastLine = lines.Length - 1;
			if (lastLine >= 0 && options == IndentOptions.SkipTrailingEmpty && string.IsNullOrEmpty(lines[lastLine]))
				--lastLine;
			for (int i = firstLine; i <= lastLine; ++i)
			{
				lines[i] = indent + lines[i];
			}
			return string.Join("\n", lines);
		}

		// Gets the position of the character which ends the line.
		private int GetLineEnd(string text, int position)
		{
			while (position < text.Length)
			{
				switch (text[position])
				{
					case '\r':
					case '\n':
						return position;

					default:
						++position;
						break;
				}
			}
			return position;
		}

		// Extract all whitespace from text[position] up to least(non-whitespace, max).
		private string GetWhitespace(string text, int position, int max)
		{
			int start = position;
			while (position < text.Length && position < max)
			{
				char ch = text[position];
				if (ch == '\r' || ch == '\n')
					break;
				if (char.IsWhiteSpace(ch))
					++position;
				else
					break;
			}
			return text.Substring(start, position - start);
		}

		private string GetCurrentIndent(string text, int position)
		{
			int lineStart = GetLineStart(text, position);
			return GetWhitespace(text, lineStart, position);
		}

		// Gets the position of the character which starts the line.
		private int GetLineStart(string text, int position)
		{
			if (position == 0)
				return 0;
			// If we are at the "end" of the line in the editor we are also at the start of the next line
			--position;
			while (position > 0)
			{
				switch (text[position])
				{
					case '\r':
					case '\n':
						return position + 1;

					default:
						--position;
						break;
				}
			}
			return position;
		}

		private char GetNextNonWhite(string text, ref int pos)
		{
			while (pos < text.Length && char.IsWhiteSpace(text, pos))
				++pos;
			if (pos >= text.Length)
				return '\0';
			return text[pos];
		}

		enum SmartDelimiter
		{
			Yes,
			No,
			IndentOnly
		}

		private SmartDelimiter IsSmartDelimiter(string text, int pos, out char closer)
		{
			closer = ' ';
			if (pos < 0 || pos >= text.Length)
				return SmartDelimiter.No;
			int smartIndex = Array.IndexOf(SmartChars, text[pos]);
			if (smartIndex < 0)
				return SmartDelimiter.No;
			closer = SmartInversion[smartIndex];
			int currIndent = GetIndent(GetCurrentIndent(text, pos));
			++pos;
			char nextCh = GetNextNonWhite(text, ref pos);
			int nextIndent = GetIndent(GetCurrentIndent(text, pos));
			// foo(|<-- end of file; smart delimiter
			if (nextCh == '\0')
				return SmartDelimiter.Yes;
			// foo(|<-- next indent is equal indent but not a match; new delimeter
			// blah
			if (currIndent == nextIndent && nextCh != closer)
				return SmartDelimiter.Yes;
			// foo (
			// ..blah (|<-- next indent is less indented; we want a new delimeter
			// )
			//
			// foo (|<-- next indent is equal indent; no new delimiter but do indent
			// )
			//
			// foo(|<-- next indent is more indented; no new delimeter but do indent
			//   abc()
			// )
			if (nextIndent < currIndent)
				return SmartDelimiter.Yes;
			return SmartDelimiter.IndentOnly;
		}


	}

}