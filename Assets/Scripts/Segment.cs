// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// describes the composition of a section of a path
/// </summary>
public class Segment {
	public readonly Sim g;
	public readonly Path path;
	public int id; // index in segment list
	public long timeStart;
	/// <summary>
	/// segments that branch off at the beginning of this segment;
	/// connected branches share the same List instance so updating this list in one segment updates it for all branches
	/// (NOTE: protobuf-net won't like that)
	/// </summary>
	public List<Segment> branches;
	public List<int> units; // indices of units on this path segment
	public bool unseen; // whether path segment is known to not be seen by another player
	
	public Segment(Path pathVal, int idVal, long timeStartVal, List<int> unitsVal, bool unseenVal) {
		path = pathVal;
		g = path.g;
		id = idVal;
		timeStart = timeStartVal;
		branches = new List<Segment>();
		branches.Add (this);
		units = unitsVal;
		unseen = unseenVal;
	}
	
	/// <summary>
	/// removes all units if doing so wouldn't affect anything that another player saw
	/// </summary>
	public void removeAllUnits() {
		while (units.Count > 0) {
			if (!new SegmentUnit(this, g.units[units.Last ()]).delete ()) throw new SystemException("failed to remove a unit from segment");
		}
	}
	
	/// <summary>
	/// iterates over all segments that merge onto the beginning of this segment
	/// </summary>
	public IEnumerable<Segment> prev() {
		foreach (Segment segment in branches) {
			if (segment.prevOnPath() != null) yield return segment.prevOnPath();
		}
	}
	
	/// <summary>
	/// iterates over all segments that branch off from the end of this segment
	/// </summary>
	public IEnumerable<Segment> next() {
		if (nextOnPath() != null) {
			foreach (Segment segment in nextOnPath().branches) {
				yield return segment;
			}
		}
	}
	
	/// <summary>
	/// returns previous segment on this path, or null if this is the first segment
	/// </summary>
	public Segment prevOnPath() {
		if (id == 0) return null;
		return path.segments[id - 1];
	}
	
	/// <summary>
	/// returns next segment on this path, or null if this is the last segment
	/// </summary>
	public Segment nextOnPath() {
		if (id == path.segments.Count - 1) return null;
		return path.segments[id + 1];
	}
	
	public IEnumerable<SegmentUnit> segmentUnits() {
		foreach (int unit in units) {
			yield return new SegmentUnit(this, g.units[unit]);
		}
	}
}
