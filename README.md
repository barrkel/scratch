# ScratchPad for Windows and Mono

Simple keyboard-driven scratchpad application with a Gtk# UI that works
in Windows on .NET and Linux and macOS using Mono.

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

There's a simple configuration language, ScratchConf, for tweaking the UI
and encoding macro actions.

## ScratchConf

Config files are read from pages which start with .config or .globalconfig.
Settings from .config are scoped to a tab (book), while .globalconfig is
shared across all tabs.

The configuration language is designed to have no execution up front so that
startup stays fast. Only symbol bindings are allowed at the top level.
However, anonymous functions can be defined and functions which are bound
to the names of keys (using Emacs conventions for C- M- S- modifiers)
will be executed when that key (combination) is pressed.

Comments are line-oriented and introduced by # or //.

The language has no arithmetic, but the default imported library has functions
for add(), sub(), mul(), div(), eq(), ne(), lt(), gt() and so on.
The only literals are strings, non-negative integers and anonymous functions.
Identifiers may include '-'. Functions take parameters Ruby-style, with || inside {}.

The language uses dynamic scope. Function activation records are pushed on a
stack of symbol tables on function entry and popped on exit. This means that locals
defined in a function will temporarily hide globals for called functions.
This is similar to the behaviour of Emacs Lisp. Binding within nested anonymous
functions work as downward funargs, but the scope is not closed over - the bindings
are looked up in a new context if the nested function is stored and executed after
its inclosing activation record is popped (i.e. the function that created it returned).

There are two binding syntaxes inside function bodies, '=' and ':='.
The difference is that '=' will redefine an existing binding in the scope it's found
in if it already exists, while ':=' always creates a new binding.
In effect, always use ':=' inside function bodies unless you're trying to change
the value of a global.

There are two 'global' scopes: the root scope, where .globalconfig configs are loaded,
and '.config', where book scopes are loaded. The idea is you can customize settings
on a per-book basis where this makes sense, depending on what the book is used for.

### Grammar

```
<ident> =~ [a-zA-Z_-][a-z0-9_-]* ;
<string> =~ '[^']*'|"[^"]*" ;
<number> =~ [0-9]+ ;

file ::= { setting } .
setting ::= (<ident> | <string>) '=' ( literal | <ident> ) ;
literal ::= <string> | <number> | block | 'nil' ;
block ::= '{' [paramList] exprList '}' ;
paramList ::= '|' [ <ident> { ',' <ident> } ] '|' ;
exprList = { expr } ;
expr ::= if | orExpr | return | while | 'break' | 'continue' ;
while ::= 'while' orExpr '{' exprList '}' ;
// consider removing this ambiguity on the basis of semantics
// control will not flow to the next line, so what if we eat it
return ::= 'return' [ '(' orExpr ')' ] ;
orExpr ::= andExpr { '||' andExpr } ;
andExpr ::= factor { '&&' factor } ;
factor ::= [ '!' ] ( callOrAssign | literal | '(' expr ')' ) ;
callOrAssign ::= <ident>
  ( ['(' [ expr { ',' expr } ] ')']
  | '=' expr
  | ':=' expr
  |
  ) ;
if ::=
  'if' orExpr '{' exprList '}'
  [ 'else' (if | '{' exprList '}') ]
  ;
```

### Examples

#### Set preferences for font, background color
```
text-font = "Consolas, 9"
info-font = "Verdana, 12"
log-font = "Consolas, 8"
background-color = '#d1efcf'
```

#### Create a standalone read-only snippet window containing selected text when F1 is pressed

```
get-input-text = { |current|
  # get-input is a simple (and ugly) builtin text input dialog
  get-input({
    init-text := current
  })
}

show-snippet = { |snippet-text|
  # launch-snippet is a builtin function to create and show a new window
  # with read-only text
  # Its argument is an anonymous function whose bindings are used to
  # configure the window.
  launch-snippet({
    text := snippet-text
    snippet-color := '#FFFFC0'
  })
}

F1 = {
  sel := get-view-selected-text()
  if sel && ne(sel, '') {
    title := get-input-text("Snippet")
    launch-snippet({
      text := sel
      snippet-color := '#FFFFC0'
    })
  }
}
```

#### Replace text within a selected block of text using dialogs for search and replacement

```
replace-command = {
  sel := get-view-selected-text()
  if !sel || eq(sel, '') { return }
  foo := get-input-text('Regex')
  if !foo { return }
  dst := get-input-text('Replacement')
  if !dst { return }
  ensure-saved()
  set-view-selected-text(gsub(sel, foo, dst))
}
```

