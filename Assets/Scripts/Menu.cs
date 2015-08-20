// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using UnityEngine;
using System.Collections;

public class Menu : MonoBehaviour {
	
	/// <summary>
	/// use this for initialization
	/// </summary>
	void Start () {
		
	}
	
	/// <summary>
	/// Update is called once per frame
	/// </summary>
	void Update () {
	
	}
	
	void OnGUI() {
		GUILayout.BeginArea (new Rect(0, Screen.height / 3, Screen.width / 3, Screen.height));
		if (GUILayout.Button ("Welcome to Entirely Plausible Land")) {
			App.scnPath = "scn_welcome.json";
			Application.LoadLevel ("GameScene");
		}
		if (GUILayout.Button ("Find the Floating Building")) {
			App.scnPath = "scn_nsa.json";
			Application.LoadLevel ("GameScene");
		}
		if (GUILayout.Button ("Exit")) {
			Application.Quit ();
		}
		GUILayout.EndArea ();
	}
}
