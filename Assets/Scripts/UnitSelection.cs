// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class UnitSelection {
	public readonly Path path;
	public readonly Unit unit;
	public readonly long time;
	
	public UnitSelection(Path pathVal, Unit unitVal, long timeVal) {
		path = pathVal;
		unit = unitVal;
		time = timeVal;
	}
}
