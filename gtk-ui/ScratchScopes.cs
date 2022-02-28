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
					Bindings.Add(attr.Name, new ScratchValue((controller, view, args) =>
						(ScratchValue)method.Invoke(this, new object[] { view, args })));
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
			return new ScratchValue((controller, view, _) =>
					controller.Scope.Lookup(name).Invoke(controller, view, ScratchValue.EmptyList));
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