// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// base class for unit commands
/// </summary>
[ProtoContract]
[ProtoInclude(10, typeof(MoveCmdEvt))]
[ProtoInclude(11, typeof(MakeUnitCmdEvt))]
[ProtoInclude(12, typeof(MakePathCmdEvt))]
[ProtoInclude(13, typeof(DeletePathCmdEvt))]
[ProtoInclude(14, typeof(DeleteOtherPathsCmdEvt))]
[ProtoInclude(15, typeof(StackCmdEvt))]
[ProtoInclude(16, typeof(SharePathsCmdEvt))]
public abstract class UnitCmdEvt : CmdEvt {
	[ProtoMember(1)] public long timeCmd; // time is latest simulation time when command is given, timeCmd is when event takes place (may be in past)
	[ProtoMember(2)] public UnitIdSelection[] paths;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	protected UnitCmdEvt() { }

	protected UnitCmdEvt(long timeVal, long timeCmdVal, UnitIdSelection[] pathsVal) {
		time = timeVal;
		timeCmd = timeCmdVal;
		paths = pathsVal;
	}
	
	[ProtoAfterDeserialization]
	protected void afterDeserialize() {
		if (paths == null) paths = new UnitIdSelection[0];
	}
	
	public static UnitIdSelection[] pathsArg(List<UnitSelection> units) {
		UnitIdSelection[] ret = new UnitIdSelection[units.Count];
		for (int i = 0; i < units.Count; i++) {
			ret[i] = new UnitIdSelection(units[i]);
		}
		return ret;
	}
}
