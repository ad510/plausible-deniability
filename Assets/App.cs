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
	private enum UnitMenu {
		Main, MakeUnit
	}
	
	private class LineBox {
		public GameObject gameObject;
		public LineRenderer line;
		
		public LineBox() {
			gameObject = new GameObject();
			line = gameObject.AddComponent<LineRenderer>();
			line.material.shader = Shader.Find ("Diffuse");
			line.SetVertexCount (8);
		}
		
		public void setRect(Vector3 p1, Vector3 p2, float depth) {
			float minX = Math.Min (p1.x, p2.x);
			float minY = Math.Min (p1.y, p2.y);
			float maxX = Math.Max (p1.x, p2.x);
			float maxY = Math.Max (p1.y, p2.y);
			// extra vertices are needed to draw >1px thick lines correctly due to LineRenderer weirdness
			line.SetPosition (0, new Vector3(minX, minY, depth));
			line.SetPosition (1, new Vector3(maxX - 1, minY, depth));
			line.SetPosition (2, new Vector3(maxX, minY, depth));
			line.SetPosition (3, new Vector3(maxX, maxY - 1, depth));
			line.SetPosition (4, new Vector3(maxX, maxY, depth));
			line.SetPosition (5, new Vector3(minX + 1, maxY, depth));
			line.SetPosition (6, new Vector3(minX, maxY, depth));
			line.SetPosition (7, new Vector3(minX, minY, depth));
		}
		
		public void dispose() {
			Destroy(gameObject);
		}
	}
	
	private class UnitSprite {
		public GameObject sprite;
		public GameObject preview; // for showing unit at final position
		public GameObject healthBarBack;
		public GameObject healthBarFore;
		public LineRenderer pathLine;
		public int type;
		public int player;
		
		public UnitSprite(GameObject quadPrefab) {
			sprite = Instantiate(quadPrefab) as GameObject;
			preview = Instantiate(quadPrefab) as GameObject;
			healthBarBack = Instantiate(quadPrefab) as GameObject;
			healthBarFore = Instantiate(quadPrefab) as GameObject;
			pathLine = sprite.AddComponent<LineRenderer>();
			pathLine.material.shader = Shader.Find ("Diffuse");
			pathLine.SetVertexCount (2);
			type = -1;
			player = -1;
		}
		
		public void dispose() {
			Destroy (sprite);
			Destroy (preview);
			Destroy (healthBarBack);
			Destroy (healthBarFore);
		}
	}
	
	public const double SelBoxMin = 100;
	public const float FntSize = 1f / 40;
	public const float TileDepth = 6f;
	public const float BorderDepth = 5f;
	public const float PathLineDepth = 4f;
	public const float UnitDepth = 3f;
	public const float HealthBarDepth = 2f;
	public const float SelectBoxDepth = 1f;
	
	public GameObject quadPrefab;
	
	string appPath;
	string modPath = "mod/";
	float winDiag; // diagonal length of screen in pixels
	GameObject sprTile;
	LineBox border;
	Texture[,] texUnits;
	List<UnitSprite> sprUnits;
	GameObject sprMakeUnit;
	LineBox selectBox;
	GUIStyle lblStyle;
	UnitMenu unitMenu;
	Vector2 unitMenuScrollPos;
	Vector2 selUnitsScrollPos;
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
		RenderSettings.ambientLight = Color.white;
		Camera.main.orthographicSize = Screen.height / 2;
		Camera.main.transform.position = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
		quadPrefab.renderer.material.shader = Shader.Find ("Transparent/Diffuse");
		quadPrefab.renderer.material.color = Color.white;
		quadPrefab.transform.rotation = new Quaternion(0, 1, 0, 0);
		sprTile = Instantiate (quadPrefab) as GameObject;
		border = new LineBox();
		border.line.SetWidth (2, 2); // TODO: make width customizable by mod
		sprMakeUnit = Instantiate (quadPrefab) as GameObject;
		// TODO: make color and width customizable by mod
		selectBox = new LineBox();
		selectBox.line.material.color = Color.white;
		selectBox.line.SetWidth (2, 2);
		// TODO: make font, size, and color customizable by mod
		lblStyle = GUIStyle.none;
		lblStyle.fontSize = (int)(Screen.height * FntSize);
		lblStyle.normal.textColor = Color.white;
		if (!scnOpen (appPath + modPath + "scn.json", 0, false)) {
			Debug.LogError ("Scenario failed to load.");
		}
	}
	
	/// <summary>
	/// loads scenario from json file and returns whether successful
	/// </summary>
	private bool scnOpen(string path, int user, bool multiplayer) {
		int i, j;
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
		g.checksum = 0;
		g.synced = true;
		g.timeSim = -1;
		g.timeUpdateEvt = long.MinValue;
		g.events.add(new UpdateEvt(-1));
		g.maxSpeed = 0;
		g.mapSize = jsonFP(json, "mapSize");
		g.updateInterval = (long)jsonDouble(json, "updateInterval");
		g.visRadius = jsonFP(json, "visRadius");
		g.camPos = jsonFPVector(json, "camPos", new FP.Vector(g.mapSize / 2, g.mapSize / 2));
		g.camSpeed = jsonFP(json, "camSpeed");
		g.zoom = (float)jsonDouble(json, "zoom");
		g.zoomMin = (float)jsonDouble(json, "zoomMin");
		g.zoomMax = (float)jsonDouble(json, "zoomMax");
		g.zoomSpeed = (float)jsonDouble (json, "zoomSpeed");
		g.zoomMouseWheelSpeed = (float)jsonDouble (json, "zoomMouseWheelSpeed");
		g.uiBarHeight = (float)jsonDouble (json, "uiBarHeight");
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
				player.user = (int)jsonDouble(jsonO, "user");
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
				unitT.imgOffset = jsonFPVector (jsonO, "imgOffset");
				unitT.imgHalfHeight = jsonFP (jsonO, "imgHalfHeight");
				unitT.selMinPos = jsonFPVector (jsonO, "selMinPos", new FP.Vector(unitT.imgOffset.x - unitT.imgHalfHeight, unitT.imgOffset.y - unitT.imgHalfHeight));
				unitT.selMaxPos = jsonFPVector (jsonO, "selMaxPos", new FP.Vector(unitT.imgOffset.x + unitT.imgHalfHeight, unitT.imgOffset.y + unitT.imgHalfHeight));
				unitT.maxHealth = (int)jsonDouble(jsonO, "maxHealth");
				unitT.speed = jsonFP(jsonO, "speed");
				if (unitT.speed > g.maxSpeed) g.maxSpeed = unitT.speed;
				unitT.reload = (long)jsonDouble(jsonO, "reload");
				unitT.range = jsonFP(jsonO, "range");
				unitT.tightFormationSpacing = jsonFP(jsonO, "tightFormationSpacing");
				unitT.makeUnitMinDist = jsonFP(jsonO, "makeUnitMinDist");
				unitT.makeUnitMaxDist = jsonFP(jsonO, "makeUnitMaxDist");
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
		texUnits = new Texture[g.nUnitT, g.nPlayers];
		for (i = 0; i < g.nUnitT; i++) {
			for (j = 0; j < g.nPlayers; j++) {
				if (!(texUnits[i, j] = loadTexture (appPath + modPath + g.players[j].name + '.' + g.unitT[i].imgPath))) {
					if (!(texUnits[i, j] = loadTexture (appPath + modPath + g.unitT[i].imgPath))) {
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
				if (g.unitTypeNamed(jsonString(jsonO, "type")) >= 0 && g.playerNamed(jsonString(jsonO, "player")) >= 0) {
					g.setNUnits(g.nUnits + 1);
					g.u[g.nUnits - 1] = new Unit(g, g.nUnits - 1, g.unitTypeNamed(jsonString(jsonO, "type")),
						g.playerNamed(jsonString(jsonO, "player")), (long)jsonDouble(jsonO, "startTime"),
						jsonFPVector(jsonO, "startPos", new FP.Vector((long)(UnityEngine.Random.value * g.mapSize), (long)(UnityEngine.Random.value * g.mapSize))));
				}
			}
		}
		if (sprUnits != null) {
			foreach (UnitSprite spr in sprUnits) {
				spr.dispose ();
			}
		}
		sprUnits = new List<UnitSprite>();
		// start game
		Camera.main.backgroundColor = g.backCol;
		border.line.material.color = g.borderCol;
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
		draw ();
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
		// don't increment time past latest time that commands were synced across network
		if (g.networkView != null && timeGame >= g.timeUpdateEvt + g.updateInterval) {
			for (int i = 0; i < g.nUsers; i++) {
				if (g.users[i].timeSync < g.timeUpdateEvt + g.updateInterval) {
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
			mouseDownPos[0] = Input.mousePosition;
		}
		if (Input.GetMouseButtonDown (1)) { // right button down
			mouseDownPos[1] = Input.mousePosition;
		}
		if (Input.GetMouseButtonDown (2)) { // middle button down
			mouseDownPos[2] = Input.mousePosition;
		}
		if (Input.GetMouseButtonUp (0)) { // left button up
			mouseUpPos[0] = Input.mousePosition;
			if (mouseDownPos[0].y > Screen.height * g.uiBarHeight) {
				FP.Vector pos;
				if (makeUnitType >= 0) {
					// make unit
					// happens at newCmdTime() + 1 so new unit starts out live if game is live
					pos = makeUnitPos();
					if (pos.x != Sim.OffMap) g.cmdPending.add(new MakeUnitCmdEvt(g.timeSim, newCmdTime() + 1, selUnits.ToArray(), makeUnitType, pos));
					makeUnitType = -1;
				}
				else {
					// select units
					if (!Input.GetKey (KeyCode.LeftControl) && !Input.GetKey (KeyCode.LeftShift)) selUnits.Clear();
					for (i = 0; i < g.nUnits; i++) {
						if (selPlayer == g.u[i].player && timeGame >= g.u[i].m[0].timeStart) {
							pos = g.u[i].calcPos(timeGame);
							if (FP.rectIntersects (drawToSimPos (mouseDownPos[0]), drawToSimPos (Input.mousePosition),
								pos + g.unitT[g.u[i].type].selMinPos, pos + g.unitT[g.u[i].type].selMaxPos)) {
								if (selUnits.Contains(i)) {
									selUnits.Remove(i);
								}
								else {
									selUnits.Add(i);
								}
								if (SelBoxMin > (Input.mousePosition - mouseDownPos[0]).sqrMagnitude) break;
							}
						}
					}
					unitMenu = UnitMenu.Main;
				}
			}
		}
		if (Input.GetMouseButtonUp (1)) { // right button up
			mouseUpPos[1] = Input.mousePosition;
			if (mouseDownPos[1].y > Screen.height * g.uiBarHeight) {
				if (makeUnitType >= 0) {
					// cancel making unit
					makeUnitType = -1;
				}
				else {
					// move selected units
					g.cmdPending.add(new MoveCmdEvt(g.timeSim, newCmdTime(), selUnits.ToArray(), drawToSimPos (Input.mousePosition),
						Input.GetKey (KeyCode.LeftControl) ? Formation.Loose : Input.GetKey (KeyCode.LeftAlt) ? Formation.Ring : Formation.Tight));
				}
			}
		}
		if (Input.GetMouseButtonUp (2)) { // middle button up
			mouseUpPos[2] = Input.mousePosition;
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
		if (Input.GetKeyDown (KeyCode.N)) {
			// create new paths that selected units could take
			makePaths ();
		}
		if (Input.GetKeyDown (KeyCode.Delete)) {
			if (Input.GetKey (KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift)) {
				// delete unselected paths of selected units
				deleteOtherPaths ();
			}
			else {
				// delete selected paths
				deletePaths ();
			}
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
			// allow selecting any player
			for (i = 0; i < g.nPlayers; i++) {
				if (g.players[i].isUser) g.players[i].user = g.selUser;
			}
			paused = true;
		}
		// move camera
		if (Input.GetKey (KeyCode.LeftArrow) || (Input.mousePosition.x == 0 && Screen.fullScreen)) {
			g.camPos.x -= g.camSpeed * (timeNow - timeLast);
			if (g.camPos.x < 0) g.camPos.x = 0;
		}
		if (Input.GetKey (KeyCode.RightArrow) || (Input.mousePosition.x == Screen.width - 1 && Screen.fullScreen)) {
			g.camPos.x += g.camSpeed * (timeNow - timeLast);
			if (g.camPos.x > g.mapSize) g.camPos.x = g.mapSize;
		}
		if (Input.GetKey (KeyCode.DownArrow) || (Input.mousePosition.y == 0 && Screen.fullScreen)) {
			g.camPos.y -= g.camSpeed * (timeNow - timeLast);
			if (g.camPos.y < 0) g.camPos.y = 0;
		}
		if (Input.GetKey (KeyCode.UpArrow) || (Input.mousePosition.y == Screen.height - 1 && Screen.fullScreen)) {
			g.camPos.y += g.camSpeed * (timeNow - timeLast);
			if (g.camPos.y > g.mapSize) g.camPos.y = g.mapSize;
		}
		// zoom camera
		if (Input.GetKey (KeyCode.PageUp)) {
			g.zoom /= (float)Math.Exp (g.zoomSpeed * (timeNow - timeLast));
		}
		if (Input.GetKey (KeyCode.PageDown)) {
			g.zoom *= (float)Math.Exp (g.zoomSpeed * (timeNow - timeLast));
		}
		if (Input.mousePosition.y > Screen.height * g.uiBarHeight && Input.GetAxis ("Mouse ScrollWheel") != 0) {
			g.zoom *= (float)Math.Exp (g.zoomMouseWheelSpeed * Input.GetAxis ("Mouse ScrollWheel"));
		}
		if (g.zoom < g.zoomMin) g.zoom = g.zoomMin;
		if (g.zoom > g.zoomMax) g.zoom = g.zoomMax;
	}
	
	private void draw() {
		Vector3 vec = new Vector3();
		FP.Vector fpVec;
		Texture2D tex;
		Color col;
		float f;
		int i, tX, tY;
		// visibility tiles
		// TODO: don't draw tiles off map
		tex = new Texture2D(g.tileLen (), g.tileLen (), TextureFormat.ARGB32, false);
		for (tX = 0; tX < g.tileLen(); tX++) {
			for (tY = 0; tY < g.tileLen(); tY++) {
				col = g.noVisCol;
				if (g.tiles[tX, tY].playerVisWhen(selPlayer, timeGame)) {
					col += g.playerVisCol;
					if (g.tiles[tX, tY].playerDirectVisWhen(selPlayer, timeGame)) col += g.unitVisCol;
					if (g.tiles[tX, tY].coherentWhen(selPlayer, timeGame)) col += g.coherentCol;
				}
				tex.SetPixel (tX, tY, col);
			}
		}
		tex.Apply ();
		tex.filterMode = FilterMode.Point;
		sprTile.renderer.material.mainTexture = tex;
		sprTile.transform.position = simToDrawPos (new FP.Vector((g.tileLen () << FP.Precision) / 2, (g.tileLen () << FP.Precision) / 2), TileDepth);
		sprTile.transform.localScale = simToDrawScl (new FP.Vector((g.tileLen () << FP.Precision) / 2, (g.tileLen () << FP.Precision) / 2));
		// map border
		border.setRect (simToDrawPos (new FP.Vector()), simToDrawPos(new FP.Vector(g.mapSize, g.mapSize)), BorderDepth);
		// units
		for (i = 0; i < g.nUnits; i++) {
			if (i == sprUnits.Count) sprUnits.Add (new UnitSprite(quadPrefab));
			sprUnits[i].sprite.renderer.enabled = false;
			sprUnits[i].preview.renderer.enabled = false;
			sprUnits[i].healthBarBack.renderer.enabled = false;
			sprUnits[i].healthBarFore.renderer.enabled = false;
			sprUnits[i].pathLine.enabled = false;
			if (unitDrawPos(i, ref vec)) {
				if (sprUnits[i].type != g.u[i].type || sprUnits[i].player != g.u[i].player) {
					sprUnits[i].sprite.renderer.material.mainTexture = texUnits[g.u[i].type, g.u[i].player];
					sprUnits[i].preview.renderer.material.mainTexture = texUnits[g.u[i].type, g.u[i].player];
					sprUnits[i].pathLine.material.color = g.pathCol;
					sprUnits[i].type = g.u[i].type;
					sprUnits[i].player = g.u[i].player;
				}
				if (g.u[i].isLive(timeGame)) {
					sprUnits[i].sprite.renderer.material.color = new Color(1, 1, 1, 1);
				}
				else {
					sprUnits[i].sprite.renderer.material.color = new Color(1, 1, 1, 0.5f); // TODO: make transparency amount customizable
				}
				sprUnits[i].sprite.transform.position = vec + simToDrawScl (g.unitT[g.u[i].type].imgOffset);
				sprUnits[i].sprite.transform.localScale = unitScale (g.u[i].type, g.u[i].player);
				sprUnits[i].sprite.renderer.enabled = true;
				if (g.u[i].isChildPath && timeGame >= g.u[g.u[i].parent].m[0].timeStart) {
					// unit path line
					sprUnits[i].pathLine.SetPosition (0, new Vector3(vec.x, vec.y, PathLineDepth));
					sprUnits[i].pathLine.SetPosition (1, simToDrawPos (g.u[g.u[i].parent].calcPos(timeGame), PathLineDepth));
					sprUnits[i].pathLine.enabled = true;
				}
				if (Input.GetKey (KeyCode.LeftShift) && selUnits.Contains(i)) {
					// show final position if holding shift
					sprUnits[i].preview.renderer.material.color = sprUnits[i].sprite.renderer.material.color;
					sprUnits[i].preview.transform.position = simToDrawPos(g.u[i].m[g.u[i].n - 1].vecEnd + g.unitT[g.u[i].type].imgOffset, UnitDepth);
					sprUnits[i].preview.transform.localScale = sprUnits[i].sprite.transform.localScale;
					sprUnits[i].preview.renderer.enabled = true;
				}
			}
		}
		// unit to be made
		if (makeUnitType >= 0) {
			sprMakeUnit.renderer.material.mainTexture = texUnits[makeUnitType, selPlayer];
			fpVec = makeUnitPos();
			if (fpVec.x != Sim.OffMap) {
				sprMakeUnit.renderer.material.color = new Color(1, 1, 1, 1);
				sprMakeUnit.transform.position = simToDrawPos(fpVec + g.unitT[makeUnitType].imgOffset, UnitDepth);
			}
			else {
				sprMakeUnit.renderer.material.color = new Color(1, 1, 1, 0.5f); // TODO: make transparency amount customizable
				sprMakeUnit.transform.position = new Vector3(Input.mousePosition.x, Input.mousePosition.y, UnitDepth) + simToDrawScl (g.unitT[makeUnitType].imgOffset);
			}
			sprMakeUnit.transform.localScale = unitScale (makeUnitType, selPlayer);
			sprMakeUnit.renderer.enabled = true;
		}
		else {
			sprMakeUnit.renderer.enabled = false;
		}
		// health bars
		foreach (int unit in selUnits) {
			if (unitDrawPos(unit, ref vec)) {
				f = ((float)g.u[g.u[unit].rootParentPath()].healthWhen(timeGame)) / g.unitT[g.u[unit].type].maxHealth;
				vec.y += simToDrawScl (g.unitT[g.u[unit].type].selMaxPos.y) + g.healthBarYOffset * winDiag;
				// background
				if (g.u[unit].healthWhen(timeGame) > 0) {
					sprUnits[unit].healthBarBack.renderer.material.color = g.healthBarBackCol;
					sprUnits[unit].healthBarBack.transform.position = new Vector3(vec.x + g.healthBarSize.x * winDiag * f / 2, vec.y, HealthBarDepth);
					sprUnits[unit].healthBarBack.transform.localScale = new Vector3(g.healthBarSize.x * winDiag * (1 - f) / 2, g.healthBarSize.y * winDiag / 2, 1);
					sprUnits[unit].healthBarBack.renderer.enabled = true;
				}
				// foreground
				sprUnits[unit].healthBarFore.renderer.material.color = g.healthBarEmptyCol + (g.healthBarFullCol - g.healthBarEmptyCol) * f;
				sprUnits[unit].healthBarFore.transform.position = new Vector3(vec.x + g.healthBarSize.x * winDiag * (f - 1) / 2, vec.y, HealthBarDepth);
				sprUnits[unit].healthBarFore.transform.localScale = new Vector3(g.healthBarSize.x * winDiag * f / 2, g.healthBarSize.y * winDiag / 2, 1);
				sprUnits[unit].healthBarFore.renderer.enabled = true;
			}
		}
		// select box (if needed)
		if (Input.GetMouseButton (0) && makeUnitType < 0 && SelBoxMin <= (Input.mousePosition - mouseDownPos[0]).sqrMagnitude && mouseDownPos[0].y > Screen.height * g.uiBarHeight) {
			selectBox.setRect (mouseDownPos[0], Input.mousePosition, SelectBoxDepth);
			selectBox.line.enabled = true;
		}
		else {
			selectBox.line.enabled = false;
		}
	}
	
	void OnGUI() {
		int i;
		GUI.skin.button.fontSize = lblStyle.fontSize;
		GUI.skin.textField.fontSize = lblStyle.fontSize;
		// text at top left
		GUILayout.BeginArea (new Rect(0, 0, Screen.width, Screen.height * (1 - g.uiBarHeight)));
		if (!g.synced) {
			lblStyle.normal.textColor = Color.red;
			GUILayout.Label ("OUT OF SYNC", lblStyle);
			lblStyle.normal.textColor = Color.white;
		}
		GUILayout.Label ((timeGame >= g.timeSim) ? "LIVE" : "TIME TRAVELING", lblStyle);
		if (paused) GUILayout.Label ("PAUSED", lblStyle);
		if (Environment.TickCount < timeSpeedChg) timeSpeedChg -= UInt32.MaxValue;
		if (Environment.TickCount < timeSpeedChg + 1000) GUILayout.Label ("SPEED: " + Math.Pow(2, speed) + "x", lblStyle);
		if (g.players[selPlayer].timeGoLiveFail != long.MaxValue) {
			lblStyle.normal.textColor = Color.red;
			GUILayout.Label ("ERROR: Going live may cause you to have negative resources " + (timeGame - g.players[selPlayer].timeNegRsc) / 1000 + " second(s) ago.", lblStyle);
		}
		// text at bottom left
		GUILayout.FlexibleSpace ();
		for (i = 0; i < g.nRsc; i++) {
			long rscMin = (long)Math.Floor(FP.toDouble(g.playerResource(selPlayer, timeGame, i, false, true, false)));
			long rscMax = (long)Math.Floor(FP.toDouble(g.playerResource(selPlayer, timeGame, i, true, true, false)));
			lblStyle.normal.textColor = (rscMin >= 0) ? Color.white : Color.red;
			GUILayout.Label (g.rscNames[i] + ": " + rscMin + ((rscMax != rscMin) ? " to " + rscMax : ""), lblStyle);
		}
		GUILayout.EndArea ();
		// TODO: formation buttons
		// TODO: timeline
		// TODO: mini map
		// command menu
		// TODO: show text or hide button if can't do any of these actions
		GUI.Box (new Rect(0, Screen.height * (1 - g.uiBarHeight), Screen.width / 2, Screen.height * g.uiBarHeight), new GUIContent());
		GUILayout.BeginArea (new Rect(0, Screen.height * (1 - g.uiBarHeight), Screen.width / 2, Screen.height * g.uiBarHeight));
		unitMenuScrollPos = GUILayout.BeginScrollView (unitMenuScrollPos);
		if (selUnits.Count > 0) {
			string plural = (selUnits.Count == 1) ? "" : "s";
			if (unitMenu == UnitMenu.Main) {
				if (GUILayout.Button ("New Path" + plural)) makePaths ();
				if (GUILayout.Button ("Delete Path" + plural)) deletePaths ();
				if (GUILayout.Button ("Delete Other Paths")) deleteOtherPaths ();
				if (GUILayout.Button ("Make Unit")) unitMenu = UnitMenu.MakeUnit;
			}
			else if (unitMenu == UnitMenu.MakeUnit) {
				if (GUILayout.Button ("Back")) {
					unitMenu = UnitMenu.Main;
				}
				for (i = 0; i < g.nUnitT; i++) {
					foreach (int unit in selUnits) {
						if (g.u[unit].exists (timeGame) && g.unitT[g.u[unit].type].canMake[i]) {
							if (GUILayout.Button ("Make " + g.unitT[i].name)) makeUnit (i);
							break;
						}
					}
				}
			}
		}
		GUILayout.EndScrollView ();
		GUILayout.EndArea ();
		// unit selection bar
		GUI.Box (new Rect(Screen.width / 2, Screen.height * (1 - g.uiBarHeight), Screen.width, Screen.height * g.uiBarHeight), new GUIContent());
		GUILayout.BeginArea (new Rect(Screen.width / 2, Screen.height * (1 - g.uiBarHeight), Screen.width / 2, Screen.height * g.uiBarHeight));
		selUnitsScrollPos = GUILayout.BeginScrollView (selUnitsScrollPos);
		foreach (KeyValuePair<int, int> item in selRootParentPaths()) {
			if (GUILayout.Button (g.unitT[g.u[item.Key].type].name + (item.Value != 1 ? " (" + item.Value + " paths)" : ""))) {
				if (Event.current.button == 0) { // left button
					// select unit
					for (i = 0; i < selUnits.Count; i++) {
						if (g.u[selUnits[i]].rootParentPath () != item.Key) {
							selUnits.RemoveAt (i);
							i--;
						}
					}
				}
				else if (Event.current.button == 1) { // right button
					// deselect unit
					for (i = 0; i < selUnits.Count; i++) {
						if (g.u[selUnits[i]].rootParentPath () == item.Key) {
							selUnits.RemoveAt (i);
							i--;
						}
					}
				}
			}
		}
		GUILayout.EndScrollView ();
		GUILayout.EndArea ();
		// multiplayer GUI
		// TODO: implement main menu and move this there
		GUILayout.BeginArea (new Rect(0, Screen.height / 3, lblStyle.fontSize * 10, Screen.height));
		if (Network.peerType == NetworkPeerType.Disconnected) {
			serverAddr = GUILayout.TextField (serverAddr);
			serverPort = int.Parse (GUILayout.TextField (serverPort.ToString ()));
			if (GUILayout.Button ("Connect as Client")) {
				Network.Connect (serverAddr, serverPort);
			}
			if (GUILayout.Button ("Start Server")) {
				Network.InitializeServer (g.nUsers - 1, serverPort, !Network.HavePublicAddress ());
			}
		}
		else {
			if (GUILayout.Button ("Disconnect")) {
				Network.Disconnect (200);
			}
		}
		GUILayout.EndArea ();
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
	void allCmdsSent(int user, int checksum) {
		g.users[user].timeSync += g.updateInterval;
		g.users[user].checksums[g.users[user].timeSync] = checksum;
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
	/// returns dictionary of existing selected units' root parent paths (keys) and how many of their paths are selected (values)
	/// </summary>
	private Dictionary<int, int> selRootParentPaths() {
		Dictionary<int, int> ret = new Dictionary<int, int>();
		int rootParentPath;
		foreach (int unit in selUnits) {
			if (g.u[unit].exists (timeGame)) {
				rootParentPath = g.u[unit].rootParentPath ();
				if (!ret.ContainsKey (rootParentPath)) ret.Add (rootParentPath, 0);
				ret[rootParentPath]++;
			}
		}
		return ret;
	}

	/// <summary>
	/// returns where to make new unit, or (Sim.OffMap, 0) if mouse is at invalid position
	/// </summary>
	private FP.Vector makeUnitPos() {
		FP.Vector vec;
		if (FP.rectContains (new FP.Vector(), new FP.Vector(g.mapSize, g.mapSize), drawToSimPos(Input.mousePosition))) {
			if (g.unitT[makeUnitType].makeOnUnitT >= 0) {
				// selected unit type must be made on top of another unit of correct type
				// TODO: prevent putting multiple units on same unit (unless on different paths of same unit and maybe some other cases)
				for (int i = 0; i < g.nUnits; i++) {
					if (g.u[i].exists(timeGame)) {
						vec = g.u[i].calcPos(timeGame);
						if (g.u[i].type == g.unitT[makeUnitType].makeOnUnitT && g.tileAt(vec).playerVisWhen(selPlayer, timeGame)
							&& FP.rectContains (vec + g.unitT[g.u[i].type].selMinPos, vec + g.unitT[g.u[i].type].selMaxPos, drawToSimPos (Input.mousePosition))) {
							return vec;
						}
					}
				}
			}
			else {
				return drawToSimPos(Input.mousePosition);
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
	/// creates new paths that selected units could take
	/// </summary>
	private void makePaths() {
		if (selUnits.Count > 0) {
			FP.Vector[] pos = new FP.Vector[selUnits.Count];
			for (int i = 0; i < selUnits.Count; i++) {
				if (g.u[selUnits[i]].exists(timeGame + 1)) pos[i] = makeUnitMovePos(timeGame + 1, selUnits[i]);
			}
			// happens at newCmdTime() + 1 so new path starts out live if game is live
			g.cmdPending.add(new MakePathCmdEvt(g.timeSim, newCmdTime() + 1, selUnits.ToArray(), pos));
		}
	}
	
	/// <summary>
	/// deletes selected paths
	/// </summary>
	private void deletePaths() {
		// happens at newCmdTime() instead of newCmdTime() + 1 so that when paused, making path then deleting parent path doesn't cause an error
		if (selUnits.Count > 0) g.cmdPending.add(new DeletePathCmdEvt(g.timeSim, newCmdTime(), selUnits.ToArray()));
	}
	
	/// <summary>
	/// deletes unselected paths of selected units
	/// </summary>
	private void deleteOtherPaths() {
		Dictionary<int, int> parentPaths = selRootParentPaths ();
		List<int> otherPaths = new List<int>();
		for (int i = 0; i < g.nUnits; i++) {
			if (g.u[i].exists (timeGame) && !selUnits.Contains (i) && parentPaths.ContainsKey (g.u[i].rootParentPath ())) {
				otherPaths.Add (i);
			}
		}
		if (otherPaths.Count > 0) g.cmdPending.add (new DeletePathCmdEvt(g.timeSim, newCmdTime (), otherPaths.ToArray ()));
	}
	
	/// <summary>
	/// makes a new unit using selected units
	/// </summary>
	private void makeUnit(int type) {
		foreach (int unit in selUnits) {
			if (g.u[unit].canMakeChildUnit(timeGame + 1, false, type)) {
				if (g.unitT[type].speed > 0 && g.unitT[type].makeOnUnitT < 0) {
					// make unit now
					int[] unitArray = new int[1];
					unitArray[0] = unit;
					// happens at newCmdTime() + 1 so new unit starts out live if game is live
					g.cmdPending.add(new MakeUnitCmdEvt(g.timeSim, newCmdTime() + 1, unitArray, type, makeUnitMovePos(timeGame + 1, unit)));
				}
				else {
					// don't make unit yet; let user pick where to place it
					makeUnitType = type;
				}
				break;
			}
		}
	}

	/// <summary>
	/// sets pos to where base of unit should be drawn at, and returns whether it should be drawn
	/// </summary>
	private bool unitDrawPos(int unit, ref Vector3 pos) {
		FP.Vector fpVec;
		if (!g.u[unit].exists(timeGame) || (selPlayer != g.u[unit].player && !g.u[unit].isLive(timeGame))) return false;
		fpVec = g.u[unit].calcPos(timeGame);
		if (selPlayer != g.u[unit].player && !g.tileAt(fpVec).playerVisWhen(selPlayer, timeGame)) return false;
		pos = simToDrawPos(fpVec, UnitDepth);
		return true;
	}
	
	/// <summary>
	/// returns localScale of unit sprite with specified properties
	/// </summary>
	private Vector3 unitScale(int type, int player) {
		return new Vector3(simToDrawScl (g.unitT[type].imgHalfHeight) * texUnits[type, player].width / texUnits[type, player].height,
			simToDrawScl (g.unitT[type].imgHalfHeight), 1);
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
	
	private float simToDrawScl(long coor) {
		return (float)(FP.toDouble(coor) * g.zoom * winDiag);
	}

	private long drawToSimScl(float coor) {
		return FP.fromDouble(coor / winDiag / g.zoom);
	}

	private Vector3 simToDrawScl(FP.Vector vec) {
		return new Vector3(simToDrawScl(vec.x), simToDrawScl(vec.y), simToDrawScl(vec.z));
	}

	private FP.Vector drawToSimScl(Vector3 vec) {
		return new FP.Vector(drawToSimScl(vec.x), drawToSimScl(vec.y), drawToSimScl(vec.z));
	}

	private Vector3 simToDrawPos(FP.Vector vec, float depth = 0f) {
		return new Vector3(simToDrawScl(vec.x - g.camPos.x) + Screen.width / 2, simToDrawScl(vec.y - g.camPos.y) + Screen.height / 2, depth);
	}

	private FP.Vector drawToSimPos(Vector3 vec) {
		return new FP.Vector(drawToSimScl(vec.x - Screen.width / 2), drawToSimScl(vec.y - Screen.height / 2)) + g.camPos;
	}
}
