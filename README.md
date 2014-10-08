win32-whereis
=============

Unix's has had it's `whereis` tool for decades while Windows users are stuck with `where`. Window's `where` is limited to executable files, while Unix's `whereis` can also find libraries and almost any other file.

`win32-whereis` is an alternative or replacement for Windows's `where` and provides more functionality then it's default `where`.

`win32-whereis` uses the following methods to find files:

* Order of precedence in executable look-up: http://support2.microsoft.com/kb/35284
* Dynamic-Link library search order: http://msdn.microsoft.com/en-us/library/windows/desktop/ms682586(v=vs.85).aspx

also, `win32-whereis` does not stop when a file was found, instead it continues it's search and displays a full list of paths that the file was found in the right order.
