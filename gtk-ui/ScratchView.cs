using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Barrkel.ScratchPad
{
	internal class PageViewState
	{
		// Exists for nicer references to the constructor.
		internal static PageViewState Create()
		{
			return new PageViewState();
		}

		public (int, int)? CurrentSelection { get; set; }
		public int? CurrentScrollPos { get; set; }
		// [start, end) delimits text inserted in a completion attempt
		public (int, int)? CurrentCompletion { get; set; }
	}

	public delegate IEnumerable<(string, T)> SearchFunc<T>(string text);

	public interface IScratchBookView
	{
		// Get the book this view is for; the view only ever shows a single page from a book
		ScratchBook Book { get; }

		// Inserts text at CurrentPosition
		void InsertText(string text);
		// Delete text backwards from CurrentPosition
		void DeleteTextBackwards(int count);
		// Gets 0-based position in text
		int CurrentPosition { get; set; }
		// Get or set the bounds of selected text; first is cursor, second is bound.
		(int, int) Selection { get; set; }

		// 0-based position in text of first character on visible line at top of view.
		// Assigning will attempt to set the scroll position so that this character is at the top.
		int ScrollPos { get; set; }
		// Ensure 0-based position in text is visible by scrolling if necessary.
		void ScrollIntoView(int position);

		// 0-based index of current Page in Book; may be equal to Book.Pages.Count for new page.
		int CurrentPageIndex { get; }
		// Current text in editor, which may be ahead of model (lazy saves).
		string CurrentText { get; }

		string Clipboard { get; }
		string SelectedText { get; set; }

		// View should call InvokeAction with actionName every millis milliseconds
		void AddRepeatingTimer(int millis, string actionName);

		void AddNewPage();

		// Show a search dialog with text box, list box and ok/cancel buttons.
		// List box is populated from result of searchFunc applied to contents of text box.
		// Returns true if OK clicked and item in list box selected, with associated T in result.
		// Returns false if Cancel clicked or search otherwise cancelled (Esc).
		bool RunSearch<T>(SearchFunc<T> searchFunc, out T result);

		// Show an input dialog.
		bool GetInput(ScratchScope settings, out string value);

		// Show a non-modal snippet window.
		void LaunchSnippet(ScratchScope settings);

		// Before invoking cross-page navigation, call this.
		void EnsureSaved();
		// Jump to page by index in Book.
		void JumpToPage(int page);
	}

}
