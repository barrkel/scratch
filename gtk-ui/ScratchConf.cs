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
		public static readonly long MaxBackEdges = 50_000;
		public static readonly int MaxExecutionDepth = 1000;

		private class GlobalContext
		{
			public GlobalContext(ScratchBookController controller, IScratchBookView view)
			{
				Controller = controller;
				View = view;
				StartTime = DateTime.UtcNow;
			}

			public ScratchBookController Controller { get; }
			public IScratchBookView View { get; }
			public DateTime StartTime { get; }

			// Watchdog on runaway script execution is based on back edges.
			// Loops that reduce the value of the instruction pointer, and function returns
			// are considered back edges. It's not perfect but trying to calculate execution time
			// is awkward when we expect modal dialogs to be triggered from scripts.
			public long BackEdges;
		}

		public ExecutionContext(ScratchBookController controller, IScratchBookView view, ScratchScope scope)
		{
			Context = new GlobalContext(controller, view);
			Scope = scope;
			Depth = 0;
		}

		private ExecutionContext(ExecutionContext parent, ScratchScope childScope)
		{
			Context = parent.Context;
			Scope = childScope;
			Depth = parent.Depth + 1;
			if (Depth > MaxExecutionDepth)
				throw new InvalidOperationException("Execution stack too deep");
		}

		public ExecutionContext CreateChild()
		{
			return new ExecutionContext(this, new ScratchScope(Scope));
		}

		private GlobalContext Context { get; }
		public ScratchBookController Controller => Context.Controller;
		public IScratchBookView View => Context.View;
		public ScratchScope Scope { get; }
		public int Depth { get; }

		public void AddBackEdge()
		{
			++Context.BackEdges;
			if (Context.BackEdges > MaxBackEdges)
				throw new InvalidOperationException("Too much execution, too many back edges.");
		}
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
		public static readonly ScratchValue True = new ScratchValue("true");
		public static readonly ScratchValue False = Null;
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

		public static ScratchValue From(bool value)
		{
			return value ? True : False;
		}

		public static ScratchValue From(string value)
		{
			return new ScratchValue(value);
		}

		public static ScratchValue From(int value)
		{
			return new ScratchValue(value);
		}

		public static ScratchValue From(object value)
		{
			switch (value)
			{
				case null:
					return Null;

				case bool boolValue:
					return From(boolValue);

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
		public bool IsTrue => Type != ScratchType.Null;
		public bool IsFalse => Type == ScratchType.Null;
		public object ObjectValue => _value;

		public ScratchValue Invoke(ExecutionContext context, params ScratchValue[] args)
		{
			return Invoke(context, (IList<ScratchValue>)args);
		}

		public ScratchValue Invoke(ExecutionContext context, IList<ScratchValue> args)
		{
			switch (Type)
			{
				case ScratchType.Action:
					return ScratchValue.From(((ScratchAction)_value)(context, args));

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

		public static IList<ScratchValue> List(params object[] values)
		{
			ScratchValue[] result = new ScratchValue[values.Length];
			for (int i = 0; i < values.Length; ++i)
				result[i] = From(values[i]);
			return result;
		}
	}

	// Basic stack machine straight from parser.
	public class ScratchProgram
	{
		public static readonly bool DebugStack = true;

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
			// Pop stack, jump if null
			JumpIfNull,
			// Pop stack, jump if not null
			JumpIfNotNull,
			// Unconditional jump
			Jump,
			// Boolean not
			Not,
			Dup,
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

		public struct Label
		{
			public Label(string value)
			{
				Value = value;
			}

			public string Value { get; }
		}

		public class Writer
		{
			Dictionary<string, int> _labels = new Dictionary<string, int>();
			List<Op> _ops = new List<Op>();
			List<int> _fixups = new List<int>();
			List<Loop> _loops = new List<Loop>();

			public class Loop
			{
				public Label Break;
				public Label Continue;
			}

			public Label NewLabel(string prefix)
			{
				string result = $"{prefix}{_labels.Count}";
				_labels[result] = -1;
				return new Label(result);
			}

			public void ResolveLabel(Label label)
			{
				_labels[label.Value] = _ops.Count;
			}

			public void EnterLoop(Label breakLabel, Label continueLabel)
			{
				_loops.Add(new Loop() { Break = breakLabel, Continue = continueLabel });
			}

			public void ExitLoop()
			{
				_loops.RemoveAt(_loops.Count - 1);
			}

			public Loop CurrentLoop
			{
				get
				{
					if (_loops.Count == 0)
						return null;
					return _loops[_loops.Count - 1];
				}
			}

			public void AddOp(Operation op)
			{
				_ops.Add(new Op(op));
			}

			public void AddOpWithLabel(Operation op, Label label)
			{
				_fixups.Add(_ops.Count);
				_ops.Add(new Op(op, new ScratchValue(label.Value)));
			}

			public void AddOp(Operation op, ScratchValue value)
			{
				_ops.Add(new Op(op, value));
			}

			public ScratchProgram ToProgram()
			{
				Console.WriteLine(this);
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

			public override string ToString()
			{
				StringBuilder result = new StringBuilder();
				Dictionary<int, List<string>> labels = new Dictionary<int, List<string>>();
				foreach (var entry in _labels)
					if (labels.TryGetValue(entry.Value, out var names))
						names.Add(entry.Key);
					else
						labels.Add(entry.Value, new List<string>() { entry.Key });
				for (int i = 0; i < _ops.Count; ++i)
				{
					if (labels.TryGetValue(i, out var names))
						foreach (string name in names)
							result.AppendLine($"{name}:");
					result.AppendLine($"  {_ops[i]}");
				}
				return result.ToString();
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
				if (DebugStack)
				{
					Console.WriteLine("  stack: {0}", string.Join(", ", stack));
					Console.WriteLine(_ops[cp]);
				}
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
						context.AddBackEdge();
						return Pop(stack);

					case Operation.Jump:
						ip = _ops[cp].Arg.Int32Value;
						break;

					case Operation.JumpIfNull:
						if (Pop(stack).IsFalse)
							ip = _ops[cp].Arg.Int32Value;
						break;

					case Operation.JumpIfNotNull:
						if (Pop(stack).IsTrue)
							ip = _ops[cp].Arg.Int32Value;
						break;

					case Operation.Set:
						context.Scope.Assign(_ops[cp].ArgAsString, Peek(stack));
						break;

					case Operation.SetLocal:
						context.Scope.AssignLocal(_ops[cp].ArgAsString, Peek(stack));
						break;

					case Operation.Call:
						stack.Add(context.Scope.Lookup(_ops[cp].ArgAsString).Invoke(context, PopArgList(stack)));
						break;

					case Operation.Not:
						if (Pop(stack).IsFalse)
							stack.Add(ScratchValue.True);
						else
							stack.Add(ScratchValue.False);
						break;

					case Operation.Dup:
						stack.Add(Peek(stack));
						break;
				}
				if (ip <= cp)
					context.AddBackEdge();
			}

			context.AddBackEdge();
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
			// DONE: implement argument binding for blocks so we can write functions
			// TODO: extend ScratchValue with basic syntax tree, mainly for easier introspection of bindings
			// TODO: rewrite as many actions as possible in ScratchConf to get a feel for limits
			// TODO: accept identifier syntax in more contexts if not ambiguous
			// TODO: allow binding keys to functions directly, turns out it's tedious to have to name everything

			/*
			   Grammar:
				  file ::= { setting } .
				  setting ::= (<ident> | <string>) '=' ( literal | <ident> ) ;
				  literal ::= <string> | <number> | block | 'nil' ;
				  block ::= '{' [paramList] exprList '}' ;
				  paramList ::= '|' [ <ident> { ',' <ident> } ] '|' ;
				  expr ::= if | orExpr | return | while | 'break' | 'continue' ;
				  while ::= 'while' orExpr '{' exprList '}' ;
				  return ::= 'return' [ '(' expr ')' ] ;
				  orExpr ::= andExpr { '||' andExpr } ;
				  andExpr ::= factor { '&&' factor } ;
				  factor ::= [ 'not' ] callOrAssign | literal | '(' expr ')' ;
				  callOrAssign ::= <ident>
					( ['(' [ expr { ',' expr } ] ')']
					| '=' expr
					| ':=' expr
					| 
					) ;
				  exprList = { expr } ;
				  if ::= 
					'if' orExpr '{' exprList '}'
					[ 'else' (if | '{' exprList '}') ]
					;
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
					// setting ::= (<ident> | <string>) '=' ( literal | <ident> ) ;
					lexer.ExpectEither(ScopeToken.Ident, ScopeToken.String);
					string ident = lexer.StringValue;
					lexer.NextToken();
					lexer.Eat(ScopeToken.Eq);
					var value = ParseSettingValue(lexer);
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

		private static ScratchValue ParseSettingValue(ScopeLexer lexer)
		{
			if (lexer.CurrToken == ScopeToken.Ident)
			{
				var result = new ScratchValue(lexer.StringValue);
				lexer.NextToken();
				return result;
			}
			return ParseLiteral(lexer);
		}

		private static ScratchValue ParseLiteral(ScopeLexer lexer)
		{
			ScratchValue result;
			// literal ::= <string> | <number> | block | 'nil' ;
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

				case ScopeToken.Nil:
					result = ScratchValue.Null;
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
			// block ::= '{' [paramList] exprList '}' ;
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

			CompileExprList(w, lexer);

			w.AddOp(ScratchProgram.Operation.Ret);
			return new ScratchFunction(w.ToProgram(), paramList);
		}

		private static void CompileExprList(ScratchProgram.Writer w, ScopeLexer lexer, 
			ScopeToken stopToken = ScopeToken.RBrace)
		{
			// Invariant: we enter without a value on the stack, and we always leave with one.
			bool prevRetValOnStack = false;
			while (lexer.IsNot(ScopeToken.Eof, stopToken))
			{
				if (prevRetValOnStack)
					w.AddOp(ScratchProgram.Operation.Pop);
				CompileExpr(w, lexer);
				prevRetValOnStack = true;
			}
			lexer.Eat(stopToken);
			if (!prevRetValOnStack)
				w.AddOp(ScratchProgram.Operation.Push, ScratchValue.Null);
		}

		private static void CompileExpr(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			// expr ::= if | orExpr | return | while | 'break' | 'continue' ;
			switch (lexer.CurrToken)
			{
				case ScopeToken.Break:
				{
					lexer.NextToken();
					var loop = w.CurrentLoop;
					if (loop == null)
						throw lexer.Error("break used outside loop");
					// TODO: consider taking arg like return, result of broken loop
					w.AddOp(ScratchProgram.Operation.Push, ScratchValue.Null);
					w.AddOpWithLabel(ScratchProgram.Operation.Jump, loop.Break);
					break;
				}

				case ScopeToken.Continue:
				{
					lexer.NextToken();
					var loop = w.CurrentLoop;
					if (loop == null)
						throw lexer.Error("continue used outside loop");
					// TODO: consider taking arg like return, result of continued loop that fails predicate
					w.AddOp(ScratchProgram.Operation.Push, ScratchValue.Null);
					w.AddOpWithLabel(ScratchProgram.Operation.Jump, loop.Continue);
					break;
				}

				case ScopeToken.If:
					CompileIf(w, lexer);
					break;

				case ScopeToken.While:
					CompileWhile(w, lexer);
					break;

				case ScopeToken.Return:
					// return ::= 'return' [ '(' orExpr ')' ] ;
					lexer.NextToken();
					if (lexer.CurrToken == ScopeToken.LParen)
						CompileOrExpr(w, lexer);
					else
						w.AddOp(ScratchProgram.Operation.Push, ScratchValue.Null);
					w.AddOp(ScratchProgram.Operation.Ret);
					break;

				default:
					CompileOrExpr(w, lexer);
					break;
			}
		}

		private static void CompileOrExpr(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			// orExpr ::= andExpr { '||' andExpr } ;
			CompileAndExpr(w, lexer);
			var ifTrue = w.NewLabel("ifTrue");
			bool needLabel = false;
			while (lexer.SkipIf(ScopeToken.Or))
			{
				w.AddOp(ScratchProgram.Operation.Dup);
				// short circuit ||
				w.AddOpWithLabel(ScratchProgram.Operation.JumpIfNotNull, ifTrue);
				needLabel = true;
				w.AddOp(ScratchProgram.Operation.Pop);
				CompileAndExpr(w, lexer);
			}
			if (needLabel)
				w.ResolveLabel(ifTrue);
		}

		private static void CompileAndExpr(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			// andExpr ::= factor { '&&' factor } ;
			CompileFactor(w, lexer);
			// These labels can get really verbose when dumping opcodes
			var ifFalse = w.NewLabel("ifFalse");
			bool needLabel = false;
			while (lexer.SkipIf(ScopeToken.And))
			{
				w.AddOp(ScratchProgram.Operation.Dup);
				// short circuit &&
				w.AddOpWithLabel(ScratchProgram.Operation.JumpIfNull, ifFalse);
				needLabel = true;
				w.AddOp(ScratchProgram.Operation.Pop);
				CompileFactor(w, lexer);
			}
			if (needLabel)
				w.ResolveLabel(ifFalse);
		}

		private static void CompileFactor(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			// factor ::= [ '!' ] callOrAssign | literal | '(' expr ')' ;
			bool not = lexer.SkipIf(ScopeToken.Not);

			switch (lexer.CurrToken)
			{
				case ScopeToken.Ident:
					CompileCallOrAssign(w, lexer);
					break;

				case ScopeToken.LParen:
					lexer.NextToken();
					CompileExpr(w, lexer);
					lexer.Eat(ScopeToken.RParen);
					break;

				default:
					w.AddOp(ScratchProgram.Operation.Push, ParseLiteral(lexer));
					break;
			}

			if (not)
				w.AddOp(ScratchProgram.Operation.Not);
		}

		private static void CompileCallOrAssign(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			//  callOrAssign ::= <ident>
			//    ( ['(' { expr } ')']  // invoke binding
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

		private static void CompileWhile(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			// while ::= 'while' orExpr '{' exprList '}';
			lexer.NextToken();
			var topOfLoop = w.NewLabel("top");
			var pastLoop = w.NewLabel("pastLoop");
			// this is 'result' of loop if we never execute body
			w.AddOp(ScratchProgram.Operation.Push, ScratchValue.Null);
			// we have at least a null on the stack
			w.ResolveLabel(topOfLoop);
			CompileOrExpr(w, lexer);
			w.AddOpWithLabel(ScratchProgram.Operation.JumpIfNull, pastLoop);
			// pop off either the null above, or the result of previous iteration
			w.AddOp(ScratchProgram.Operation.Pop);
			lexer.Eat(ScopeToken.LBrace);
			w.EnterLoop(pastLoop, topOfLoop);
			// We have nothing on the stack
			CompileExprList(w, lexer);
			// we have at least a null on the stack
			w.ExitLoop();
			w.AddOpWithLabel(ScratchProgram.Operation.Jump, topOfLoop);
			w.ResolveLabel(pastLoop);
			// top of stack is now either null or last expr evaluated in body
		}

		private static void CompileIf(ScratchProgram.Writer w, ScopeLexer lexer)
		{
			/*
			  if ::= 
				'if' orExpr '{' exprList '}'
				[ 'else' (if | '{' exprList '}') ]
				;
			 */
			var elseCase = w.NewLabel("else");
			var afterIf = w.NewLabel("afterIf");
			lexer.NextToken();
			CompileOrExpr(w, lexer);
			lexer.Eat(ScopeToken.LBrace);
			w.AddOpWithLabel(ScratchProgram.Operation.JumpIfNull, elseCase);
			CompileExprList(w, lexer);
			w.AddOpWithLabel(ScratchProgram.Operation.Jump, afterIf);
			while (true)
			{
				w.ResolveLabel(elseCase);
				if (lexer.CurrToken == ScopeToken.Else)
				{
					lexer.NextToken();

					if (lexer.CurrToken == ScopeToken.If)
					{
						elseCase = w.NewLabel("else");
						lexer.NextToken();
						CompileExpr(w, lexer);
						lexer.Eat(ScopeToken.LBrace);
						w.AddOpWithLabel(ScratchProgram.Operation.JumpIfNull, elseCase);
						CompileExprList(w, lexer);
						w.AddOpWithLabel(ScratchProgram.Operation.Jump, afterIf);
					}
					else
					{
						lexer.Eat(ScopeToken.LBrace);
						CompileExprList(w, lexer);
						break;
					}
				}
				else
				{
					// we need to have a value from the else branch even if it's missing
					w.AddOp(ScratchProgram.Operation.Push, ScratchValue.Null);
					break;
				}
			}
			w.ResolveLabel(afterIf);
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
		If,
		Else,
		And,
		Or,
		Not,
		Nil,
		Return,
		While,
		Break,
		Continue,
	}

	class ScopeLexer
	{
		private static readonly Dictionary<string, ScopeToken> Keywords = CreateKeywordDictionary();

		private int _currPos;
		private int _lineNum = 1;

		public string Source { get; }
		public ScopeToken CurrToken { get; private set; }
		public string StringValue { get; private set; }
		public int Int32Value { get; private set; }
		public int LineNum => _lineNum;

		public ScopeLexer(string source)
		{
			Source = source;
			// deliberately not NextToken() here
		}

		private static Dictionary<string,ScopeToken> CreateKeywordDictionary()
		{
			var result = new Dictionary<string, ScopeToken>();
			result.Add("if", ScopeToken.If);
			result.Add("while", ScopeToken.While);
			result.Add("break", ScopeToken.Break);
			result.Add("continue", ScopeToken.Continue);
			result.Add("else", ScopeToken.Else);
			result.Add("nil", ScopeToken.Nil);
			result.Add("return", ScopeToken.Return);
			return result;
		}

		public bool SkipIf(ScopeToken token)
		{
			if (CurrToken == token)
			{
				NextToken();
				return true;
			}
			return false;
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
			return int.Parse(Source.Substring(start, _currPos - start));
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

		private bool SkipIfNextChar(char ch)
		{
			if (_currPos < Source.Length && Source[_currPos] == ch)
			{
				++_currPos;
				return true;
			}
			return false;
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
						if (!SkipIfNextChar('/'))
							break;
						SkipPastEol();
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

				case '!':
					return ScopeToken.Not;

				case '|':
					if (SkipIfNextChar('|'))
						return ScopeToken.Or;
					return ScopeToken.Bar;

				case ':':
					if (SkipIfNextChar('='))
						return ScopeToken.Assign;
					throw Error("Unexpected ':', did you mean ':='");

				case '&':
					if (SkipIfNextChar('&'))
						return ScopeToken.And;
					throw Error("Unexpected '&', did you mean '&&'");

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
				if (Keywords.TryGetValue(StringValue, out var keyword))
					return keyword;
				return ScopeToken.Ident;
			}

			throw new ArgumentException($"Unexpected token character: '{ch}' on line {_lineNum}");
		}
	}
}
