using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        [TypedAction("gsub", ScratchType.String, ScratchType.String, ScratchType.String)]
        public ScratchValue DoGsub(ExecutionContext context, IList<ScratchValue> args)
        {
            // args: (text-to-scan, regex, replacement)
            Regex re = new Regex(args[1].StringValue,
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
                TimeSpan.FromSeconds(1));
            return new ScratchValue(re.Replace(args[0].StringValue, args[2].StringValue));
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
            return ScratchValue.From(args[0].StringValue.Substring(args[1].Int32Value, args[1].Int32Value + args[2].Int32Value));
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
