// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// describes the composition of a section of a path
/// </summary>
[ProtoContract]
public class Segment {
	[ProtoMember(1, AsReference = true)] public readonly Sim g;
	[ProtoMember(2, AsReference = true)] public readonly Path path;
	[ProtoMember(3)] public int id; // index in segment list
	[ProtoMember(4)] public long timeStart;
	/// <summary>
	/// segments that branch off at the beginning of this segment;
	/// connected branches share the same List instance so updating this list in one segment updates it for all branches
	/// (NOTE: protobuf-net won't like that)
	/// </summary>
	[ProtoMember(5, AsReference = true)] public List<Segment> branches;
	[ProtoMember(6, AsReference = true)] public List<Unit> units; // units on this path segment
	[ProtoMember(8, AsReference = true)] public List<Unit> deletedUnits;
	[ProtoMember(7)] public bool unseen; // whether path segment is known to not be seen by another player
	
	/// <summary>
	/// parameterless constructor for protobuf-net use only
	/// </summary>
	private Segment() {
		units = new List<Unit>();
		deletedUnits = new List<Unit>();
	}
	
	public Segment(Path pathVal, int idVal, long timeStartVal, List<Unit> unitsVal, bool unseenVal) {
		path = pathVal;
		g = path.g;
		id = idVal;
		timeStart = timeStartVal;
		branches = new List<Segment> { this };
		units = unitsVal;
		deletedUnits = new List<Unit>();
		unseen = unseenVal;
	}
	
	/// <summary>
	/// removes all units if doing so wouldn't affect anything that another player saw
	/// </summary>
	public void removeAllUnits(bool addMoveLines = false) {
		while (units.Count > 0) {
			if (!new SegmentUnit(this, units.Last ()).delete (addMoveLines)) throw new SystemException("failed to remove a unit from segment");
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
		foreach (Unit unit in units) {
			yield return new SegmentUnit(this, unit);
		}
	}
}
