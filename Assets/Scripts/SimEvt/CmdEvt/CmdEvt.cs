// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using ProtoBuf;

/// <summary>
/// protobuf-net identifiers of each type of CmdEvt
/// </summary>
public enum CmdEvtTag {
	unit = 10,
	move = 11,
	makeUnit = 12,
	makePath = 13,
	deletePath = 14,
	deleteOtherPaths = 15,
	stack = 16,
	goLive = 17,
}

/// <summary>
/// base class for user commands
/// </summary>
[ProtoContract]
[ProtoInclude((int)CmdEvtTag.unit, typeof(UnitCmdEvt))]
[ProtoInclude((int)CmdEvtTag.goLive, typeof(GoLiveCmdEvt))]
public abstract class CmdEvt : SimEvt { }
