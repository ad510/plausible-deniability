Decoherence
===========
*Nothing really happens, until you see it.*

Overview
--------
This is a technical proof of concept of an RTS game in which you only have to make up your mind about what you do when another player sees it. For example, you can move your units along multiple paths when other players aren't looking (by pressing N), but the moment another player sees your unit moving along a path, the path they see is the one that is taken. You can also move your units in the past (hold down R to rewind) as long as your new paths stay in yellow regions, which you know other players can't see based on what your units see. I'm also hoping to implement more convenient ways to change what you did in the past. The key aspect to these features is that anything you don't do is wiped from history, so in replays (press ctrl + R to view an instant replay), your opponents won't see you doing anything beyond what is allowed in a normal RTS game, making you look consistently lucky.

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
- shift = show final positions of selected units
- spacebar = change selected player
- p = pause/resume
- r = go backwards in time
- + = increase speed
- - = decrease speed
- numbers 1-9 = make unit of type corresponding to number pressed
- n = create new paths that selected units could take
- delete = delete selected paths
- ctrl + r = instant replay

License
-------
The program is licensed under the MIT License. You can view the license in copying.txt.