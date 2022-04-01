using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Barrkel.ScratchPad
{
    public class ScratchLib : NativeLibrary
    {
        public static readonly ScratchLib Instance = new ScratchLib();

        public ScratchLib() : base("lib")
        {
        }

        /****************************************************************************************************/
        // Config and initialization
        /****************************************************************************************************/

        private IEnumerable<(ScratchBook,string,int)> FindConfigs(ScratchRoot root, string pattern)
        {
            Regex regex = new Regex(pattern);
            return root.Books.SelectMany(book => 
                    book.SearchTitles(regex)
                            .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
                            .Select(x => (book, x.Item1, x.Item2)));
        }

        public void LoadConfig(ScratchRoot root)
        {
            // Reset global config if missing
            string path = Path.Combine(root.RootDirectory, "0-globalconfig.txt");
            if (!File.Exists(path))
                File.WriteAllText(path, GtkScratchPad.Resources.globalconfig);

            bool debugBinding = ((ScratchScope)root.RootScope).GetOrDefault("debug-binding", 0) > 0;

            // Load global configs first
            foreach (var (book, title, index) in FindConfigs(root, @"^\.globalconfig\b.*"))
            {
                var library = ConfigFileLibrary.Load(debugBinding, title, book.Pages[index].Text);
                try
                {
                    ((ScratchScope)root.RootScope).Load(library);
                }
                catch (Exception ex)
                {
                    Log.Out(ex.Message);
                }
            }
            foreach (var (book, title, index) in FindConfigs(root, @"^\.config\b.*"))
            {
                var library = ConfigFileLibrary.Load(debugBinding, title, book.Pages[index].Text);
                try
                {
                    ((ScratchScope)book.Scope).Load(library);
                }
                catch (Exception ex)
                {
                    Log.Out(ex.Message);
                }
            }
        }

        [TypedAction("load-config")]
        public void DoLoadConfig(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.EnsureSaved();
            // TODO: consider load vs reload
            LoadConfig(context.Controller.RootController.Root);
            Log.Out("Config reloaded.");
        }

        [TypedAction("reset-config")]
        public void ResetConfig(ExecutionContext context, IList<ScratchValue> args)
        {
            ScratchRoot root = context.Controller.RootController.Root;
            string path = Path.Combine(root.RootDirectory, "0-globalconfig.txt");
            File.WriteAllText(path, GtkScratchPad.Resources.globalconfig);
            Log.Out($"{path} written.");
        }

        [TypedAction("restart-app")]
        public void RestartApp(ExecutionContext context, IList<ScratchValue> args)
        {
            context.Controller.RootController.Exit(ExitIntent.Restart);
        }

        [TypedAction("exit-app")]
        public void ExitApp(ExecutionContext context, IList<ScratchValue> args)
        {
            context.Controller.RootController.Exit(ExitIntent.Exit);
        }

        /****************************************************************************************************/
        // Meta
        /****************************************************************************************************/

        [TypedAction("is-defined", ScratchType.String)]
        public ScratchValue IsDefined(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(context.Scope.TryLookup(args[0].StringValue, out _));
        }

        /****************************************************************************************************/
        // Operators
        /****************************************************************************************************/

        // lets see how long we can get away without arithmetic in the language

        [TypedAction("add", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoAdd(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(args[0].Int32Value + args[1].Int32Value);
        }

        [TypedAction("sub", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoSub(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(args[0].Int32Value - args[1].Int32Value);
        }

        [TypedAction("mul", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoMul(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(args[0].Int32Value * args[1].Int32Value);
        }

        [TypedAction("div", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoDiv(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(args[0].Int32Value / args[1].Int32Value);
        }

        [TypedAction("mod", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoMod(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(args[0].Int32Value % args[1].Int32Value);
        }

        [TypedAction("lt", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoLt(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(args[0].Int32Value < args[1].Int32Value);
        }

        [TypedAction("gt", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoGt(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(args[0].Int32Value > args[1].Int32Value);
        }

        [TypedAction("le", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoLe(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(args[0].Int32Value <= args[1].Int32Value);
        }

        [TypedAction("ge", ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue DoGe(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(args[0].Int32Value >= args[1].Int32Value);
        }

        [VariadicAction("eq")]
        public ScratchValue DoEq(ExecutionContext context, IList<ScratchValue> args)
        {
            ValidateLength("eq", args, 2);
            if (args[0].Type != args[1].Type)
                return ScratchValue.False;
            switch (args[0].Type)
            {
                case ScratchType.Int32:
                    return ScratchValue.From(args[0].Int32Value == args[1].Int32Value);

                case ScratchType.String:
                    return ScratchValue.From(args[0].StringValue == args[1].StringValue);

                default:
                    return ScratchValue.From(args[0].ObjectValue == args[1].ObjectValue);
            }
        }

        [VariadicAction("ne")]
        public ScratchValue DoNe(ExecutionContext context, IList<ScratchValue> args)
        {
            ValidateLength("ne", args, 2);
            if (args[0].Type != args[1].Type)
                return ScratchValue.True;
            switch (args[0].Type)
            {
                case ScratchType.Int32:
                    return ScratchValue.From(args[0].Int32Value != args[1].Int32Value);

                case ScratchType.String:
                    return ScratchValue.From(args[0].StringValue != args[1].StringValue);

                default:
                    return ScratchValue.From(args[0].ObjectValue != args[1].ObjectValue);
            }
        }

        /****************************************************************************************************/
        // String operations
        /****************************************************************************************************/

        [TypedAction("length", ScratchType.String)]
        public ScratchValue DoLength(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(args[0].StringValue.Length);
        }

        [TypedAction("to-int", ScratchType.String)]
        public ScratchValue DoToInt(ExecutionContext context, IList<ScratchValue> args)
        {
            if (int.TryParse(args[0].StringValue, out var x))
                return new ScratchValue(x);
            return ScratchValue.Null;
        }

        [TypedAction("to-str", ScratchType.Int32)]
        public ScratchValue DoToStr(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(args[0].Int32Value.ToString());
        }

        [VariadicAction("gsub")]
        public ScratchValue DoGsub(ExecutionContext context, IList<ScratchValue> args)
        {
            // args: (text-to-scan, regex, replacement)
            // Replacement may be a string or a function invoked per match.
            // If it's a function, and it has a parameter, it receives the matched text.
            // If the regex has named capture groups, they're locally bound for the duration of the invocation.
            ValidateLength("gsub", args, 3);
            ValidateArgument("gsub", args, 0, ScratchType.String);
            ValidateArgument("gsub", args, 1, ScratchType.String);
            ValidateArgument("gsub", args, 2, ScratchType.String, ScratchType.ScratchFunction);
            Regex re = new Regex(args[1].StringValue,
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline,
                TimeSpan.FromSeconds(1));
            ScratchValue replacement = args[2];
            switch (replacement.Type)
            {
                case ScratchType.String:
                    return new ScratchValue(re.Replace(args[0].StringValue, replacement.StringValue));

                case ScratchType.ScratchFunction:
                    return ScratchValue.From(re.Replace(args[0].StringValue, match =>
                    {
                        var child = context.CreateChild("gsub/callback");
                        foreach (Group g in match.Groups)
                            child.Scope.AssignLocal(g.Name, ScratchValue.From(g.Value));
                        switch (replacement.FunctionValue.Parameters.Count)
                        {
                            case 0:
                                return replacement.FunctionValue.Program.Run(child).StringValue;

                            case 1:
                                child.Scope.AssignLocal(replacement.FunctionValue.Parameters[0],
                                    ScratchValue.From(match.Value));
                                return replacement.FunctionValue.Program.Run(child).StringValue;

                            default:
                                throw new ArgumentException("Function arg to gsub may have at most 1 parameter");
                        }
                    }));

                default:
                    throw new ArgumentException("Never reached");
            }
        }

        [TypedAction("match-re", ScratchType.String, ScratchType.String)]
        public ScratchValue DoMatchRe(ExecutionContext context, IList<ScratchValue> args)
        {
            // args: (text-to-scan, regex)
            // return text of matching regex if match, null otherwise
            Regex re = new Regex(args[1].StringValue,
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
                TimeSpan.FromSeconds(1));
            Match m = re.Match(args[0].StringValue);
            if (m.Success)
                return new ScratchValue(m.Value);
            return ScratchValue.Null;
        }

        [TypedAction("escape-re", ScratchType.String)]
        public ScratchValue EscapeRe(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(Regex.Escape(args[0].StringValue));
        }

        [VariadicAction("concat")]
        public ScratchValue DoConcat(ExecutionContext context, IList<ScratchValue> args)
        {
            StringBuilder result = new StringBuilder();
            foreach (var sv in args)
            {
                switch (sv.Type)
                {
                    case ScratchType.Int32:
                        result.Append(sv.Int32Value);
                        break;

                    case ScratchType.String:
                        result.Append(sv.StringValue);
                        break;

                    default:
                        break;
                }
            }
            return new ScratchValue(result.ToString());
        }

        [VariadicAction("format")]
        public ScratchValue DoFormat(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(string.Format(args[0].StringValue,
                args.Skip(1).Select(x => x.ObjectValue).ToArray()));
        }

        [TypedAction("get-string-from-to", ScratchType.String, ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue GetStringFromTo(ExecutionContext context, IList<ScratchValue> args)
        {
            int startIndex = args[1].Int32Value;
            int endIndex = args[2].Int32Value;
            int length = endIndex - startIndex;
            return ScratchValue.From(args[0].StringValue.Substring(startIndex, length));
        }

        [TypedAction("substring", ScratchType.String, ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue Subtring(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(args[0].StringValue.Substring(args[1].Int32Value, args[2].Int32Value));
        }

        [TypedAction("char-at", ScratchType.String, ScratchType.Int32)]
        public ScratchValue GetCharAt(ExecutionContext context, IList<ScratchValue> args)
        {
            int pos = args[1].Int32Value;
            string s = args[0].StringValue;
            if (pos < 0 || pos >= s.Length)
                return ScratchValue.Null;
            return ScratchValue.From(s.Substring(pos, 1));
        }

        // This is also 'filter-lines' because null transformations will be filtered out
        [TypedAction("transform-lines", ScratchType.String, ScratchType.ScratchFunction)]
        public ScratchValue TransformLines(ExecutionContext context, IList<ScratchValue> args)
        {
            ScratchFunction func = args[1].FunctionValue;
            return ScratchValue.From(string.Join("\n", args[0].StringValue.Split('\n')
                .Select(x => func.Invoke("transform-lines/callback", context, ScratchValue.List(x)))
                .Where(x => !x.IsFalse)
                .Select(x => x.StringValue)));
        }

        [TypedAction("sort-lines", ScratchType.String)]
        public ScratchValue SortLines(ExecutionContext context, IList<ScratchValue> args)
        {
            // Consider case sensitivity, version sorting
            return ScratchValue.From(string.Join("\n", args[0].StringValue.Split('\n')
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
        }

        [TypedAction("reverse-lines", ScratchType.String)]
        public ScratchValue ReverseLines(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(string.Join("\n", args[0].StringValue.Split('\n').Reverse()));
        }

        /****************************************************************************************************/
        // String operations which are more 'editor' operations
        /****************************************************************************************************/

        [TypedAction("get-line-indent", ScratchType.String, ScratchType.Int32)]
        // get-whitespace(text, get-line-start(text, position), position)
        public ScratchValue GetLineIndent(ExecutionContext context, IList<ScratchValue> args)
        {
            string text = args[0].StringValue;
            int position = args[1].Int32Value;
            int lineStart = GetLineStart(text, position);
            return ScratchValue.From(DoGetWhitespace(text, lineStart, position));
        }

        // Extract all whitespace from text[position] up to least(non-whitespace, max).
        // get-whitespace(text, position, max)
        [TypedAction("get-whitespace", ScratchType.String, ScratchType.Int32, ScratchType.Int32)]
        public ScratchValue GetWhitespace(ExecutionContext context, IList<ScratchValue> args)
        {
            string text = args[0].StringValue;
            int position = args[1].Int32Value;
            int max = args[2].Int32Value;

            return ScratchValue.From(DoGetWhitespace(text, position, max));
        }

        private string DoGetWhitespace(string text, int position, int max)
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

        [TypedAction("reset-indent", ScratchType.String)]
        public ScratchValue ResetIndent(ExecutionContext context, IList<ScratchValue> args)
        {
            string text = args[0].StringValue;

            string[] lines = text.Split('\r', '\n');
            int minIndent = int.MaxValue;
            // TODO: make this tab-aware
            foreach (string line in lines)
            {
                int indent = DoGetWhitespace(line, 0, line.Length).Length;
                // don't count empty lines, or lines with only whitespace
                if (indent == line.Length)
                    continue;
                minIndent = Math.Min(minIndent, indent);
            }
            if (minIndent == 0)
                return ScratchValue.From(text);
            for (int i = 0; i < lines.Length; ++i)
                if (minIndent >= lines[i].Length)
                    lines[i] = "";
                else
                    lines[i] = lines[i].Substring(minIndent);
            return ScratchValue.From(string.Join("\n", lines));
        }

        // (text, indent) - note this is opposite to original
        [TypedAction("add-indent", ScratchType.String, ScratchType.String)]
        public ScratchValue AddIndent(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(AddIndent(args[0].StringValue, args[1].StringValue));
        }

        enum IndentOptions
        {
            None,
            SkipFirst,
            SkipTrailingEmpty
        }

        private string AddIndent(string text, string indent, IndentOptions options = IndentOptions.None)
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


        /****************************************************************************************************/
        // OS interaction
        /****************************************************************************************************/

        [VariadicAction("open")]
        public void DoOpen(ExecutionContext context, IList<ScratchValue> args)
        {
            if (args.Count != 1)
                throw new ArgumentException($"Expected exactly one argument to open, got {args.Count}");
            Process.Start(args[0].StringValue);
        }

        [VariadicAction("exec")]
        public void DoExec(ExecutionContext context, IList<ScratchValue> args)
        {
            // TODO: upgrade to .net Core and use ArgumentList
            ProcessStartInfo info = new ProcessStartInfo(args[0].StringValue,
                string.Join(" ", args.Skip(1).Select(x => x.StringValue)))
            {
                UseShellExecute = false
            };
            Process.Start(info);
        }

        /****************************************************************************************************/
        // Version history
        /****************************************************************************************************/

        [TypedAction("goto-next-version")]
        public void GotoNextVersion(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.Navigate(iter => iter.MoveNext());
        }

        [TypedAction("goto-previous-version")]
        public void GotoPreviousVersion(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.Navigate(iter => iter.MovePrevious());
        }

        [TypedAction("goto-next-major-version", ScratchType.Int32)]
        public void GotoNextMajorVersion(ExecutionContext context, IList<ScratchValue> args)
        {
            TimeSpan minBetween = TimeSpan.FromMinutes(args[0].Int32Value);
            context.View.Navigate(iter =>
            {
                var prev = iter.Stamp;
                while (iter.MoveNext())
                {
                    TimeSpan interval = iter.Stamp - prev;
                    if (interval >= minBetween)
                    {
                        iter.MovePrevious();
                        return true;
                    }
                }
                return true;
            });
        }

        [TypedAction("goto-previous-major-version", ScratchType.Int32)]
        public void GotoPreviousMajorVersion(ExecutionContext context, IList<ScratchValue> args)
        {
            TimeSpan minBetween = TimeSpan.FromMinutes(args[0].Int32Value);
            context.View.Navigate(iter =>
            {
                var next = iter.Stamp;
                while (iter.MovePrevious())
                {
                    TimeSpan interval = next - iter.Stamp;
                    if (interval >= minBetween)
                        return true;
                }
                return true;
            });
        }

        /****************************************************************************************************/
        // View interaction
        /****************************************************************************************************/

        [VariadicAction("insert-text")]
        public void DoInsertText(ExecutionContext context, IList<ScratchValue> args)
        {
            foreach (var arg in args.Where(x => x.Type == ScratchType.String))
                context.View.InsertText(arg.StringValue);
        }

        [TypedAction("get-clipboard")]
        public ScratchValue GetClipboard(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(context.View.Clipboard);
        }

        [TypedAction("get-view-text")]
        public ScratchValue GetViewText(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(context.View.CurrentText);
        }

        [TypedAction("get-view-pos")]
        public ScratchValue GetViewPos(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(context.View.CurrentPosition);
        }

        [TypedAction("get-line-start", ScratchType.String, ScratchType.Int32)]
        public ScratchValue DoGetLineStart(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(GetLineStart(args[0].StringValue, args[1].Int32Value));
        }

        [TypedAction("get-line-end", ScratchType.String, ScratchType.Int32)]
        public ScratchValue DoGetLineEnd(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(GetLineEnd(args[0].StringValue, args[1].Int32Value));
        }

        [TypedAction("set-view-pos", ScratchType.Int32)]
        public void SetViewPos(ExecutionContext context, IList<ScratchValue> args)
        {
            int pos = args[0].Int32Value;
            context.View.Selection = (pos, pos);
        }

        [TypedAction("set-view-selection", ScratchType.Int32, ScratchType.Int32)]
        public void SetViewSelection(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.Selection = (args[0].Int32Value, args[1].Int32Value);
        }

        [TypedAction("get-view-selected-text")]
        public ScratchValue GetViewSelectedText(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(context.View.SelectedText);
        }

        [TypedAction("set-view-selected-text", ScratchType.String)]
        public void SetViewSelectedText(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.SelectedText = args[0].StringValue;
        }

        [TypedAction("scroll-pos-into-view", ScratchType.Int32)]
        public void ScrollPosIntoView(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.ScrollIntoView(args[0].Int32Value);
        }

        [TypedAction("ensure-saved")]
        public void EnsureSaved(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.EnsureSaved();
        }

        [TypedAction("get-page-index")]
        public ScratchValue GetPageIndex(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(context.View.CurrentPageIndex);
        }

        [TypedAction("set-page-index", ScratchType.Int32)]
        public void SetPageIndex(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.CurrentPageIndex = args[0].Int32Value;
        }

        [TypedAction("get-page-count")]
        public ScratchValue GetPageCount(ExecutionContext context, IList<ScratchValue> args)
        {
            return ScratchValue.From(context.Controller.Book.Pages.Count);
        }

        // Gets the position of the character which starts the line.
        private int GetLineStart(string text, int position)
        {
            if (position == 0)
                return 0;
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

        [VariadicAction("get-input")]
        public ScratchValue GetInput(ExecutionContext context, IList<ScratchValue> args)
        {
            ScratchScope settings = context.Scope;
            if (args.Count == 1)
            {
                ValidateArgument("get-input", args, 0, ScratchType.ScratchFunction);
                settings = EvaluateScratchScope(context, args[0].FunctionValue, "get-input/callback");
            }
            if (context.View.GetInput(settings, out string result))
                return ScratchValue.From(result);
            return ScratchValue.Null;
        }

        /****************************************************************************************************/
        // Snippet dialog
        /****************************************************************************************************/

        private ScratchScope EvaluateScratchScope(ExecutionContext context, ScratchFunction function, string name)
        {
            ExecutionContext child = context.CreateChild(name);
            function.Program.Run(context);
            return child.Scope;
        }

        [TypedAction("launch-snippet", ScratchType.ScratchFunction)]
        public void LaunchSnippet(ExecutionContext context, IList<ScratchValue> args)
        {
            context.View.LaunchSnippet(EvaluateScratchScope(context, args[0].FunctionValue, "launch-snippet/callback"));
        }

        /****************************************************************************************************/
        // Search dialog
        /****************************************************************************************************/

        private const int DefaultContextLength = 40;

        // Arg is regex to prefilter for.
        // TODO: transform output
        // TOOD: clean up context display
        [TypedAction("search-current-page", ScratchType.String)]
        public void SearchCurrentPage(ExecutionContext context, IList<ScratchValue> args)
        {
            Regex prefilter = new Regex(args[0].StringValue);
            var lines = GetNonEmptyLines(context.View.CurrentText)
                .Where(item => prefilter.IsMatch(item.Item2)).ToList();
            if (context.View.RunSearch(pattern => FindMatchingLocations(lines, ParseRegexList(pattern, RegexOptions.Singleline)),
                out var pair))
            {
                var (pos, len) = pair;
                context.View.ScrollIntoView(pos);
                context.View.Selection = (pos, pos + len);
            }
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

        /****************************************************************************************************/
        // Diagnostics
        /****************************************************************************************************/

        [TypedAction("debug-stack", ScratchType.Int32)]
        public void DebugStack(ExecutionContext context, IList<ScratchValue> args)
        {
            ScratchProgram.DebugStack = args[0].Int32Value > 0;
        }

        [TypedAction("dump-scopes")]
        public void DumpScopes(ExecutionContext context, IList<ScratchValue> args)
        {
            Log.Out("Root scope:");
            foreach (var (name, value) in context.Controller.RootController.RootScope)
                Log.Out($"  {name} = {value}");
            Log.Out("Book scope:");
            foreach (var (name, value) in context.Controller.Scope)
                Log.Out($"  {name} = {value}");
        }

        /****************************************************************************************************/
        // Obsolete actions
        /****************************************************************************************************/

        // these actions can be completely replaced by script
        // we need to look at shipping a standard library script

        [TypedAction("goto-eol")]
        public void GotoEol(ExecutionContext context, IList<ScratchValue> args)
        {
            string text = context.View.CurrentText;
            int pos = context.View.CurrentPosition;
            int eol = GetLineEnd(text, pos);
            context.View.Selection = (eol, eol);
        }

        [TypedAction("goto-sol")]
        public void GotoSol(ExecutionContext context, IList<ScratchValue> args)
        {
            // NOTE: does not extend selection
            string text = context.View.CurrentText;
            int pos = context.View.CurrentPosition;
            int sol = GetLineStart(text, pos);
            context.View.Selection = (sol, sol);
        }
    }
}
