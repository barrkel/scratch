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

	public abstract class ScratchLibraryBase : IScratchLibrary
	{
		protected ScratchLibraryBase(string name)
		{
			Name = name;
		}

		protected Dictionary<string, ScratchValue> Bindings { get; } = new Dictionary<string, ScratchValue>();

		public string Name { get; }

		public IEnumerator<(string, ScratchValue)> GetEnumerator()
		{
			foreach (var entry in Bindings)
				yield return (entry.Key, entry.Value);
		}

		public bool TryLookup(string name, out ScratchValue result)
		{
			return Bindings.TryGetValue(name, out result);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	delegate void ScratchActionVoid(ExecutionContext context, IList<ScratchValue> args);

	public abstract class NativeLibrary : ScratchLibraryBase
	{
		protected NativeLibrary(string name) : base(name)
		{
			foreach (var member in GetType().GetMembers())
			{
				if (member.MemberType != MemberTypes.Method)
					continue;
				MethodInfo method = (MethodInfo)member;
				foreach (ActionAttribute attr in Attribute.GetCustomAttributes(member, typeof(ActionAttribute)))
				{
					ScratchAction action;
					try
					{
						if (method.ReturnType == typeof(void))
						{
							ScratchActionVoid voidAction =
								(ScratchActionVoid)Delegate.CreateDelegate(typeof(ScratchActionVoid), this, method, true);
							action = (context, args) =>
							{
								voidAction(context, args);
								return ScratchValue.Null;
							};
						}
						else
						{
							action = (ScratchAction)Delegate.CreateDelegate(typeof(ScratchAction), this, method, true);
						}
						Bindings.Add(attr.Name, new ScratchValue(action));
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error binding {0}: {1}", method.Name, ex.Message);
					}
				}
			}
		}

		protected void Bind(string name, ScratchValue value)
		{
			Bindings[name] = value;
		}

		protected void BindToAction(string name, string actionName)
		{
			Bind(name, MakeInvoke(actionName));
		}

		protected ScratchValue MakeInvoke(string name)
		{
			return new ScratchValue((context, _) =>
					context.Scope.Lookup(name).Invoke(context, ScratchValue.EmptyList));
		}

		protected void ValidateLength(string method, IList<ScratchValue> args, int length)
		{
			if (args.Count != length)
				throw new ArgumentException($"Expected {length} arguments to {method} but got {args.Count}");
		}

		protected void Validate(string method, IList<ScratchValue> args, params ScratchType[] paramTypes)
        {
			ValidateLength(method, args, paramTypes.Length);
			for (int i = 0; i < args.Count; ++i)
				if (args[i].Type != paramTypes[i])
					throw new ArgumentException($"Expected arg {i + 1} to {method} to be {paramTypes[i]} but got {args[i].Type}");
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

		// We model assignment as updating a binding, rather than updating a box referenced by the binding.
		public void Assign(string name, ScratchValue value)
		{
			for (ScratchScope scope = this; scope != null; scope = scope.Parent)
				if (scope.TryAssign(name, value))
					return;
			AssignLocal(name, value);
		}

		public void AssignLocal(string name, ScratchValue value)
		{
			_bindings[name] = value;
		}

		public bool TryAssign(string name, ScratchValue value)
		{
			if (_bindings.ContainsKey(name))
			{
				_bindings[name] = value;
				return true;
			}
			return false;
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

	// TODO: implement arg type validator
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
