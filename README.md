Plausible Deniability
=====================

Overview
--------
This is an RTS game prototype that lets you cheat with plausible deniability. By that, I mean that if you play a multiplayer game and cheat, then even after the opponent watches the replay and compares it to what they saw while they played, it is impossible for them to prove that you cheated (short of hacking into the game or replay file). An example of cheating with plausible deniability is a map hack (seeing the entire map). An example of cheating *without* plausible deniability is giving yourself extra resources, since opponents can notice that when they watch the replay. But surely map hacks cannot be the only way to cheat with plausible deniability?

For example, Plausible Deniability lets you move your units along multiple paths when other players aren't looking (by pressing N), but the moment another player sees your unit moving along a path, the path they see is the one that is taken. You can also move your units in the past (use the time slider to rewind) as long as your new paths stay in yellow regions, which you know other players can't see based on what your units see. I also have more ideas that aren't implemented yet. The key aspect to these features is that anything you don't do is wiped from history, so in replays (press shift + R to view an instant replay), your opponents won't see you doing anything beyond what is allowed in a normal RTS game.

I'm hoping to keep working on this and turn it into a full multiplayer game. If you want to help, feel free to submit issues or pull requests to the GitHub repository at https://github.com/ad510/plausible-deniability , or email questions or comments to me using the link at http://andrewd.50webs.com/.

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
