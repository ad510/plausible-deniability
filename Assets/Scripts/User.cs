// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class User {
	public List<SimEvt> cmdReceived; // commands received from this user to be applied in next update
	public long timeSync; // latest time at which commands from this user are ready to be applied
	public Dictionary<long, int> checksums; // checksums calculated by this user to be compared to our checksum (key is timeSync when checksum is received)
	
	public User() {
		cmdReceived = new List<SimEvt>();
		timeSync = 0;
		checksums = new Dictionary<long, int>();
	}
}
