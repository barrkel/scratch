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
	public enum ExitIntent
	{
		Exit,
		Restart
	}

	// Controller for behaviour. UI should receive this and send keystrokes and events to it, along with view callbacks.
	// The view should be updated via the callbacks.
	// Much of it is stringly typed for a dynamically bound future.
	public class ScratchRootController
	{
		Dictionary<ScratchBook, ScratchBookController> _controllerMap = new Dictionary<ScratchBook, ScratchBookController>();

		public ScratchRoot Root { get; }

		public Options Options => Root.Options;

		public ScratchScope RootScope { get; }

		public ExitIntent ExitIntent { get; private set; } = ExitIntent.Exit;

		public event EventHandler ExitHandler;

		public void Exit(ExitIntent intent)
		{
			ExitIntent = intent;
			ExitHandler(this, EventArgs.Empty);
		}

		public ScratchBookController GetControllerFor(ScratchBook book)
		{
			if (_controllerMap.TryGetValue(book, out var result))
			{
				return result;
			}
			result = new ScratchBookController(this, book);
			_controllerMap.Add(book, result);
			return result;
		}

		public ScratchRootController(ScratchRoot root)
		{
			Root = root;
			RootScope = (ScratchScope)root.RootScope;
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
		public ScratchRootController RootController { get; }
		public ScratchScope Scope { get; }

		public ScratchBookController(ScratchRootController rootController, ScratchBook book)
		{
			Book = book;
			RootController = rootController;
			Scope = (ScratchScope)book.Scope;
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

			if (Scope.GetOrDefault("debug-keys", false))
				Log.Out($"debug-keys: {key}");

			if (Scope.TryLookup(key, out var action))
			{
				try
				{
					action.Invoke(key, context, ScratchValue.EmptyList);
				}
				catch (Exception ex)
				{
					Log.Out(ex.Message);
				}
				return true;
			}

			return false;
		}

		public void InvokeAction(IScratchBookView view, string actionName, IList<ScratchValue> args)
		{
			ExecutionContext context = new ExecutionContext(this, view, Scope);
			if (Scope.TryLookup(actionName, out var action))
				action.Invoke(actionName, context, args);
		}

		public void InformEvent(IScratchBookView view, string eventName, IList<ScratchValue> args)
		{
			ExecutionContext context = new ExecutionContext(this, view, Scope);
			string eventMethod = $"on-{eventName}";
			if (Scope.TryLookup(eventMethod, out var action))
				action.Invoke(eventMethod, context, args);
		}
	}
}
