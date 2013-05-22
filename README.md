Decoherence
===========
*Nothing really happens, until you see it.*

Overview
--------
This is a technical proof of concept of an RTS game in which you don't have to make up your mind about what you do until another player sees it. You can do this by splitting your units into multiple "amplitudes" (by pressing A), but the moment another player sees one, the one they see is the one that happens. You can even do this in the past (hold down R to rewind) as long as your new amplitudes stay in yellow regions, which you know other players can't see based on what your units see. I'm also hoping to implement more convenient ways to change what you did in the past. The key aspect to these features is that anything you don't do is wiped from history, so in replays (after I implement them), your opponents won't see you doing anything beyond what is allowed in a normal RTS game, making you look psychic (or just consistently lucky).

I'm hoping to keep working on this and turn it into a full multiplayer game. If you want to help, feel free to send pull requests to the GitHub repository at https://github.com/ad510/decoherence , or email questions or comments to me using the link at http://ad510.users.sf.net/.

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
- (ctrl/shift) left click/drag = select units
- right click = move selected units in tight formation
- ctrl + right click = move selected units in loose formation
- alt + right click = move selected units in ring formation
- spacebar = change selected player
- p = pause/resume
- r = go backwards in time
- a = create amplitudes from selected units
- delete = delete selected amplitudes

License
-------
The program is licensed under the MIT License. You can view the license in copying.txt.