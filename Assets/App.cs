// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using ProtoBuf;

/// <summary>
/// contains initialization and user interface code
/// </summary>
public class App : MonoBehaviour {
	public const double SelBoxMin = 100;
	public const float FntSize = 1f / 40;
	
	string appPath;
	string modPath = "mod/";
	float winDiag;
	Texture[] imgUnit;
	Sim g;
	int selPlayer;
	List<int> selUnits;
	int makeUnitType;
	long timeNow;
	long timeLast;
	long timeGame;
	bool paused;
	int speed;
	long timeSpeedChg;
	Vector3[] mouseDownPos;
	Vector3[] mouseUpPos;
	string serverAddr = "127.0.0.1";
	int serverPort = 44247;
	
	/// <summary>
	/// use this for initialization
	/// </summary>
	void Start () {
		appPath = Application.streamingAssetsPath + '/';
		winDiag = new Vector2(Screen.width, Screen.height).magnitude;
		mouseDownPos = new Vector3[3];
		mouseUpPos = new Vector3[3];
		if (!scnOpen (appPath + modPath + "scn.json", 0, false)) {
			Debug.LogError ("Scenario failed to load.");
		}
	}
	
	/// <summary>
	/// loads scenario from json file and returns whether successful
	/// </summary>
	private bool scnOpen(string path, int user, bool multiplayer) {
		int i, j, k;
		Hashtable json;
		ArrayList jsonA;
		bool b = false;
		// TODO: if this ever supports multiplayer games, host should load file & send data to other players, otherwise json double parsing may not match
		if (!System.IO.File.Exists(path)) return false;
		json = (Hashtable)Procurios.Public.JSON.JsonDecode(System.IO.File.ReadAllText(path), ref b);
		if (!b) return false;
		// base scenario
		g = new Sim();
		g.selUser = user;
		g.networkView = multiplayer ? networkView : null;
		g.events = new SimEvtList();
		g.cmdPending = new SimEvtList();
		g.cmdHistory = new SimEvtList();
		g.unitIdChgs = new List<int>();
		g.timeSim = -1;
		g.timeUpdateEvt = long.MinValue;
		g.events.add(new UpdateEvt(-1));
		g.maxSpeed = 0;
		g.mapSize = jsonFP(json, "mapSize");
		g.updateInterval = (long)jsonDouble(json, "updateInterval");
		g.visRadius = jsonFP(json, "visRadius");
		g.camSpeed = jsonFP(json, "camSpeed");
		g.camPos = jsonFPVector(json, "camPos", new FP.Vector(g.mapSize / 2, g.mapSize / 2));
		g.drawScl = (float)jsonDouble(json, "drawScl");
		g.drawSclMin = (float)jsonDouble(json, "drawSclMin");
		g.drawSclMax = (float)jsonDouble(json, "drawSclMax");
		g.healthBarSize = jsonVector2(json, "healthBarSize");
		g.healthBarYOffset = (float)jsonDouble(json, "healthBarYOffset");
		g.backCol = jsonColor(json, "backCol");
		g.borderCol = jsonColor(json, "borderCol");
		g.noVisCol = jsonColor(json, "noVisCol");
		g.playerVisCol = jsonColor(json, "playerVisCol");
		g.unitVisCol = jsonColor(json, "unitVisCol");
		g.coherentCol = jsonColor(json, "coherentCol");
		g.pathCol = jsonColor(json, "pathCol");
		g.healthBarBackCol = jsonColor(json, "healthBarBackCol");
		g.healthBarFullCol = jsonColor(json, "healthBarFullCol");
		g.healthBarEmptyCol = jsonColor(json, "healthBarEmptyCol");
		//Sim.music = jsonString(json, "music");
		// resources
		g.nRsc = 0;
		jsonA = jsonArray(json, "resources");
		if (jsonA != null) {
			foreach (string rscName in jsonA) {
				g.nRsc++;
				Array.Resize(ref g.rscNames, g.nRsc);
				g.rscNames[g.nRsc - 1] = rscName;
			}
		}
		// players
		g.nUsers = 0;
		g.nPlayers = 0;
		jsonA = jsonArray(json, "players");
		if (jsonA != null) {
			foreach (Hashtable jsonO in jsonA) {
				Hashtable jsonO2 = jsonObject(jsonO, "startRsc");
				Sim.Player player = new Sim.Player();
				player.name = jsonString(jsonO, "name");
				player.isUser = jsonBool(jsonO, "isUser");
				player.user = (short)jsonDouble(jsonO, "user");
				if (player.user >= g.nUsers) g.nUsers = player.user + 1;
				player.startRsc = new long[g.nRsc];
				for (j = 0; j < g.nRsc; j++) {
					player.startRsc[j] = (jsonO2 != null) ? jsonFP(jsonO2, g.rscNames[j]) : 0;
				}
				g.nPlayers++;
				Array.Resize(ref g.players, g.nPlayers);
				g.players[g.nPlayers - 1] = player;
			}
			foreach (Hashtable jsonO in jsonA) {
				ArrayList jsonA2 = jsonArray(jsonO, "mayAttack");
				i = g.playerNamed(jsonString(jsonO, "name"));
				g.players[i].mayAttack = new bool[g.nPlayers];
				for (j = 0; j < g.nPlayers; j++) {
					g.players[i].mayAttack[j] = false;
				}
				if (jsonA2 != null) {
					foreach (string player in jsonA2) {
						if (g.playerNamed(player) >= 0) {
							g.players[i].mayAttack[g.playerNamed(player)] = true;
						}
					}
				}
			}
			for (i = 0; i < g.nPlayers; i++) {
				g.players[i].immutable = g.calcPlayerImmutable(i);
			}
		}
		// users
		g.users = new Sim.User[g.nUsers];
		for (i = 0; i < g.nUsers; i++) {
			g.users[i] = new Sim.User();
		}
		// unit types
		g.nUnitT = 0;
		jsonA = jsonArray(json, "unitTypes");
		if (jsonA != null) {
			foreach (Hashtable jsonO in jsonA) {
				Hashtable jsonO2 = jsonObject(jsonO, "rscCost");
				Hashtable jsonO3 = jsonObject(jsonO, "rscCollectRate");
				Sim.UnitType unitT = new Sim.UnitType();
				unitT.name = jsonString(jsonO, "name");
				unitT.imgPath = jsonString(jsonO, "imgPath");
				unitT.maxHealth = (int)jsonDouble(jsonO, "maxHealth");
				unitT.speed = jsonFP(jsonO, "speed");
				if (unitT.speed > g.maxSpeed) g.maxSpeed = unitT.speed;
				unitT.reload = (long)jsonDouble(jsonO, "reload");
				unitT.range = jsonFP(jsonO, "range");
				unitT.tightFormationSpacing = jsonFP(jsonO, "tightFormationSpacing");
				unitT.makeUnitMinDist = jsonFP(jsonO, "makeUnitMinDist");
				unitT.makeUnitMaxDist = jsonFP(jsonO, "makeUnitMaxDist");
				unitT.selRadius = jsonDouble(jsonO, "selRadius");
				unitT.rscCost = new long[g.nRsc];
				unitT.rscCollectRate = new long[g.nRsc];
				for (j = 0; j < g.nRsc; j++) {
					unitT.rscCost[j] = (jsonO2 != null) ? jsonFP(jsonO2, g.rscNames[j]) : 0;
					unitT.rscCollectRate[j] = (jsonO3 != null) ? jsonFP(jsonO3, g.rscNames[j]) : 0;
				}
				g.nUnitT++;
				Array.Resize(ref g.unitT, g.nUnitT);
				g.unitT[g.nUnitT - 1] = unitT;
			}
			foreach (Hashtable jsonO in jsonA) {
				Hashtable jsonO2 = jsonObject(jsonO, "damage");
				ArrayList jsonA2 = jsonArray(jsonO, "canMake");
				i = g.unitTypeNamed(jsonString(jsonO, "name"));
				g.unitT[i].makeOnUnitT = g.unitTypeNamed(jsonString(jsonO, "makeOnUnitT"));
				g.unitT[i].damage = new int[g.nUnitT];
				for (j = 0; j < g.nUnitT; j++) {
					g.unitT[i].damage[j] = (jsonO2 != null) ? (int)jsonDouble(jsonO2, g.unitT[j].name) : 0;
				}
				g.unitT[i].canMake = new bool[g.nUnitT];
				for (j = 0; j < g.nUnitT; j++) {
					g.unitT[i].canMake[j] = false;
				}
				if (jsonA2 != null) {
					foreach (string type in jsonA2) {
						if (g.unitTypeNamed(type) >= 0) {
							g.unitT[i].canMake[g.unitTypeNamed(type)] = true;
						}
					}
				}
			}
		}
		imgUnit = new Texture[g.nUnitT * g.nPlayers];
		for (i = 0; i < g.nUnitT; i++) {
			for (j = 0; j < g.nPlayers; j++) {
				k = i * g.nPlayers + j;
				if (!(imgUnit[k] = loadTexture (appPath + modPath + g.players[j].name + '.' + g.unitT[i].imgPath))) {
					if (!(imgUnit[k] = loadTexture (appPath + modPath + g.unitT[i].imgPath))) {
						Debug.LogWarning ("Failed to load " + modPath + g.players[j].name + '.' + g.unitT[i].imgPath);
					}
				}
			}
		}
		// tiles
		g.tiles = new Sim.Tile[g.tileLen(), g.tileLen()];
		for (i = 0; i < g.tileLen(); i++) {
			for (j = 0; j < g.tileLen(); j++) {
				g.tiles[i, j] = new Sim.Tile(g);
			}
		}
		// units
		g.nUnits = 0;
		jsonA = jsonArray(json, "units");
		if (jsonA != null) {
			foreach (Hashtable jsonO in jsonA) {
				g.setNUnits(g.nUnits + 1);
				g.u[g.nUnits - 1] = new Unit(g, g.nUnits - 1, g.unitTypeNamed(jsonString(jsonO, "type")),
					g.playerNamed(jsonString(jsonO, "player")), (long)jsonDouble(jsonO, "startTime"),
					jsonFPVector(jsonO, "startPos", new FP.Vector((long)(UnityEngine.Random.value * g.mapSize), (long)(UnityEngine.Random.value * g.mapSize))));
			}
		}
		// tile graphics
		/*tlTile.primitive = PrimitiveType.TriangleList;
		tlTile.setNPoly(0);
		tlTile.nV[0] = g.tileLen() * g.tileLen() * 2;
		tlTile.poly[0].v = new DX.TLVertex[tlTile.nV[0] * 3];
		for (i = 0; i < tlTile.poly[0].v.Length; i++) {
			tlTile.poly[0].v[i].rhw = 1;
			tlTile.poly[0].v[i].z = 0;
		}*/
		// start game
		Camera.main.backgroundColor = g.backCol;
		selPlayer = 0;
		while (g.players[selPlayer].user != g.selUser) selPlayer = (selPlayer + 1) % g.nPlayers;
		selUnits = new List<int>();
		makeUnitType = -1;
		paused = false;
		speed = 0;
		timeGame = 0;
		timeSpeedChg = (long)(Time.time * 1000) - 1000;
		timeNow = (long)(Time.time * 1000);
		return true;
	}
	
	private Texture2D loadTexture(string path) {
		if (!System.IO.File.Exists (path)) return null;
		Texture2D tex = new Texture2D(0, 0);
		byte[] imgBytes = System.IO.File.ReadAllBytes (path);
		tex.LoadImage (imgBytes);
		return tex;
	}
	
	/// <summary>
	/// Update is called once per frame
	/// </summary>
	void Update () {
		updateTime ();
		g.updatePast (selPlayer, timeGame);
		g.update (timeGame);
		updateInput ();
	}
	
	private void updateTime() {
		timeLast = timeNow;
		timeNow = (long)(Time.time * 1000);
		if (!paused) {
			long timeGameDiff;
			if (Input.GetKey (KeyCode.R)) {
				// rewind
				timeGameDiff = -(timeNow - timeLast);
			}
			else if (timeNow - timeLast > g.updateInterval && timeGame + timeNow - timeLast >= g.timeSim) {
				// cap time difference to a max amount
				timeGameDiff = g.updateInterval;
			}
			else {
				// normal speed
				timeGameDiff = timeNow - timeLast;
			}
			timeGame += (long)(timeGameDiff * Math.Pow(2, speed)); // adjust game speed based on user setting
		}
		// only apply next UpdateEvt when received all users' commands across network
		if (timeGame >= g.timeUpdateEvt + g.updateInterval) {
			for (int i = 0; i < g.nUsers; i++) {
				if (!g.users[i].cmdAllReceived) {
					timeGame = g.timeUpdateEvt + g.updateInterval - 1;
					break;
				}
			}
		}
	}
	
	private void updateInput() {
		int i, j;
		// handle changed unit indices
		for (i = 0; i < g.unitIdChgs.Count / 2; i++) {
			if (selUnits.Contains(g.unitIdChgs[i * 2])) {
				if (g.unitIdChgs[i * 2 + 1] >= 0 && !selUnits.Contains(g.unitIdChgs[i * 2 + 1])) selUnits.Insert(selUnits.IndexOf(g.unitIdChgs[i * 2]), g.unitIdChgs[i * 2 + 1]);
				selUnits.Remove(g.unitIdChgs[i * 2]);
			}
		}
		g.unitIdChgs.Clear();
		// handle changed mouse buttons
		if (Input.GetMouseButtonDown (0)) { // left button down
			mouseDownPos[0] = mousePos();
		}
		if (Input.GetMouseButtonDown (1)) { // right button down
			mouseDownPos[1] = mousePos();
		}
		if (Input.GetMouseButtonDown (2)) { // middle button down
			mouseDownPos[2] = mousePos();
		}
		if (Input.GetMouseButtonUp (0)) { // left button up
			mouseUpPos[0] = mousePos();
			if (makeUnitType >= 0) {
				// make unit
				// happens at newCmdTime() + 1 so new unit starts out live if game is live
				FP.Vector pos = makeUnitPos();
				if (pos.x != Sim.OffMap) g.cmdPending.add(new MakeUnitCmdEvt(g.timeSim, newCmdTime() + 1, selUnits.ToArray(), makeUnitType, pos));
				makeUnitType = -1;
			}
			else {
				// select units
				Vector3 drawPos;
				if (!Input.GetKey (KeyCode.LeftControl) && !Input.GetKey (KeyCode.LeftShift)) selUnits.Clear();
				for (i = 0; i < g.nUnits; i++) {
					if (selPlayer == g.u[i].player && timeGame >= g.u[i].m[0].timeStart) {
						drawPos = simToDrawPos(g.u[i].calcPos(timeGame));
						if (drawPos.x + g.unitT[g.u[i].type].selRadius >= Math.Min(mouseDownPos[0].x, mousePos ().x)
							&& drawPos.x - g.unitT[g.u[i].type].selRadius <= Math.Max(mouseDownPos[0].x, mousePos ().x)
							&& drawPos.y + g.unitT[g.u[i].type].selRadius >= Math.Min(mouseDownPos[0].y, mousePos ().y)
							&& drawPos.y - g.unitT[g.u[i].type].selRadius <= Math.Max(mouseDownPos[0].y, mousePos ().y)) {
							if (selUnits.Contains(i)) {
								selUnits.Remove(i);
							}
							else {
								selUnits.Add(i);
							}
							if (SelBoxMin > (mousePos() - mouseDownPos[0]).sqrMagnitude) break;
						}
					}
				}
			}
		}
		if (Input.GetMouseButtonUp (1)) { // right button up
			mouseUpPos[1] = mousePos();
			if (makeUnitType >= 0) {
				// cancel making unit
				makeUnitType = -1;
			}
			else {
				// move selected units
				g.cmdPending.add(new MoveCmdEvt(g.timeSim, newCmdTime(), selUnits.ToArray(), drawToSimPos (mousePos ()),
					Input.GetKey (KeyCode.LeftControl) ? Formation.Loose : Input.GetKey (KeyCode.LeftAlt) ? Formation.Ring : Formation.Tight));
			}
		}
		if (Input.GetMouseButtonUp (2)) { // middle button up
			mouseUpPos[2] = mousePos();
		}
		// handle changed keys
		if (Input.GetKeyDown (KeyCode.Escape)) {
			// exit
			Application.Quit ();
		}
		if (Input.GetKeyDown (KeyCode.Space)) {
			// change selected player
			do {
				selPlayer = (selPlayer + 1) % g.nPlayers;
			} while (g.players[selPlayer].user != g.selUser);
			selUnits.Clear();
			makeUnitType = -1;
		}
		if (Input.GetKeyDown (KeyCode.P)) {
			// pause/resume
			paused = !paused;
		}
		if (Input.GetKeyDown (KeyCode.Equals)) {
			// increase speed
			speed++;
			timeSpeedChg = Environment.TickCount;
		}
		if (Input.GetKeyDown (KeyCode.Minus)) {
			// decrease speed
			speed--;
			timeSpeedChg = Environment.TickCount;
		}
		for (i = 0; i < 9; i++) {
			if (Input.GetKeyDown (KeyCode.Alpha1 + i)) {
				// make unit of specified type
				foreach (int unit in selUnits) {
					if (g.u[unit].canMakeChildUnit(timeGame + 1, false, i)) {
						if (g.unitT[i].speed > 0 && g.unitT[i].makeOnUnitT < 0) {
							int[] unitArray = new int[1];
							unitArray[0] = unit;
							// happens at newCmdTime() + 1 so new unit starts out live if game is live
							g.cmdPending.add(new MakeUnitCmdEvt(g.timeSim, newCmdTime() + 1, unitArray, i, makeUnitMovePos(timeGame + 1, unit)));
						}
						else {
							makeUnitType = i;
						}
						break;
					}
				}
			}
		}
		if (Input.GetKeyDown (KeyCode.N)) {
			// create new paths that selected units could take
			if (selUnits.Count > 0) {
				FP.Vector[] pos = new FP.Vector[selUnits.Count];
				for (i = 0; i < selUnits.Count; i++) {
					if (g.u[selUnits[i]].exists(timeGame + 1)) pos[i] = makeUnitMovePos(timeGame + 1, selUnits[i]);
				}
				// happens at newCmdTime() + 1 so new path starts out live if game is live
				g.cmdPending.add(new MakePathCmdEvt(g.timeSim, newCmdTime() + 1, selUnits.ToArray(), pos));
			}
		}
		if (Input.GetKeyDown (KeyCode.Delete)) {
			// delete selected paths
			// happens at newCmdTime() instead of newCmdTime() + 1 so that when paused, making path then deleting parent path doesn't cause an error
			if (selUnits.Count > 0) g.cmdPending.add(new DeletePathCmdEvt(g.timeSim, newCmdTime(), selUnits.ToArray()));
		}
		if (Input.GetKeyDown (KeyCode.R) && Input.GetKey (KeyCode.LeftShift)) {
			// instant replay
			timeGame = 0;
			// hide traces of mischief
			for (i = 0; i < g.nUnits; i++) {
				g.u[i].decohere();
				g.u[i].tileX = Sim.OffMap + 1;
			}
			for (i = 0; i < g.tileLen(); i++) {
				for (j = 0; j < g.tileLen(); j++) {
					g.tiles[i, j] = new Sim.Tile(g);
				}
			}
			for (i = 0; i < g.nUnits; i++) {
				g.u[i].addTileMoveEvts(ref g.events, -1, g.timeSim);
			}
			g.update(g.timeSim);
			paused = true;
		}
		// move camera
		if (Input.GetKey (KeyCode.LeftArrow) || mousePos ().x == 0 || (!Screen.fullScreen && mousePos ().x <= 15)) {
			g.camPos.x -= g.camSpeed * (timeNow - timeLast);
			if (g.camPos.x < 0) g.camPos.x = 0;
		}
		if (Input.GetKey (KeyCode.RightArrow) || mousePos ().x == Screen.width - 1 || (!Screen.fullScreen && mousePos ().x >= Screen.width - 15)) {
			g.camPos.x += g.camSpeed * (timeNow - timeLast);
			if (g.camPos.x > g.mapSize) g.camPos.x = g.mapSize;
		}
		if (Input.GetKey (KeyCode.UpArrow) || mousePos ().y == 0 || (!Screen.fullScreen && mousePos ().y <= 15)) {
			g.camPos.y -= g.camSpeed * (timeNow - timeLast);
			if (g.camPos.y < 0) g.camPos.y = 0;
		}
		if (Input.GetKey (KeyCode.DownArrow) || mousePos ().y == Screen.height - 1 || (!Screen.fullScreen && mousePos ().y >= Screen.height - 15)) {
			g.camPos.y += g.camSpeed * (timeNow - timeLast);
			if (g.camPos.y > g.mapSize) g.camPos.y = g.mapSize;
		}
	}
	
	// TODO: game slows down if many GUI events (e.g. mouse movements) happen in 1 frame, should do drawing in Update() instead
	void OnGUI() {
		Vector3 vec = new Vector3(), vec2;
		FP.Vector fpVec;
		Texture2D tex;
		Color col;
		float f;
		int i, j, tX, tY;
		// visibility tiles
		// TODO: don't draw tiles off map
		tex = new Texture2D(g.tileLen (), g.tileLen (), TextureFormat.ARGB32, false);
		for (tX = 0; tX < g.tileLen(); tX++) {
			for (tY = 0; tY < g.tileLen(); tY++) {
				vec = simToDrawPos(new FP.Vector(tX << FP.Precision, tY << FP.Precision));
				vec2 = simToDrawPos(new FP.Vector((tX + 1) << FP.Precision, (tY + 1) << FP.Precision));
				col = g.noVisCol;
				if (g.tiles[tX, tY].playerVisWhen(selPlayer, timeGame)) {
					col += g.playerVisCol;
					if (g.tiles[tX, tY].playerDirectVisWhen(selPlayer, timeGame)) col += g.unitVisCol;
					if (g.tiles[tX, tY].coherentWhen(selPlayer, timeGame)) col += g.coherentCol;
				}
				tex.SetPixel (tX, g.tileLen () - 1 - tY, col);
			}
		}
		tex.Apply ();
		vec = simToDrawPos (new FP.Vector(0, 0));
		vec2 = simToDrawPos (new FP.Vector(g.tileLen () << FP.Precision, g.tileLen () << FP.Precision));
		GUI.DrawTexture (new Rect(vec.x, vec.y, vec2.x - vec.x, vec2.y - vec.y), tex);
		tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		// map border
		/*tlPoly.primitive = PrimitiveType.LineStrip;
		tlPoly.setNPoly(0);
		tlPoly.nV[0] = 4;
		tlPoly.poly[0].v = new DX.TLVertex[tlPoly.nV[0] + 1];
		for (i = 0; i < 4; i++) {
			tlPoly.poly[0].v[i].color = g.borderCol.ToArgb();
			tlPoly.poly[0].v[i].rhw = 1;
			tlPoly.poly[0].v[i].z = 0;
		}
		vec = simToDrawPos(new FP.Vector());
		vec2 = simToDrawPos(new FP.Vector(g.mapSize, g.mapSize));
		tlPoly.poly[0].v[0].x = vec.X;
		tlPoly.poly[0].v[0].y = vec.Y;
		tlPoly.poly[0].v[1].x = vec2.X;
		tlPoly.poly[0].v[1].y = vec.Y;
		tlPoly.poly[0].v[2].x = vec2.X;
		tlPoly.poly[0].v[2].y = vec2.Y;
		tlPoly.poly[0].v[3].x = vec.X;
		tlPoly.poly[0].v[3].y = vec2.Y;
		tlPoly.poly[0].v[4] = tlPoly.poly[0].v[0];
		tlPoly.draw();*/
		// unit path lines
		/*for (i = 0; i < g.nUnits; i++) {
			if (unitDrawPos(i, ref vec) && g.u[i].isChildPath && timeGame >= g.u[g.u[i].parent].m[0].timeStart) {
				DX.d3dDevice.SetTexture(0, null);
				tlPoly.primitive = PrimitiveType.LineStrip;
				tlPoly.setNPoly(0);
				tlPoly.nV[0] = 1;
				tlPoly.poly[0].v = new DX.TLVertex[tlPoly.nV[0] + 1];
				tlPoly.poly[0].v[0] = new DX.TLVertex(vec, g.pathCol.ToArgb(), 0, 0);
				tlPoly.poly[0].v[1] = new DX.TLVertex(simToDrawPos(g.u[g.u[i].parent].calcPos(timeGame)), g.pathCol.ToArgb(), 0, 0);
				tlPoly.draw();
			}
		}*/
		// units
		// TODO: scale unit images
		for (i = 0; i < g.nUnits; i++) {
			if (unitDrawPos(i, ref vec)) {
				/*if (g.u[i].isLive(timeGame)) {
					imgUnit[j].color = new Color4(1, 1, 1, 1).ToArgb();
				}
				else {
					imgUnit[j].color = new Color4(0.5f, 1, 1, 1).ToArgb(); // TODO: make transparency amount customizable
				}*/
				drawUnit (g.u[i].player, g.u[i].type, vec);
				if (Input.GetKey (KeyCode.LeftShift) && selUnits.Contains(i)) {
					// show final position if holding shift
					drawUnit (g.u[i].player, g.u[i].type, simToDrawPos(g.u[i].m[g.u[i].n - 1].vecEnd));
				}
			}
		}
		// unit to be made
		if (makeUnitType >= 0) {
			fpVec = makeUnitPos();
			if (fpVec.x != Sim.OffMap) {
				//imgUnit[i].color = new Color4(1, 1, 1, 1).ToArgb();
				vec = simToDrawPos(fpVec);
			}
			else {
				//imgUnit[i].color = new Color4(0.5f, 1, 1, 1).ToArgb(); // TODO: make transparency amount customizable
				vec = mousePos();
			}
			drawUnit (selPlayer, makeUnitType, vec);
		}
		// health bars
		foreach (int unit in selUnits) {
			if (unitDrawPos(unit, ref vec)) {
				j = g.u[unit].type * g.nPlayers + g.u[unit].player;
				f = ((float)g.u[g.u[unit].rootParentPath()].healthWhen(timeGame)) / g.unitT[g.u[unit].type].maxHealth;
				// background
				if (g.u[unit].healthWhen(timeGame) > 0) {
					tex.SetPixel (0, 0, g.healthBarBackCol);
					tex.Apply ();
					GUI.DrawTexture (new Rect(vec.x + g.healthBarSize.x * winDiag * (-0.5f + f),
						vec.y - imgUnit[j].height / 2 - (g.healthBarYOffset - g.healthBarSize.y / 2) * winDiag,
						g.healthBarSize.x * winDiag * (1 - f), g.healthBarSize.y * winDiag), tex);
				}
				// foreground
				tex.SetPixel (0, 0, g.healthBarEmptyCol + (g.healthBarFullCol - g.healthBarEmptyCol) * f);
				tex.Apply ();
				GUI.DrawTexture (new Rect(vec.x + g.healthBarSize.x * winDiag * -0.5f,
					vec.y - imgUnit[j].height / 2 - (g.healthBarYOffset - g.healthBarSize.y / 2) * winDiag,
					g.healthBarSize.x * winDiag * f, g.healthBarSize.y * winDiag), tex);
			}
		}
		// select box (if needed)
		// TODO: make color customizable by mod?
		if (Input.GetMouseButton (0) && makeUnitType < 0 && SelBoxMin <= (mousePos() - mouseDownPos[0]).sqrMagnitude) {
			/*DX.d3dDevice.SetTexture(0, null);
			tlPoly.primitive = PrimitiveType.LineStrip;
			tlPoly.setNPoly(0);
			tlPoly.nV[0] = 4;
			tlPoly.poly[0].v = new DX.TLVertex[tlPoly.nV[0] + 1];
			for (i = 0; i < 4; i++) {
				tlPoly.poly[0].v[i].color = DX.ColWhite;
				tlPoly.poly[0].v[i].rhw = 1;
				tlPoly.poly[0].v[i].z = 0;
			}
			tlPoly.poly[0].v[0].x = DX.mouseDX[1];
			tlPoly.poly[0].v[0].y = DX.mouseDY[1];
			tlPoly.poly[0].v[1].x = DX.mouseX;
			tlPoly.poly[0].v[1].y = DX.mouseDY[1];
			tlPoly.poly[0].v[2].x = DX.mouseX;
			tlPoly.poly[0].v[2].y = DX.mouseY;
			tlPoly.poly[0].v[3].x = DX.mouseDX[1];
			tlPoly.poly[0].v[3].y = DX.mouseY;
			tlPoly.poly[0].v[4] = tlPoly.poly[0].v[0];
			tlPoly.draw();*/
			tex.SetPixel (0, 0, new Color(1, 1, 1, 0.5f));
			tex.Apply ();
			GUI.DrawTexture (new Rect(mouseDownPos[0].x, mouseDownPos[0].y, mousePos().x - mouseDownPos[0].x, mousePos().y - mouseDownPos[0].y), tex);
		}
		// text
		// TODO: make font, size, and color customizable by mod
		GUIStyle style = GUIStyle.none;
		style.fontSize = (int)(Screen.height * FntSize);
		style.normal.textColor = Color.white;
		GUI.Label (new Rect(0, 0, Screen.width, style.fontSize), (timeGame >= g.timeSim) ? "LIVE" : "TIME TRAVELING", style);
		if (paused) GUI.Label (new Rect(0, style.fontSize, Screen.width, style.fontSize), "PAUSED", style);
		if (Environment.TickCount < timeSpeedChg) timeSpeedChg -= UInt32.MaxValue;
		if (Environment.TickCount < timeSpeedChg + 1000) GUI.Label (new Rect(0, style.fontSize * 2, Screen.width, style.fontSize), "SPEED: " + Math.Pow(2, speed) + "x", style);
		if (g.players[selPlayer].timeGoLiveFail != long.MaxValue) {
			style.normal.textColor = Color.red;
			GUI.Label (new Rect(0, style.fontSize * 3, Screen.width, style.fontSize), "ERROR: Going live may cause you to have negative resources " + (timeGame - g.players[selPlayer].timeNegRsc) / 1000 + " second(s) ago.", style);
		}
		for (i = 0; i < g.nRsc; i++) {
			long rscMin = (long)Math.Floor(FP.toDouble(g.playerResource(selPlayer, timeGame, i, false, true, false)));
			long rscMax = (long)Math.Floor(FP.toDouble(g.playerResource(selPlayer, timeGame, i, true, true, false)));
			style.normal.textColor = (rscMin >= 0) ? Color.white : Color.red;
			GUI.Label (new Rect(0, Screen.height + (i - g.nRsc) * Screen.height * FntSize, Screen.width, style.fontSize), g.rscNames[i] + ": " + rscMin + ((rscMax != rscMin) ? " to " + rscMax : ""), style);
		}
		// multiplayer GUI
		if (Network.peerType == NetworkPeerType.Disconnected) {
			serverAddr = GUI.TextField (new Rect(0, style.fontSize * 4, 10 * style.fontSize, style.fontSize), serverAddr);
			serverPort = int.Parse (GUI.TextField (new Rect(0, style.fontSize * 5, 10 * style.fontSize, style.fontSize), serverPort.ToString ()));
			if (GUI.Button (new Rect(0, style.fontSize * 6, 10 * style.fontSize, style.fontSize), "Connect as client")) {
				Network.Connect (serverAddr, serverPort);
			}
			if (GUI.Button (new Rect(0, style.fontSize * 7, 10 * style.fontSize, style.fontSize), "Start server")) {
				Network.InitializeServer (g.nUsers - 1, serverPort, !Network.HavePublicAddress ());
			}
		}
		else {
			if (GUI.Button (new Rect(0, style.fontSize * 4, 10 * style.fontSize, style.fontSize), "Disconnect")) {
				Network.Disconnect (200);
			}
		}
	}
	
	private void drawUnit(int player, int type, Vector3 pos) {
		int i = type * g.nPlayers + player;
		GUI.DrawTexture (new Rect(pos.x - imgUnit[i].width / 2, pos.y - imgUnit[i].height / 2, imgUnit[i].width, imgUnit[i].height), imgUnit[i]);
	}
	
	void OnPlayerConnected(NetworkPlayer player) {
		if (Network.connections.Length == g.nUsers - 1) {
			int seed = UnityEngine.Random.Range (int.MinValue, int.MaxValue);
			scnOpenMultiplayer (0, seed);
			for (int i = 0; i < Network.connections.Length; i++) {
				networkView.RPC ("scnOpenMultiplayer", Network.connections[i], i + 1, seed);
			}
		}
	}
	
	[RPC]
	void scnOpenMultiplayer(int user, int seed) {
		UnityEngine.Random.seed = seed;
		scnOpen (appPath + modPath + "scn.json", user, true);
	}
	
	// cmdType is same as the SimEvt type's protobuf identifier
	[RPC]
	void addCmd(int user, int cmdType, byte[] cmdData) {
		System.IO.MemoryStream stream = new System.IO.MemoryStream(cmdData);
		if (cmdType == 11) {
			g.users[user].cmdReceived.add (Serializer.Deserialize<MoveCmdEvt>(stream));
		}
		else if (cmdType == 12) {
			g.users[user].cmdReceived.add (Serializer.Deserialize<MakeUnitCmdEvt>(stream));
		}
		else if (cmdType == 13) {
			g.users[user].cmdReceived.add (Serializer.Deserialize<MakePathCmdEvt>(stream));
		}
		else if (cmdType == 14) {
			g.users[user].cmdReceived.add (Serializer.Deserialize<DeletePathCmdEvt>(stream));
		}
		else if (cmdType == 15) {
			g.users[user].cmdReceived.add (Serializer.Deserialize<GoLiveCmdEvt>(stream));
		}
		else {
			throw new InvalidOperationException("received command of invalid type");
		}
	}
	
	[RPC]
	void allCmdsSent(int user) {
		g.users[user].cmdAllReceived = true;
	}

	private string jsonString(Hashtable json, string key, string defaultVal = "") {
		if (json.ContainsKey(key) && json[key] is string) return (string)json[key];
		return defaultVal;
	}

	private double jsonDouble(Hashtable json, string key, double defaultVal = 0) {
		if (json.ContainsKey(key) && json[key] is double) return (double)json[key];
		return defaultVal;
	}

	private bool jsonBool(Hashtable json, string key, bool defaultVal = false) {
		if (json.ContainsKey(key) && json[key] is bool) return (bool)json[key];
		return defaultVal;
	}

	private long jsonFP(Hashtable json, string key, long defaultVal = 0) {
		if (json.ContainsKey(key)) {
			if (json[key] is double) return FP.fromDouble((double)json[key]);
			if (json[key] is string) {
				// parse as hex string, so no rounding errors when converting from double
				// allow beginning string with '-' to specify negative number, as alternative to prepending with f's
				long ret;
				if (long.TryParse(((string)json[key]).TrimStart('-'), System.Globalization.NumberStyles.HexNumber, null, out ret)) {
					return ((string)json[key])[0] == '-' ? -ret : ret;
				}
				return defaultVal;
			}
		}
		return defaultVal;
	}

	private Hashtable jsonObject(Hashtable json, string key) {
		if (json.ContainsKey(key) && json[key] is Hashtable) return (Hashtable)json[key];
		return null;
	}

	private ArrayList jsonArray(Hashtable json, string key) {
		if (json.ContainsKey(key) && json[key] is ArrayList) return (ArrayList)json[key];
		return null;
	}

	private FP.Vector jsonFPVector(Hashtable json, string key, FP.Vector defaultVal = new FP.Vector()) {
		if (json.ContainsKey(key) && json[key] is Hashtable) {
			return new FP.Vector(jsonFP((Hashtable)json[key], "x", defaultVal.x),
				jsonFP((Hashtable)json[key], "y", defaultVal.y),
				jsonFP((Hashtable)json[key], "z", defaultVal.z));
		}
		return defaultVal;
	}

	private Vector2 jsonVector2(Hashtable json, string key, Vector2 defaultVal = new Vector2()) {
		if (json.ContainsKey(key) && json[key] is Hashtable) {
			return new Vector2((float)jsonDouble((Hashtable)json[key], "x", defaultVal.x),
				(float)jsonDouble((Hashtable)json[key], "y", defaultVal.y));
		}
		return defaultVal;
	}

	private Color jsonColor(Hashtable json, string key) {
		if (json.ContainsKey(key) && json[key] is Hashtable) {
			return new Color((float)jsonDouble((Hashtable)json[key], "r", 0),
				(float)jsonDouble((Hashtable)json[key], "g", 0),
				(float)jsonDouble((Hashtable)json[key], "b", 0),
				(float)jsonDouble((Hashtable)json[key], "a", 1));
		}
		return new Color();
	}

	/// <summary>
	/// returns where to make new unit, or (Sim.OffMap, 0) if mouse is at invalid position
	/// </summary>
	private FP.Vector makeUnitPos() {
		FP.Vector vec;
		if (drawToSimPos(mousePos()).x >= 0 && drawToSimPos(mousePos()).x <= g.mapSize
			&& drawToSimPos(mousePos()).y >= 0 && drawToSimPos(mousePos()).y <= g.mapSize) {
			if (g.unitT[makeUnitType].makeOnUnitT >= 0) {
				// selected unit type must be made on top of another unit of correct type
				// TODO: prevent putting multiple units on same unit (unless on different paths of same unit and maybe some other cases)
				for (int i = 0; i < g.nUnits; i++) {
					if (g.u[i].exists(timeGame)) {
						vec = g.u[i].calcPos(timeGame);
						if (g.u[i].type == g.unitT[makeUnitType].makeOnUnitT && g.tileAt(vec).playerVisWhen(selPlayer, timeGame)
							&& (mousePos() - simToDrawPos(vec)).sqrMagnitude <= g.unitT[g.u[i].type].selRadius * g.unitT[g.u[i].type].selRadius) {
							return vec;
						}
					}
				}
			}
			else {
				return drawToSimPos(mousePos());
			}
		}
		return new FP.Vector(Sim.OffMap, 0);
	}

	/// <summary>
	/// returns where new unit can move out of the way after specified unit makes it
	/// </summary>
	/// <remarks>chooses a random location between makeUnitMinDist and makeUnitMaxDist away from unit</remarks>
	private FP.Vector makeUnitMovePos(long time, int unit) {
		FP.Vector ret;
		do {
			ret = new FP.Vector((long)((UnityEngine.Random.value - 0.5) * g.unitT[g.u[unit].type].makeUnitMaxDist * 2),
				(long)((UnityEngine.Random.value - 0.5) * g.unitT[g.u[unit].type].makeUnitMaxDist * 2));
		} while (ret.lengthSq() < g.unitT[g.u[unit].type].makeUnitMinDist * g.unitT[g.u[unit].type].makeUnitMinDist
			&& ret.lengthSq() > g.unitT[g.u[unit].type].makeUnitMaxDist * g.unitT[g.u[unit].type].makeUnitMaxDist);
		return ret + g.u[unit].calcPos(time);
	}

	/// <summary>
	/// sets pos to where unit should be drawn at, and returns whether it should be drawn
	/// </summary>
	private bool unitDrawPos(int unit, ref Vector3 pos) {
		FP.Vector fpVec;
		if (!g.u[unit].exists(timeGame) || (selPlayer != g.u[unit].player && !g.u[unit].isLive(timeGame))) return false;
		fpVec = g.u[unit].calcPos(timeGame);
		if (selPlayer != g.u[unit].player && !g.tileAt(fpVec).playerVisWhen(selPlayer, timeGame)) return false;
		pos = simToDrawPos(fpVec);
		return true;
	}
	
	/// <summary>
	/// returns suggested timeCmd field for a new CmdEvt, corresponding to when it would appear to be applied
	/// </summary>
	public long newCmdTime() {
		if (g.networkView != null) { // multiplayer
			return Math.Min (timeGame, g.timeUpdateEvt) + g.updateInterval * 2;
		}
		else { // single player
			return timeGame;
		}
	}
	
	/// <summary>
	/// returns current mouse position relative to upper left corner
	/// </summary>
	private Vector3 mousePos() {
		return new Vector3(Input.mousePosition.x, Screen.height - 1 - Input.mousePosition.y, 0);
	}

	private float simToDrawScl(long coor) {
		return (float)(FP.toDouble(coor) * g.drawScl * winDiag);
	}

	private long drawToSimScl(float coor) {
		return FP.fromDouble(coor / winDiag / g.drawScl);
	}

	private Vector3 simToDrawScl(FP.Vector vec) {
		return new Vector3(simToDrawScl(vec.x), simToDrawScl(vec.y), simToDrawScl(vec.z));
	}

	private FP.Vector drawToSimScl(Vector3 vec) {
		return new FP.Vector(drawToSimScl(vec.x), drawToSimScl(vec.y), drawToSimScl(vec.z));
	}

	private Vector3 simToDrawPos(FP.Vector vec) {
		return new Vector3(simToDrawScl(vec.x - g.camPos.x), simToDrawScl(vec.y - g.camPos.y), 0f) + new Vector3(Screen.width / 2, Screen.height / 2, 0f);
	}

	private FP.Vector drawToSimPos(Vector3 vec) {
		return new FP.Vector(drawToSimScl(vec.x - Screen.width / 2), drawToSimScl(vec.y - Screen.height / 2)) + g.camPos;
	}
}
