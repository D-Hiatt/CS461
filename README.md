# MultiGrep

Description:  This tool is designed to read a series of patterns followed by replacements in a file, and search for and replace all matches in any number of other files.  It is similiar to grep or sed, except that it searches for multiple pattern matches simeneously.  No data is ever compared more than once.  In order to achieve this, the program uses a 'trie' data structure consisting of all possible pattern matches.

Requirements:  Microsoft .NET 4.5+ or mono 4.2.1+

Build Instructions: On Linux, compile with mono using either 'xbuild MultiGrep/MultiGrep.csproj' or mcs 
