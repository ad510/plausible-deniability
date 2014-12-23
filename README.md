Plausible Deniability
=====================

Overview
--------
This is an experimental RTS prototype that lets you cheat with plausible deniability. By that, I mean that if you play a multiplayer game and cheat, then even after the opponent watches the replay and compares it to what they saw while they played, it is impossible for them to prove that you cheated (short of hacking into the game or replay file). An example of cheating with plausible deniability is a map hack (seeing the entire map). An example of cheating *without* plausible deniability is giving yourself extra resources, since opponents can notice that when they watch the replay. But surely map hacks cannot be the only way to cheat with plausible deniability?

For example, Plausible Deniability lets you move your units along multiple paths when other players aren't looking (by pressing N), but the moment another player sees your unit moving along a path, the path they see is the one that is taken. There is also a special way of doing this called "sharing paths" that influences the order that other players see your units, for example if you want to keep a sniper out of sight. If other players don't mind you seeing them cheat, you can hit F3 to do a map hack and check the "automatic time travel" box, which makes your units automatically move in the past to get to where you want them faster. The key aspect to these features is that anything you don't do is wiped from history, so in replays (press shift + R to view an instant replay), your opponents won't see you doing anything beyond what is allowed in a normal RTS game. This works because you don't *really* have to make up your mind about what your units do until another player sees them.

From a purely technical perspective, this is a fleshed out proof of concept that incorporates most ways of cheating with plausible deniability that I've thought of apart from exploiting identical units, which would practically require a rewrite to implement. But after I reached this point in summer 2014, I pretty much hit a brick wall because I have no clue how to turn this into an actual game, and neither does anyone I showed it to. That said, if I figure out a way to get unstuck (and simultaneously manage the necessarily complex codebase), I'd still love to turn this into a full multiplayer game. If you want to help, feel free to submit issues or pull requests to the GitHub repository at https://github.com/ad510/plausible-deniability , or email questions or comments to me using the link at http://andrewd.50webs.com/.

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
- right click on opponent unit = attack clicked unit
- right click on friendly unit = stack selected units onto clicked unit
- shift = show final positions of selected units
- p = pause/resume
- + = increase speed
- - = decrease speed
- q = tight formation
- w = loose formation
- e = ring formation
- u = unstack
- n = create new paths that selected units could take
- delete/d = delete selected paths
- shift + delete/shift + d = delete unselected paths of selected units
- s = share selected paths
- f2 = change selected player
- f3 = toggle map hack
- shift + r = instant replay
- shift + o = load game
- shift + s = save game

License
-------
Plausible Deniability is licensed under the MIT License. You can view the license in copying.txt.

Plausible Deniability uses the protobuf-net r640 library (Assets/protobuf-net.dll), which is licensed under the Apache License 2.0. You can view the license in copying-protobuf-net.txt, and the protobuf-net website is https://code.google.com/p/protobuf-net/.
