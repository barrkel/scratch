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
			return ((ScratchAction)_value)(controller, view, args);
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
		private int _lineNum;

		public string Source { get; }
		public ScopeToken CurrToken { get; private set; }
		public string StringValue { get; private set; }
		public int Int32Value { get; private set; }

		public ScopeLexer(string source)
		{
			Source = source;
			NextToken();
		}

		private void SkipPastEol()
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
				throw Error("Expected: " + token);
		}

		public bool IsNot(params ScopeToken[] tokens)
		{
			return Array.IndexOf(tokens, CurrToken) < 0;
		}

		public void ExpectEither(ScopeToken thisToken, ScopeToken thatToken)
		{
			if (CurrToken != thisToken && CurrToken != thatToken)
				throw Error($"Expected: {thisToken} or ${thatToken}");
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
			return Int32.Parse(Source.Substring(start, _currPos - start - 1));
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
			return Source.Substring(start, _currPos - start - 1);
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
