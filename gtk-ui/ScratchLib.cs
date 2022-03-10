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

        // let's see how long we can get away without arithmetic in the language

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


    }
}
