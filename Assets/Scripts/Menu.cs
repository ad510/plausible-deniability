// Copyright (c) 2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

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
