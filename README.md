ScratchPad for Windows and Mono
===========

Simple keyboard-driven scratchpad application with two implemented UIs, one
WinForms, the other Gtk#.

Designed with constant persistence and unlimited history logging for
persistent undo across sessions.

Uses text-based storage and edit log to minimize risk of data loss. Text can
be recovered by replaying log in case the .txt is lost; if the .log alone is
lost, the most current text should still be OK. If the two get out of sync
(e.g. modifying the .txt file independently), the conflict is resolved by
replaying the log and and making the diff to the current text the final edit.

The diff algorithm that produces the edit log is naive and very simple, in
the interests of reducing code complexity and eliminating third-party
dependencies.

Navigation is expected to be done with the keyboard:

* Alt+Up/Down navigates history backwards and forwards.
* Alt+Left/Right navigates pages backwards and forwards.
* F12 for simple title search dialog
* Pages sorted in most recently edited order

Run the application with a directory argument. The default tab will use that
directory for storage. Any subdirectories found will be used as names for
additional tabs. All .txt files in directories will be added as pages; all
.log files will be replayed to recreate any .txt files if missing.
