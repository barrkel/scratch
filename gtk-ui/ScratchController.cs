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
	// Controller for behaviour. UI should receive this and send keystrokes and events to it, along with view callbacks.
	// The view should be updated via the callbacks.
	// Much of it is stringly typed for a dynamically bound future.
	public class ScratchController
	{
		Dictionary<ScratchBook, ScratchBookController> _controllerMap = new Dictionary<ScratchBook, ScratchBookController>();

		public ScratchRoot Root { get; }

		public Options Options => Root.Options;

        public ScratchScope RootScope { get; } = new ScratchScope();

		public ScratchBookController GetControllerFor(ScratchBook book)
		{
			if (_controllerMap.TryGetValue(book, out var result))
			{
				return result;
			}
			result = new ScratchBookController(this, book);
			_controllerMap.Add(book, result);
			RootScope.Load(LegacyLibrary.Instance);
			return result;
		}

		public ScratchController(ScratchRoot root)
		{
			Root = root;
		}
	}

	public class ScratchBookController
	{
		// TODO: bullet mode, somewhat like auto indent; make tabs do similar things
		// TODO: keyword jump from hotkey, to create cross-page linking
		// TODO: read-only generated pages that e.g. collect sigil lines
		//   Canonical example: TODO: lines, or discussion items for people; ought to link back
		// TODO: search over log history
		// TODO: load key -> action bindings from a note
		// TODO: move top-level logic (e.g. jumping) to controller
		// TODO: lightweight scripting for composing new actions

		public ScratchBook Book { get; }
		public ScratchController RootController { get; }
		public ScratchScope Scope { get; }

		public ScratchBookController(ScratchController rootController, ScratchBook book)
		{
			Book = book;
			RootController = rootController;
			Scope = new ScratchScope(rootController.RootScope);
		}

		public void ConnectView(IScratchBookView view)
		{
			view.AddRepeatingTimer(3000, "check-for-save");
		}

		public bool InformKeyStroke(IScratchBookView view, string keyName, bool ctrl, bool alt, bool shift)
		{
			string ctrlPrefix = ctrl ? "C-" : "";
			string altPrefix = alt ? "M-" : "";
			// Convention: self-printing keys have a single character. Exceptions are Return and Space.
			// Within this convention, don't use S- if shift was pressed to access the key. M-A is M-S-a, but M-A is emacs style.
			string shiftPrefix = (keyName.Length > 1 && shift) ? "S-" : "";
			string key = string.Concat(ctrlPrefix, altPrefix, shiftPrefix, keyName);

			ExecutionContext context = new ExecutionContext(this, view, Scope);

			if (RootController.Options.DebugKeys)
				Console.WriteLine("debug-keys: {0}", key);

			if (Scope.TryLookup(key, out var action))
			{
				try
				{
					action.Invoke(context, ScratchValue.EmptyList);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine(ex.Message);
				}
				return true;
			}

			return false;
		}

		public void InvokeAction(IScratchBookView view, string actionName, IList<ScratchValue> args)
		{
			ExecutionContext context = new ExecutionContext(this, view, Scope);
			if (Scope.TryLookup(actionName, out var action))
				action.Invoke(context, args);
		}

		public void InformEvent(IScratchBookView view, string eventName, IList<ScratchValue> args)
		{
			ExecutionContext context = new ExecutionContext(this, view, Scope);
			if (Scope.TryLookup("on-" + eventName, out var action))
				action.Invoke(context, args);
		}
	}
}
