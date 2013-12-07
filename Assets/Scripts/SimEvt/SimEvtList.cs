// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// list of simulation events in order of ascending event time
/// </summary>
public class SimEvtList {
	public List<SimEvt> events;

	public SimEvtList() {
		events = new List<SimEvt>();
	}

	/// <summary>
	/// inserts specified event into list in order of ascending event time
	/// </summary>
	public void add(SimEvt evt) {
		int ins;
		for (ins = events.Count; ins >= 1 && evt.time < events[ins - 1].time; ins--) ;
		events.Insert(ins, evt);
	}

	/// <summary>
	/// pops the first (earliest) event from the list, returning null if list is empty
	/// </summary>
	public SimEvt pop() {
		if (events.Count == 0) return null;
		SimEvt ret = events[0];
		events.RemoveAt(0);
		return ret;
	}

	/// <summary>
	/// returns time of first (earliest) event in list, or long.MaxValue if list is empty
	/// </summary>
	public long peekTime() {
		if (events.Count == 0) return long.MaxValue;
		return events[0].time;
	}
}
