Decoherence
===========
*Nothing really happens, until you see it.*

Overview
--------
This is an RTS game prototype in which you don't have to make up your mind about what your units do until another player sees them. For example, you can move your units along multiple paths when other players aren't looking (by pressing N), but the moment another player sees your unit moving along a path, the path they see is the one that is taken. You can also move your units in the past (hold down R to rewind) as long as your new paths stay in yellow regions, which you know other players can't see based on what your units see. I'm also hoping to implement more convenient ways to change what you did in the past. The key aspect to these features is that anything you don't do is wiped from history, so in replays (press shift + R to view an instant replay), your opponents won't see you doing anything beyond what is allowed in a normal RTS game, making you look consistently lucky.

I'm hoping to keep working on this and turn it into a full multiplayer game. If you want to help, feel free to send pull requests to the GitHub repository at https://github.com/ad510/decoherence , or email questions or comments to me using the link at http://ad510.users.sf.net/.

System Requirements
-------------------
The game is made using the free version of Unity, which can be downloaded at https://unity3d.com/.

Controls
--------
- esc = exit
- arrow keys = move view
- move mouse to edge of screen (fullscreen only) = move view
- page up, page down, mouse wheel = zoom view
- (ctrl/shift) left click/drag = select units
- right click = move selected units
- right click on friendly unit = stack selected units onto clicked unit
- shift = show final positions of selected units
- spacebar = change selected player
- p = pause/resume
- r = go backwards in time
- + = increase speed
- - = decrease speed
- t = tight formation
- l = loose formation
- o = ring formation
- n = create new paths that selected units could take
- delete = delete selected paths
- shift + delete/shift + d = delete unselected paths of selected units
- s = share selected paths
- shift + r = instant replay
- shift + o = load game
- shift + s = save game

License
-------
Decoherence is licensed under the MIT License. You can view the license in copying.txt.

Decoherence uses the protobuf-net r640 library (Assets/protobuf-net.dll), which is licensed under the Apache License 2.0. You can view the license in copying-protobuf-net.txt, and the protobuf-net website is https://code.google.com/p/protobuf-net/.
