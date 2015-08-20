// Written in 2013-2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using ProtoBuf;

/// <summary>
/// base class for simulation events
/// </summary>
[ProtoContract]
[ProtoInclude(10, typeof(CmdEvt))]
[ProtoInclude(11, typeof(UpdateEvt))]
[ProtoInclude(12, typeof(TileUpdateEvt))]
[ProtoInclude(13, typeof(StackEvt))]
[ProtoInclude(14, typeof(PlayerVisRemoveEvt))]
[ProtoInclude(15, typeof(WaypointAddEvt))]
public abstract class SimEvt {
	[ProtoMember(1)] public long time;

	public abstract void apply(Sim g);
}
