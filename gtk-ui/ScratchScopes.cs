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
	public interface IScratchLibrary : IEnumerable<(string, ScratchValue)>
	{
		bool TryLookup(string name, out ScratchValue result);
		string Name { get; }
	}

	public abstract class ScratchLibrary : IScratchLibrary
	{
		Dictionary<string, ScratchValue> _bindings = new Dictionary<string, ScratchValue>();

		protected ScratchLibrary(string name)
		{
			Name = name;
			foreach (var member in GetType().GetMembers())
			{
				if (member.MemberType != MemberTypes.Method)
					continue;
				MethodInfo method = (MethodInfo)member;
				foreach (ActionAttribute attr in Attribute.GetCustomAttributes(member, typeof(ActionAttribute)))
				{
					_bindings.Add(attr.Name, new ScratchValue((controller, view, args) =>
						(ScratchValue)method.Invoke(this, new object[] { view, args })));
				}
			}
		}

		public string Name { get; }

		protected void Bind(string name, ScratchValue value)
		{
			_bindings[name] = value;
		}

		protected void BindToAction(string name, string actionName)
		{
			Bind(name, MakeInvoke(actionName));
		}

		protected ScratchValue MakeInvoke(string name)
		{
			return new ScratchValue((controller, view, _) =>
					controller.Scope.Lookup(name).Invoke(controller, view, ScratchValue.EmptyList));
		}

		public IEnumerator<(string, ScratchValue)> GetEnumerator()
		{
			foreach (var entry in _bindings)
				yield return (entry.Key, entry.Value);
		}

		public bool TryLookup(string name, out ScratchValue result)
		{
			return _bindings.TryGetValue(name, out result);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	public class ScratchScope : IScratchLibrary
	{
		Dictionary<string, ScratchValue> _bindings = new Dictionary<string, ScratchValue>();

		public ScratchScope()
		{
			Name = "root";
		}

		public ScratchScope(ScratchScope parent)
		{
			Parent = parent;
			Name = "{}";
		}

		public string Name { get; }

		// Additively load bindings from enumerable source.
		public void Load(IScratchLibrary library)
		{
			foreach (var (name, value) in library)
				_bindings[name] = value;
		}

		// Additively load bindings from text.
		public void Load(string source)
		{
			// What grammar?
			// I want simple key-value for simple settings.
			// I don't really want top-level interpretation at load time.
			// I want any language of actions to be very minimal, and use symbolic actions for computation.
			// I don't really want to write an sexpr reader but that's the level of minimality aimed for.

			// How about this:
			//   file ::= { setting } .
			//   setting ::= (<ident> | <string>) '=' value ;
			//   value ::= <string> | <number> | '{' { call } '}' ;
			//   call ::= <ident> '(' value { ',' value } ')' ;

			// Part of the idea is to discourage language complexity.
			// There's no control flow here, for now; not even if, no lazy evaluation.
			// No way to define function arguments.
			// We'll use '#' and '//' for comments

			ScopeLexer lexer = new ScopeLexer(source);

			while (lexer.CurrToken != ScopeToken.Eof)
			{
				// setting ::= (<ident> | <string>) '=' value ;
				lexer.ExpectEither(ScopeToken.Ident, ScopeToken.String);
				string name = lexer.StringValue;
				lexer.NextToken();
				_bindings.Add(name, ParseValue(lexer));
			}
		}

		private ScratchValue ParseValue(ScopeLexer lexer)
		{
			ScratchValue result;
			// value ::= <string> | <number> | '{' { call } '}' ;
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
					lexer.NextToken();
					// call ::= <ident> '(' value { ',' value } ')' ;
					var calls = new List<(string, List<ScratchValue>)>();
					while (lexer.CurrToken == ScopeToken.Ident)
					{
						string name = lexer.StringValue;
						lexer.NextToken();
						lexer.Eat(ScopeToken.LParen);
						var args = new List<ScratchValue>();
						while (lexer.IsNot(ScopeToken.Eof, ScopeToken.RParen))
						{
							args.Add(ParseValue(lexer));
							if (lexer.CurrToken == ScopeToken.Comma)
								lexer.NextToken();
							else
								break;
						}
						lexer.Eat(ScopeToken.RParen);
						calls.Add((name, args));
						// create a lambda with late-bound lookups for all symbols
						// TODO: one day consider using another scope to store argument bindings 
						// (which needs argument syntax)
						return new ScratchValue((controller, view, _) =>
						{
							ScratchValue r = ScratchValue.Null;
							foreach (var (n, a) in calls)
								r = controller.Scope.Lookup(n).Invoke(controller, view, a);
							return r;
						});
					}
					break;
			}

			throw lexer.Error("Expected: string, int or {");
		}

		public ScratchScope Parent { get; }

		public bool TryLookup(string name, out ScratchValue result)
		{
			for (ScratchScope scope = this; scope != null; scope = scope.Parent)
				if (scope._bindings.TryGetValue(name, out result))
					return true;
			result = null;
			return false;
		}

		public ScratchValue Lookup(string name)
		{
			if (TryLookup(name, out ScratchValue result))
				return result;
			throw new ArgumentException("name not found in scope: " + name);
		}

		public IEnumerator<(string, ScratchValue)> GetEnumerator()
		{
			foreach (var entry in _bindings)
				yield return (entry.Key, entry.Value);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
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
}
