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
		Action
	}

	public delegate ScratchValue ScratchAction(ScratchBookController controller, IScratchBookView view, IList<ScratchValue> args);

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

				default:
					throw new ArgumentException("Invalid type: " + value);
			}
		}

		public ScratchType Type { get; }
		public String StringValue => (string)_value;
		public int Int32Value => (int)_value;

		public ScratchValue Invoke(ScratchBookController controller, IScratchBookView view, IList<ScratchValue> args)
		{
			return ScratchValue.FromObject(((ScratchAction)_value)(controller, view, args));
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
			ConfigFileLibrary result = new ConfigFileLibrary(name);

			try
			{
				ScopeLexer lexer = new ScopeLexer(source);

				// skip first line (title)
				lexer.SkipPastEol();
				lexer.NextToken();

				while (lexer.CurrToken != ScopeToken.Eof)
				{
					// setting ::= (<ident> | <string>) '=' value ;
					lexer.ExpectEither(ScopeToken.Ident, ScopeToken.String);
					string ident = lexer.StringValue;
					lexer.NextToken();
					lexer.Eat(ScopeToken.Eq);
					var value = ParseValue(lexer);
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

		private static ScratchValue ParseValue(ScopeLexer lexer)
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
					}
					lexer.Eat(ScopeToken.RBrace);
					return new ScratchValue((controller, view, _) =>
					{
						try
						{
							ScratchValue r = ScratchValue.Null;
							foreach (var (n, a) in calls)
								r = controller.Scope.Lookup(n).Invoke(controller, view, a);
							return r;
						}
						catch (Exception ex)
						{
							Console.Error.WriteLine("call blew up: " + ex.Message);
							return ScratchValue.Null;
						}
					});
			}

			throw lexer.Error($"Expected: string, int or {{, got {lexer.CurrToken}");
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
			while (_currPos < Source.Length)
			{
				char ch = Source[_currPos++];
				switch (ch)
				{
					case '\r':
						if (_currPos < Source.Length && Source[_currPos] == '\n')
							++_currPos;
						++_lineNum;
						return;

					case '\n':
						if (_currPos < Source.Length && Source[_currPos] == '\r')
							++_currPos;
						++_lineNum;
						return;
				}
			}
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
				if (ch == '\n')
				{
					if (_currPos < Source.Length && Source[_currPos] == '\r')
						++_currPos;
					++_lineNum;
				}
				else if (ch == '\r')
				{
					if (_currPos < Source.Length && Source[_currPos] == '\n')
						++_currPos;
					++_lineNum;
				}
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

				if (char.IsWhiteSpace(ch))
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
