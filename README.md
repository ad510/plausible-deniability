decoherence
===========

Overview
--------
My eventual hope for this game is to make a multiplayer RTS in which both players can do things that would normally be considered "cheating," but neither player can prove that the other player cheated, even after watching the replay. This is possible because  nothing that a player does *really* happens until another player sees it.

Currently this is just a technical proof of concept that lets you move around units, rewind time by holding down R, then change where your units went in the past as long as they stay in a yellow region (which are guaranteed to be impossible for other players to see). However, I also have other game mechanics in mind, and I'm hoping to keep working on this and turn it into a full game. If you want to help, feel free to send pull requests to the GitHub repository at https://github.com/ad510/decoherence , or email questions or comments to me using the link at http://ad510.users.sf.net/.

System Requirements
-------------------
The game is written in C# 2010, and requires the .NET Framework 4.0 and SlimDX. These can be downloaded at the following links:

- .NET Framework 4.0: https://www.microsoft.com/en-us/download/details.aspx?id=24872
- SlimDX: http://slimdx.org/download.php

Starting the Game
-----------------
If you find this in a compressed folder, extract all files before running the program. You may be able to find the executable to start the game at bin\Debug\decoherence.exe but if it does not exist you will have to compile it using C# 2010 or later.

Controls
--------
- esc = exit
- arrow keys/move mouse to edge of screen = move view
- (ctrl/shift) left click/drag = select particles
- right click = move selected particles in tight formation
- ctrl + right click = move selected particles in loose formation
- spacebar = change selected player
- p = pause/resume
- r = go backwards in time

License
-------
The program is licensed under the MIT License. You can view the license in copying.txt.