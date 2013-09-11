Decoherence
===========

Overview
--------
This is an RTS game prototype in which you can make unlikely things happen. This is possible because you don't *really* have to make up your mind about what your units do until another player sees them. For example, say you have some super powerful unit and you want it to defend your base, but you don't know which side your opponent will attack from. If you put the unit on one side of your base, you'll have, say, a 20% chance that it'll be in the right place when the opponent attacks. One thing Decoherence lets you do is take that unit and move it simultaneously along multiple paths, letting you surround your base with that unit's paths. Then, the moment another player sees your unit, the path they see is the one that your unit takes, and your unit's other paths are deleted from history. That means that instead of having a 20% chance that your unit is in the right place when the opponent attacks, you have a 100% chance that your unit is where the opponent first comes to your base! The best part is that paths that aren't taken are wiped from history, so in replays, your unit will appear to have been in the right place the whole time, making you look lucky (or psychic).

The game will also let you make other unlikely things happen to strategically defend an attack at multiple locations (so the opponent can't just put a decoy unit on one side of your base then attack on the other side), hide long range sniper units in multiple places at once, correct past mistakes before other players see them, swap out units in battle with ones with greater health, and things like that, all without opponents being able to tell when they watch the replay. Unfortunately most of those features aren't implemented yet, but I'm hoping to keep working on this and turn it into a full multiplayer game. If you want to help, feel free to send pull requests to the GitHub repository at https://github.com/ad510/decoherence , or email questions or comments to me using the link at http://ad510.users.sf.net/.

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
- right click = move selected units in tight formation
- ctrl + right click = move selected units in loose formation
- alt + right click = move selected units in ring formation
- shift = show final positions of selected units
- spacebar = change selected player
- p = pause/resume
- r = go backwards in time
- + = increase speed
- - = decrease speed
- n = create new paths that selected units could take
- delete = delete selected paths
- shift + delete/shift + d = delete unselected paths of selected units
- shift + r = instant replay

License
-------
Decoherence is licensed under the MIT License. You can view the license in copying.txt.

Decoherence uses the protobuf-net r640 library (Assets/protobuf-net.dll), which is licensed under the Apache License 2.0. You can view the license in copying-protobuf-net.txt, and the protobuf-net website is https://code.google.com/p/protobuf-net/.
