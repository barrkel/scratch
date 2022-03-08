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
	public enum ScratchType
	{
		Null,
		String, // string
		Int32, // integer
		ScratchFunction,
		Action
	}

	public class ExecutionContext
	{
		public ExecutionContext(ScratchBookController controller, IScratchBookView view, ScratchScope scope)
		{
			Controller = controller;
			View = view;
			Scope = scope;
		}

		public ExecutionContext CreateChild()
		{
			return new ExecutionContext(Controller, View, new ScratchScope(Scope));
		}

		public ScratchBookController Controller { get; }
		public IScratchBookView View { get; }
		public ScratchScope Scope { get; }
	}

	public delegate ScratchValue ScratchAction(ExecutionContext context, IList<ScratchValue> args);

	public class ScratchFunction
	{
		public ScratchFunction(ScratchProgram program, List<string> parameters)
		{
			Program = program;
			Parameters = new ReadOnlyCollection<string>(parameters);
		}

		public ReadOnlyCollection<string> Parameters { get; }
		public ScratchProgram Program { get; }

		public ScratchValue Invoke(ExecutionContext context, IList<ScratchValue> args)
		{
			ExecutionContext child = context.CreateChild();
			if (args.Count != Parameters.Count)
				throw new InvalidOperationException(
					$"Parameter count mismatch: expected {Parameters.Count}, got {args.Count}");
			for (int i = 0; i < Parameters.Count; ++i)
			{
				Console.WriteLine($"Assigning {args[i]} to {Parameters[i]}");
				child.Scope.AssignLocal(Parameters[i], args[i]);
			}
			return Program.Run(child);
		}

		public override string ToString()
		{
			StringBuilder result = new StringBuilder();
			result.Append("{");
			if (Parameters.Count > 0)
				result.Append("|").Append(string.Join(",", Parameters)).Append("| ");
			result.Append(Program);
			result.Append("}");
			return result.ToString();
		}
	}

	public class ScratchValue
	{
		public static readonly ScratchValue Null = new ScratchValue(null, ScratchType.Null);
		public static readonly IList<ScratchValue> EmptyList = new ScratchValue[0];

		private Object _value;

		private ScratchValue(object value, ScratchType type)
		{
			_value = value;
			Type = type;
		}

		public ScratchValue(string value)
		{
			_value = value;
			Type = ScratchType.String;
		}

		public ScratchValue(int value)
		{
			_value = value;
			Type = ScratchType.Int32;
		}

		public ScratchValue(ScratchAction action)
		{
			_value = action;
			Type = ScratchType.Action;
		}

		public ScratchValue(ScratchFunction func)
		{
			_value = func;
			Type = ScratchType.ScratchFunction;
		}

		public static ScratchValue FromObject(object value)
		{
			switch (value)
			{
				case null:
					return Null;

				case ScratchValue scratchValue:
					return scratchValue;

				case string stringValue:
					return new ScratchValue(stringValue);

				case int intValue:
					return new ScratchValue(intValue);

				case ScratchAction action:
					return new ScratchValue(action);

				case ScratchFunction func:
					return new ScratchValue(func);

				default:
					throw new ArgumentException("Invalid type: " + value);
			}
		}

		public ScratchType Type { get; }
		public String StringValue => (string)_value;
		public int Int32Value => (int)_value;

		public ScratchValue Invoke(ExecutionContext context, IList<ScratchValue> args)
		{
			switch (Type)
			{
				case ScratchType.Action:
					return ScratchValue.FromObject(((ScratchAction)_value)(context, args));

				case ScratchType.ScratchFunction:
					return ((ScratchFunction)_value).Invoke(context, args);

				// invoking a string binding invokes whatever the string is itself bound to, recursively
				// this lets us use strings as function pointers, as long as we don't mind naming our functions
				case ScratchType.String:
					Console.WriteLine($"Invoking {StringValue}");
					return context.Scope.Lookup(StringValue).Invoke(context, args);

				default:
					throw new InvalidOperationException("Tried to invoke non-function: " + this);
			}
		}

		public override string ToString()
		{
			return $"SV({_value})";
		}
	}

	// Basic stack machine straight from parser.
	public class ScratchProgram
	{
		public enum Operation
		{
			// arg is ScratchValue to push
			Push,
			Pop,
			// arg is name, stack is N followed by N arguments
			Call,
			// arg is name, value is peeked.
			// Existing binding is searched for and assigned in the scope it's found.
			Set,
			// Fetch value of existing binding
			Get,
			// arg is name, value is peeked.
			// Create binding in top scope and assign.
			SetLocal,
			// Early exit from this program, result is top of stack
			Ret,
		}

		public struct Op
		{
			public Op(Operation op)
			{
				Operation = op;
				Arg = null;
			}

			public Op(Operation op, ScratchValue arg)
			{
				Operation = op;
				Arg = arg;
			}

			public Operation Operation { get; }
			public ScratchValue Arg { get; }
			public string ArgAsString => Arg.StringValue;

			public override string ToString()
			{
				return $"{Operation} {Arg}";
			}
		}

		private List<Op> _ops;

		public class Writer
		{
			Dictionary<string, int> _labels = new Dictionary<string, int>();
			List<Op> _ops = new List<Op>();
			List<int> _fixups = new List<int>();

			public string NewLabel(string prefix)
			{
				string result = $"{prefix}{_labels.Count}";
				_labels[result] = -1;
				return result;
			}

			public void ResolveLabel(string label)
			{
				_labels[label] = _ops.Count;
			}

			public void AddOp(Operation op)
			{
				_ops.Add(new Op(op));
			}

			public void AddOpWithLabel(Operation op, string label)
			{
				_fixups.Add(_ops.Count);
				_ops.Add(new Op(op, new ScratchValue(label)));
			}

			public void AddOp(Operation op, ScratchValue value)
			{
				_ops.Add(new Op(op, value));
			}

			public ScratchProgram ToProgram()
			{
				foreach (int fixup in _fixups)
				{
					string label = _ops[fixup].ArgAsString;
					int loc = _labels[label];
					if (loc == -1)
						throw new Exception("Label not resolved: " + label);
					_ops[fixup] = new Op(_ops[fixup].Operation, new ScratchValue(loc));
				}
				ScratchProgram result = new ScratchProgram(_ops);
				_ops = null;
				return result;
			}
		}

		public static ScratchProgram WithWriter(Action<Writer> w)
		{
			Writer writer = new Writer();
			w(writer);
			return writer.ToProgram();
		}

		private ScratchProgram(List<Op> ops)
		{
			_ops = ops;
		}

		private ScratchValue Pop(List<ScratchValue> stack)
		{
			ScratchValue result = stack[stack.Count - 1];
			stack.RemoveAt(stack.Count - 1);
			return result;
		}

		private ScratchValue Peek(List<ScratchValue> stack)
		{
			return stack[stack.Count - 1];
		}

		private List<ScratchValue> PopArgList(List<ScratchValue> stack)
		{
			int count = Pop(stack).Int32Value;
			var args = new List<ScratchValue>();
			for (int i = 0; i < count; ++i)
				args.Add(Pop(stack));
			// Args are pushed left to right so they pop off from right to left
			args.Reverse();
			return args;
		}

		public ScratchValue Run(ExecutionContext context)
		{
			var stack = new List<ScratchValue>();
			int ip = 0;

			while (ip < _ops.Count)
			{
				int cp = ip++;
				switch (_ops[cp].Operation)
				{
					case Operation.Push:
						stack.Add(_ops[cp].Arg);
						break;

					case Operation.Pop:
						Pop(stack);
						break;

					case Operation.Get:
						stack.Add(context.Scope.Lookup(_ops[cp].ArgAsString));
						break;

					case Operation.Ret:
						return Pop(stack);

					case Operation.Set:
						context.Scope.Assign(_ops[cp].ArgAsString, Peek(stack));
						break;

					case Operation.SetLocal:
						context.Scope.AssignLocal(_ops[cp].ArgAsString, Peek(stack));
						break;

					case Operation.Call:
						stack.Add(context.Scope.Lookup(_ops[cp].ArgAsString).Invoke(context, PopArgList(stack)));
						break;
				}
			}

			return ScratchValue.Null;
		}

		public override string ToString()
		{
			return string.Join("; ", _ops);
		}
	}

	public class ConfigFileLibrary : ScratchLibraryBase
	{
		private ConfigFileLibrary(string name) : base(name)
		{
		}

		// Additively load bindings from text.
		public static ConfigFileLibrary Load(string name, string source)
		{
			// TODO: implement if; a lot of things get better with if
			// TODO: consider early exit (return)
			// TODO: consider error handling
			// TODO: implement argument binding for blocks so we can write functions
			// TODO: extend ScratchValue with basic syntax tree, mainly for easier introspection of bindings
			// TODO: rewrite as many actions as possible in ScratchConf to get a feel for limits
			// TODO: accept identifier syntax in more contexts if not ambiguous
			// TODO: allow binding keys to functions directly, turns out it's tedious to have to name everything

			/*
			   Grammar:
				  file ::= { setting } .
				  setting ::= (<ident> | <string>) '=' literal ;
				  literal ::= <string> | <number> | block ;
				  block ::= '{' [paramList] { expr } '}' ;
				  paramList ::= '|' { <ident> } '|' ;
				  expr ::= callOrAssign | literal ;
				  callOrAssign ::= <ident> ( 
					  ['(' { expr } ')']  // invoke binding
					| '=' expr            // assign to binding, wherever found
					| ':=' expr           // assign to local binding
					|                     // fetch value of binding
					) ;
			 */

			ConfigFileLibrary result = new ConfigFileLibrary(name);

			try
			{
				ScopeLexer lexer = new ScopeLexer(source);

				// skip first line (title)
				lexer.SkipPastEol();
				lexer.NextToken();

				while (lexer.CurrToken != ScopeToken.Eof)
				{
					// setting ::= (<ident> | <string>) '=' literal ;
					lexer.ExpectEither(ScopeToken.Ident, ScopeToken.String);
					string ident = lexer.StringValue;
					lexer.NextToken();
					lexer.Eat(ScopeToken.Eq);
					var value = ParseLiteral(lexer);
					result.Bindings.Add(ident, value);
					Console.WriteLine("Bound {0} to {1}", ident, value);
				}

				return result;
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"{name}: {ex.Message}", ex);
			}
		}

		private static ScratchValue ParseLiteral(ScopeLexer lexer)
		{
			ScratchValue result;
			// literal::= <string> | <number> | block;
			switch (lexer.CurrToken)
			{
				case ScopeToken.String:
					result = new ScratchValue(lexer.StringValue);
					lexer.NextToken();
					return result;

				case ScopeToken.Int32:
					result = new ScratchValue(lexer.Int32Value);
					lexer.NextToken();
					return result;

				case ScopeToken.LBrace:
					return new ScratchValue(CompileBlock(lexer));
			}

			throw lexer.Error($"Expected: string, int or {{, got {lexer.CurrToken}");
		}

		private static ScratchFunction CompileBlock(ScopeLexer lexer)
		{
			var w = new ScratchProgram.Writer();
			// block ::= '{' [paramList] { expr } '}' ;
			lexer.Eat(ScopeToken.LBrace);

			// paramList ::= '|' { <ident> } '|' ;
			var paramList = new List<string>();
			if (lexer.CurrToken == ScopeToken.Bar)
			{
				lexer.NextToken();
				while (lexer.IsNot(ScopeToken.Eof, ScopeToken.Bar))
				{
					lexer.Expect(ScopeToken.Ident);
					paramList.Add(lexer.StringValue);
					lexer.NextToken();
					if (lexer.CurrToken == ScopeToken.Comma)
						lexer.NextToken();
					else
						break;
				}
				lexer.Eat(ScopeToken.Bar);
			}

			bool prevRetValOnStack = false;
			while (lexer.IsNot(ScopeToken.Eof, ScopeToken.RBrace))
			{
				if (prevRetValOnStack)
					w.AddOp(ScratchProgram.Operation.Pop);
				CompileExpr(w, lexer);
				prevRetValOnStack = true;
			}
			lexer.Eat(ScopeToken.RBrace);
			if (!prevRetValOnStack)
				w.AddOp(ScratchProgram.Operation.Push, ScratchValue.Null);
			w.AddOp(ScratchProgram.Operation.Ret);
			return new ScratchFunction(w.ToProgram(), paramList);
		}

		private static void CompileExpr(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			//  expr ::= callOrAssign | literal ;

			if (lexer.CurrToken == ScopeToken.Ident)
			{
				//  callOrAssign ::= <ident> ( 
				//      ['(' { expr } ')']  // invoke binding
				//    | '=' expr            // assign to binding, wherever found
				//    | ':=' expr           // assign to local binding
				//    |                     // fetch value of binding
				//    ) ;

				string name = lexer.StringValue;
				lexer.NextToken();

				switch (lexer.CurrToken)
				{
					case ScopeToken.Eq:
						lexer.NextToken();
						CompileExpr(w, lexer);
						w.AddOp(ScratchProgram.Operation.Set, new ScratchValue(name));
						break;

					case ScopeToken.Assign:
						lexer.NextToken();
						CompileExpr(w, lexer);
						w.AddOp(ScratchProgram.Operation.SetLocal, new ScratchValue(name));
						break;

					case ScopeToken.LParen:
						lexer.NextToken();
						int argCount = 0;
						while (lexer.IsNot(ScopeToken.Eof, ScopeToken.RParen))
						{
							++argCount;
							CompileExpr(w, lexer);
							if (lexer.CurrToken == ScopeToken.Comma)
								lexer.NextToken();
							else
								break;
						}
						lexer.Eat(ScopeToken.RParen);
						w.AddOp(ScratchProgram.Operation.Push, new ScratchValue(argCount));
						w.AddOp(ScratchProgram.Operation.Call, new ScratchValue(name));
						break;

					default:
						w.AddOp(ScratchProgram.Operation.Get, new ScratchValue(name));
						break;
				}
			}
			else
			{
				w.AddOp(ScratchProgram.Operation.Push, ParseLiteral(lexer));
			}
		}
	}

	enum ScopeToken
	{
		Eof,
		String,
		Ident,
		Int32,
		Eq,
		Comma,
		LParen,
		RParen,
		LBrace,
		RBrace,
		Bar,
		Assign,
	}

	class ScopeLexer
	{
		private int _currPos;
		private int _lineNum = 1;

		public string Source { get; }
		public ScopeToken CurrToken { get; private set; }
		public string StringValue { get; private set; }
		public int Int32Value { get; private set; }

		public ScopeLexer(string source)
		{
			Source = source;
			// deliberately not NextToken() here
		}

		public void SkipPastEol()
		{
			while (_currPos < Source.Length && !TrySkipEndOfLine(Source[_currPos++]))
				/* loop */;
		}

		private bool TrySkipEndOfLine(char ch)
		{
			switch (ch)
			{
				case '\r':
					if (_currPos < Source.Length && Source[_currPos] == '\n')
						++_currPos;
					++_lineNum;
					return true;

				case '\n':
					if (_currPos < Source.Length && Source[_currPos] == '\r')
						++_currPos;
					++_lineNum;
					return true;
			}
			return false;
		}

		public void NextToken()
		{
			CurrToken = Scan();
		}

		public void Eat(ScopeToken token)
		{
			Expect(token);
			NextToken();
		}

		public void Expect(ScopeToken token)
		{
			if (CurrToken != token)
				throw Error($"Expected: {token}, got {CurrToken}");
		}

		public bool IsNot(params ScopeToken[] tokens)
		{
			return Array.IndexOf(tokens, CurrToken) < 0;
		}

		public void ExpectEither(ScopeToken thisToken, ScopeToken thatToken)
		{
			if (CurrToken != thisToken && CurrToken != thatToken)
				throw Error($"Expected: {thisToken} or {thatToken}, got {CurrToken}");
		}

		internal ArgumentException Error(string message)
		{
			return new ArgumentException($"Line {_lineNum}: {message}");
		}

		private string ScanString(char type)
		{
			int start = _currPos;
			int startLine = _lineNum;
			while (_currPos < Source.Length)
			{
				char ch = Source[_currPos++];

				if (ch == type)
					return Source.Substring(start, _currPos - start - 1);

				// we're gonna permit newlines in strings
				// it'll make things easier for big blobs of text
				// still need to detect them for the line numbers though
				TrySkipEndOfLine(ch);
			}
			throw new ArgumentException($"End of file in string started on line {startLine}");
		}

		private int ScanInt32()
		{
			int start = _currPos - 1;
			while (_currPos < Source.Length)
				if (char.IsDigit(Source[_currPos]))
					++_currPos;
				else
					break;
			return Int32.Parse(Source.Substring(start, _currPos - start));
		}

		private string ScanIdent()
		{
			// identifier syntax includes '-'
			// [a-z_][A-Z0-9_-]*
			int start = _currPos - 1;
			while (_currPos < Source.Length)
			{
				char ch = Source[_currPos];
				if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
					++_currPos;
				else
					break;
			}
			return Source.Substring(start, _currPos - start);
		}

		private ScopeToken Scan()
		{
			// this value never actually used
			// _currPos < Source.Length => it's a char from source
			// otherwise we return early
			char ch = '\0';

			// skip whitespace
			while (_currPos < Source.Length)
			{
				ch = Source[_currPos++];

				if (TrySkipEndOfLine(ch) || char.IsWhiteSpace(ch))
					continue;

				switch (ch)
				{
					case '/':
						if (_currPos < Source.Length && Source[_currPos] == '/')
						{
							++_currPos;
							SkipPastEol();
						}
						continue;

					case '#':
						SkipPastEol();
						continue;
				}

				break;
			}

			if (_currPos == Source.Length)
				return ScopeToken.Eof;

			// determine token type
			switch (ch)
			{
				case '(':
					return ScopeToken.LParen;
				case ')':
					return ScopeToken.RParen;
				case '{':
					return ScopeToken.LBrace;
				case '}':
					return ScopeToken.RBrace;
				case ',':
					return ScopeToken.Comma;
				case '=':
					return ScopeToken.Eq;
				case '|':
					return ScopeToken.Bar;

				case ':':
					if (_currPos < Source.Length && Source[_currPos] == '=')
					{
						++_currPos;
						return ScopeToken.Assign;
					}
					throw new ArgumentException("Unexpected ':', did you mean ':='");

				case '\'':
				case '"':
					StringValue = ScanString(ch);
					return ScopeToken.String;
			}

			if (char.IsDigit(ch))
			{
				Int32Value = ScanInt32();
				return ScopeToken.Int32;
			}

			if (char.IsLetter(ch) || ch == '_' || ch == '-')
			{
				StringValue = ScanIdent();
				return ScopeToken.Ident;
			}

			throw new ArgumentException($"Unexpected token character: '{ch}' on line {_lineNum}");
		}
	}
}
