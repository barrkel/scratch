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
        // Operators
        /****************************************************************************************************/

        // lets see how long we can get away without arithmetic in the language

        [Action("add")]
        public ScratchValue DoAdd(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("add", args, ScratchType.Int32, ScratchType.Int32);
            return new ScratchValue(args[0].Int32Value + args[1].Int32Value);
        }

        [Action("sub")]
        public ScratchValue DoSub(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("sub", args, ScratchType.Int32, ScratchType.Int32);
            return new ScratchValue(args[0].Int32Value - args[1].Int32Value);
        }

        [Action("mul")]
        public ScratchValue DoMul(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("mul", args, ScratchType.Int32, ScratchType.Int32);
            return new ScratchValue(args[0].Int32Value * args[1].Int32Value);
        }

        [Action("div")]
        public ScratchValue DoDiv(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("div", args, ScratchType.Int32, ScratchType.Int32);
            return new ScratchValue(args[0].Int32Value / args[1].Int32Value);
        }

        [Action("mod")]
        public ScratchValue DoMod(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("mod", args, ScratchType.Int32, ScratchType.Int32);
            return new ScratchValue(args[0].Int32Value % args[1].Int32Value);
        }

        [Action("lt")]
        public ScratchValue DoLt(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("lt", args, ScratchType.Int32, ScratchType.Int32);
            return ScratchValue.From(args[0].Int32Value < args[1].Int32Value);
        }

        [Action("gt")]
        public ScratchValue DoGt(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("gt", args, ScratchType.Int32, ScratchType.Int32);
            return ScratchValue.From(args[0].Int32Value > args[1].Int32Value);
        }

        [Action("le")]
        public ScratchValue DoLe(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("le", args, ScratchType.Int32, ScratchType.Int32);
            return ScratchValue.From(args[0].Int32Value <= args[1].Int32Value);
        }

        [Action("ge")]
        public ScratchValue DoGe(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("ge", args, ScratchType.Int32, ScratchType.Int32);
            return ScratchValue.From(args[0].Int32Value >= args[1].Int32Value);
        }

        [Action("eq")]
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

        [Action("ne")]
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

        [Action("length")]
        public ScratchValue DoLength(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("length", args, ScratchType.String);
            return new ScratchValue(args[0].StringValue.Length);
        }

        [Action("to-int")]
        public ScratchValue DoToInt(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("to-int", args, ScratchType.String);
            if (int.TryParse(args[0].StringValue, out var x))
                return new ScratchValue(x);
            return ScratchValue.Null;
        }

        [Action("to-str")]
        public ScratchValue DoToStr(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("to-str", args, ScratchType.Int32);
            return new ScratchValue(args[0].Int32Value.ToString());
        }

        [Action("gsub")]
        public ScratchValue DoGsub(ExecutionContext context, IList<ScratchValue> args)
        {
            // args: (text-to-scan, regex, replacement)
            Regex re = new Regex(args[1].StringValue,
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
                TimeSpan.FromSeconds(1));
            return new ScratchValue(re.Replace(args[0].StringValue, args[2].StringValue));
        }

        [Action("match-re")]
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

        [Action("concat")]
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

        [Action("format")]
        public ScratchValue DoFormat(ExecutionContext context, IList<ScratchValue> args)
        {
            return new ScratchValue(string.Format(args[0].StringValue,
                args.Skip(1).Select(x => x.ObjectValue).ToArray()));
        }

        [Action("get-string-from-to")]
        public ScratchValue GetStringFromTo(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("get-string-from-to", args, ScratchType.String, ScratchType.Int32, ScratchType.Int32);
            return ScratchValue.From(args[0].StringValue.Substring(args[1].Int32Value, args[1].Int32Value + args[2].Int32Value));
        }

        [Action("char-at")]
        public ScratchValue GetCharAt(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("char-at", args, ScratchType.String, ScratchType.Int32);
            int pos = args[1].Int32Value;
            string s = args[0].StringValue;
            if (pos < 0 || pos >= s.Length)
                return ScratchValue.Null;
            return ScratchValue.From(s.Substring(pos, 1));
        }

        /****************************************************************************************************/
        // OS interaction
        /****************************************************************************************************/

        [Action("open")]
        public void DoOpen(ExecutionContext context, IList<ScratchValue> args)
        {
            if (args.Count != 1)
                throw new ArgumentException($"Expected exactly one argument to open, got {args.Count}");
            Process.Start(args[0].StringValue);
        }

        [Action("exec")]
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

        [Action("get-view-text")]
        public ScratchValue GetViewText(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("get-view-text", args);
            return ScratchValue.From(context.View.CurrentText);
        }

        [Action("get-view-pos")]
        public ScratchValue GetViewPos(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("get-view-pos", args);
            return ScratchValue.From(context.View.CurrentPosition);
        }

        [Action("get-line-start")]
        public ScratchValue DoGetLineStart(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("get-line-start", args, ScratchType.String, ScratchType.Int32);
            return ScratchValue.From(GetLineStart(args[0].StringValue, args[1].Int32Value));
        }

        [Action("get-line-end")]
        public ScratchValue DoGetLineEnd(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("get-line-end", args, ScratchType.String, ScratchType.Int32);
            return ScratchValue.From(GetLineEnd(args[0].StringValue, args[1].Int32Value));
        }

        [Action("set-view-pos")]
        public void SetViewPos(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("set-view-pos", args, ScratchType.Int32);
            int pos = args[0].Int32Value;
            context.View.Selection = (pos, pos);
        }

        [Action("set-view-selection")]
        public void SetViewSelection(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("get-view-selection", args, ScratchType.Int32, ScratchType.Int32);
            context.View.Selection = (args[0].Int32Value, args[1].Int32Value);
        }

        [Action("get-view-selected-text")]
        public ScratchValue GetViewSelectedText(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("get-view-selected-text", args);
            return ScratchValue.From(context.View.SelectedText);
        }

        [Action("set-view-selected-text")]
        public void SetViewSelectedText(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("set-view-selected-text", args, ScratchType.String);
            context.View.SelectedText = args[0].StringValue;
        }

        [Action("scroll-pos-into-view")]
        public void ScrollPosIntoView(ExecutionContext context, IList<ScratchValue> args)
        {
            Validate("scroll-pos-into-view", args, ScratchType.Int32);
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

        [Action("goto-eol")]
        public void GotoEol(ExecutionContext context, IList<ScratchValue> args)
        {
            string text = context.View.CurrentText;
            int pos = context.View.CurrentPosition;
            int eol = GetLineEnd(text, pos);
            context.View.Selection = (eol, eol);
        }

        [Action("goto-sol")]
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
