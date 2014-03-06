// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ProtoBuf;

/// <summary>
/// contains initialization and user interface code
/// </summary>
public class App : MonoBehaviour {
	private class LineBox {
		public LineRenderer line;
		
		public LineBox() {
			line = new GameObject().AddComponent<LineRenderer>();
			line.material.shader = Shader.Find ("VertexLit");
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
			Destroy(line.gameObject);
		}
	}
	
	private class MoveLineRenderer {
		public GameObject gameObject;
		public Mesh mesh;
		
		public MoveLineRenderer(Color color) {
			gameObject = new GameObject();
			gameObject.AddComponent<MeshFilter>();
			gameObject.AddComponent<MeshRenderer>();
			gameObject.renderer.material.shader = Shader.Find ("VertexLit");
			gameObject.renderer.material.color = color;
			mesh = new Mesh();
			gameObject.GetComponent<MeshFilter>().mesh = mesh;
		}
		
		/// <remarks>call mesh.Clear() before calling this</remarks>
		public void draw(List<MoveLine> moveLines, App app, long time, float depth) {
			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();
			// ISSUE #16: make width, fade interval, color customizable by mod
			foreach (MoveLine moveLine in moveLines) {
				if (moveLine.player == app.selPlayer && time - moveLine.time >= 0 && time - moveLine.time < 500) {
					for (int i = 0; i < moveLine.vertices.Count; i += 2) {
						Vector3 posStart = app.simToDrawPos (moveLine.vertices[i], depth);
						Vector3 posEnd = app.simToDrawPos (moveLine.vertices[i + 1], depth);
						if (posStart == posEnd) continue;
						Vector3 offset = 2 * (1 - (time - moveLine.time) / 500f) * new Vector3(posStart.y - posEnd.y, posEnd.x - posStart.x, 0) / Vector3.Distance (posStart, posEnd);
						vertices.Add (posStart - offset);
						vertices.Add (posStart + offset);
						vertices.Add (posEnd - offset);
						vertices.Add (posEnd + offset);
						triangles.Add (vertices.Count - 4);
						triangles.Add (vertices.Count - 3);
						triangles.Add (vertices.Count - 2);
						triangles.Add (vertices.Count - 1);
						triangles.Add (vertices.Count - 2);
						triangles.Add (vertices.Count - 3);
					}
				}
			}
			if (vertices.Count > 0) {
				mesh.vertices = vertices.ToArray ();
				mesh.triangles = triangles.ToArray ();
				mesh.uv = new Vector2[vertices.Count];
				mesh.normals = new Vector3[vertices.Count];
			}
		}
	}
	
	private class UnitSprite {
		public GameObject sprite;
		public GameObject preview; // for showing unit at final position
		public GameObject healthBarBack;
		public GameObject healthBarFore;
		public LineRenderer pathLine;
		public UnitType type;
		public Player player;
		
		public UnitSprite(GameObject quadPrefab) {
			sprite = Instantiate(quadPrefab) as GameObject;
			preview = Instantiate(quadPrefab) as GameObject;
			healthBarBack = Instantiate(quadPrefab) as GameObject;
			healthBarFore = Instantiate(quadPrefab) as GameObject;
			pathLine = sprite.AddComponent<LineRenderer>();
			pathLine.material.shader = Shader.Find ("VertexLit");
			pathLine.SetVertexCount (2);
			type = null;
			player = null;
		}
		
		public void dispose() {
			Destroy (sprite);
			Destroy (preview);
			Destroy (healthBarBack);
			Destroy (healthBarFore);
		}
	}
	
	const bool EnableStacking = true;
	const double SelBoxMin = 100;
	const float FntSize = 1f / 40;
	const float TileDepth = 7;
	const float BorderDepth = 6;
	const float MoveLineDepth = 5;
	const float PathLineDepth = 4;
	const float UnitDepth = 3;
	const float HealthBarDepth = 2;
	const float SelectBoxDepth = 1;
	
	public GameObject quadPrefab;
	
	string appPath;
	string modPath = "mod/";
	float winDiag; // diagonal length of screen in pixels
	Texture2D texTile;
	GameObject sprTile;
	LineBox border;
	MoveLineRenderer deleteLines;
	MoveLineRenderer keepLines;
	Texture[,] texUnits;
	List<List<UnitSprite>> sprUnits;
	GameObject sprMakeUnit;
	LineBox selectBox;
	GUIStyle lblStyle;
	GUIStyle lblErrStyle;
	float uiBarTop;
	Vector2 cmdsScrollPos;
	Vector2 makeUnitScrollPos;
	Vector2 selUnitsScrollPos;
	Sim g;
	Player selPlayer;
	List<UnitSelection> selPaths;
	Dictionary<Path, List<Unit>> curSelPaths;
	Formation selFormation;
	UnitType makeUnitType;
	long timeNow;
	long timeDelta;
	long timeGame;
	bool replay;
	bool showDeletedUnits;
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
		ProtoBuf.Meta.RuntimeTypeModel.Default.Add (typeof(Color), false).SetSurrogate (typeof(ProtoColor));
		ProtoBuf.Meta.RuntimeTypeModel.Default.Add (typeof(Vector2), false).SetSurrogate (typeof(ProtoVector3));
		ProtoBuf.Meta.RuntimeTypeModel.Default.Add (typeof(Vector3), false).SetSurrogate (typeof(ProtoVector3));
		winDiag = new Vector2(Screen.width, Screen.height).magnitude;
		mouseDownPos = new Vector3[3];
		mouseUpPos = new Vector3[3];
		RenderSettings.ambientLight = Color.white;
		Camera.main.orthographicSize = Screen.height / 2;
		Camera.main.transform.position = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
		quadPrefab.renderer.material.shader = Shader.Find ("Transparent/VertexLit");
		quadPrefab.renderer.material.color = Color.white;
		quadPrefab.transform.rotation = new Quaternion(0, 1, 0, 0);
		sprTile = Instantiate (quadPrefab) as GameObject;
		border = new LineBox();
		border.line.SetWidth (2, 2); // ISSUE #16: make width customizable by mod
		deleteLines = new MoveLineRenderer(Color.red);
		keepLines = new MoveLineRenderer(Color.green);
		sprMakeUnit = Instantiate (quadPrefab) as GameObject;
		// ISSUE #16: make color and width customizable by mod
		selectBox = new LineBox();
		selectBox.line.material.color = Color.white;
		selectBox.line.SetWidth (2, 2);
		// ISSUE #16: make font, size, and color customizable by mod
		lblStyle = GUIStyle.none;
		lblStyle.fontSize = (int)(Screen.height * FntSize);
		lblStyle.normal.textColor = Color.white;
		lblErrStyle = new GUIStyle(lblStyle);
		lblErrStyle.normal.textColor = Color.red;
		if (!scnOpen (appPath + modPath + "scn.json", 0, false)) {
			Debug.LogError ("Scenario failed to load.");
		}
	}
	
	/// <summary>
	/// loads scenario from json file and returns whether successful
	/// </summary>
	private bool scnOpen(string path, int user, bool multiplayer) {
		Hashtable json;
		ArrayList jsonA;
		bool b = false;
		// ISSUE #17: in multiplayer games, host should load file & send data to other players, otherwise json double parsing may not match
		if (!File.Exists(path)) return false;
		json = (Hashtable)Procurios.Public.JSON.JsonDecode(File.ReadAllText(path), ref b);
		if (!b) return false;
		// base scenario
		g = new Sim {
			selUser = user,
			networkView = multiplayer ? networkView : null,
			events = new SimEvtList(),
			cmdPending = new SimEvtList(),
			cmdHistory = new SimEvtList(),
			checksum = 0,
			synced = true,
			timeSim = 0,
			timeUpdateEvt = long.MinValue,
			maxSpeed = 0,
			mapSize = jsonFP(json, "mapSize"),
			updateInterval = (long)jsonDouble(json, "updateInterval"),
			visRadius = jsonFP(json, "visRadius"),
			camSpeed = jsonFP(json, "camSpeed"),
			zoom = (float)jsonDouble(json, "zoom"),
			zoomMin = (float)jsonDouble(json, "zoomMin"),
			zoomMax = (float)jsonDouble(json, "zoomMax"),
			zoomSpeed = (float)jsonDouble (json, "zoomSpeed"),
			zoomMouseWheelSpeed = (float)jsonDouble (json, "zoomMouseWheelSpeed"),
			uiBarHeight = (float)jsonDouble (json, "uiBarHeight"),
			healthBarSize = jsonVector2(json, "healthBarSize"),
			healthBarYOffset = (float)jsonDouble(json, "healthBarYOffset"),
			backCol = jsonColor(json, "backCol"),
			borderCol = jsonColor(json, "borderCol"),
			noVisCol = jsonColor(json, "noVisCol"),
			playerVisCol = jsonColor(json, "playerVisCol"),
			unitVisCol = jsonColor(json, "unitVisCol"),
			exclusiveCol = jsonColor(json, "exclusiveCol"),
			pathCol = jsonColor(json, "pathCol"),
			healthBarBackCol = jsonColor(json, "healthBarBackCol"),
			healthBarFullCol = jsonColor(json, "healthBarFullCol"),
			healthBarEmptyCol = jsonColor(json, "healthBarEmptyCol"),
			rscNames = new string[0],
			players = new Player[0],
			unitT = new UnitType[0],
			units = new List<Unit>(),
			paths = new List<Path>(),
			deleteLines = new List<MoveLine>(),
			keepLines = new List<MoveLine>(),
		};
		if (g.updateInterval > 0) g.events.add(new UpdateEvt(0));
		g.camPos = jsonFPVector(json, "camPos", new FP.Vector(g.mapSize / 2, g.mapSize / 2));
		// resources
		jsonA = jsonArray(json, "resources");
		if (jsonA != null) {
			foreach (string rscName in jsonA) {
				Array.Resize(ref g.rscNames, g.rscNames.Length + 1);
				g.rscNames[g.rscNames.Length - 1] = rscName;
			}
		}
		// players
		jsonA = jsonArray(json, "players");
		if (jsonA != null) {
			foreach (Hashtable jsonO in jsonA) {
				Hashtable jsonO2 = jsonObject(jsonO, "startRsc");
				Player player = new Player {
					g = this.g,
					id = g.players.Length,
					name = jsonString(jsonO, "name"),
					isUser = jsonBool(jsonO, "isUser"),
					user = (int)jsonDouble(jsonO, "user"),
					startRsc = new long[g.rscNames.Length],
					hasNonLivePaths = false,
					timeGoLiveFail = long.MinValue,
				};
				for (int i = 0; i < g.rscNames.Length; i++) {
					player.startRsc[i] = (jsonO2 != null) ? jsonFP(jsonO2, g.rscNames[i]) : 0;
				}
				Array.Resize(ref g.players, g.players.Length + 1);
				g.players[g.players.Length - 1] = player;
			}
			foreach (Hashtable jsonO in jsonA) {
				ArrayList jsonA2 = jsonArray(jsonO, "mayAttack");
				Player player = g.playerNamed(jsonString(jsonO, "name"));
				player.mayAttack = new bool[g.players.Length];
				for (int i = 0; i < g.players.Length; i++) {
					player.mayAttack[i] = false;
				}
				if (jsonA2 != null) {
					foreach (string s in jsonA2) {
						if (g.playerNamed(s) != null) {
							player.mayAttack[g.playerNamed(s).id] = true;
						}
					}
				}
			}
			foreach (Player player in g.players) {
				player.immutable = player.calcImmutable();
			}
		}
		// unit types
		jsonA = jsonArray(json, "unitTypes");
		if (jsonA != null) {
			foreach (Hashtable jsonO in jsonA) {
				Hashtable jsonO2 = jsonObject(jsonO, "rscCost");
				Hashtable jsonO3 = jsonObject(jsonO, "rscCollectRate");
				UnitType unitT = new UnitType {
					id = g.unitT.Length,
					name = jsonString(jsonO, "name"),
					imgPath = jsonString(jsonO, "imgPath"),
					imgOffset = jsonFPVector (jsonO, "imgOffset"),
					imgHalfHeight = jsonFP (jsonO, "imgHalfHeight"),
					maxHealth = (int)jsonDouble(jsonO, "maxHealth"),
					speed = jsonFP(jsonO, "speed"),
					reload = (long)jsonDouble(jsonO, "reload"),
					range = jsonFP(jsonO, "range"),
					tightFormationSpacing = jsonFP(jsonO, "tightFormationSpacing"),
					seePrecedence = (int)jsonDouble (jsonO, "seePrecedence"),
					makeUnitMinDist = jsonFP(jsonO, "makeUnitMinDist"),
					makeUnitMaxDist = jsonFP(jsonO, "makeUnitMaxDist"),
					makePathMinDist = jsonFP(jsonO, "makePathMinDist"),
					makePathMaxDist = jsonFP(jsonO, "makePathMaxDist"),
					rscCost = new long[g.rscNames.Length],
					rscCollectRate = new long[g.rscNames.Length],
				};
				unitT.selMinPos = jsonFPVector (jsonO, "selMinPos", new FP.Vector(unitT.imgOffset.x - unitT.imgHalfHeight, unitT.imgOffset.y - unitT.imgHalfHeight));
				unitT.selMaxPos = jsonFPVector (jsonO, "selMaxPos", new FP.Vector(unitT.imgOffset.x + unitT.imgHalfHeight, unitT.imgOffset.y + unitT.imgHalfHeight));
				if (unitT.speed > g.maxSpeed) g.maxSpeed = unitT.speed;
				for (int i = 0; i < g.rscNames.Length; i++) {
					unitT.rscCost[i] = (jsonO2 != null) ? jsonFP(jsonO2, g.rscNames[i]) : 0;
					unitT.rscCollectRate[i] = (jsonO3 != null) ? jsonFP(jsonO3, g.rscNames[i]) : 0;
				}
				Array.Resize(ref g.unitT, g.unitT.Length + 1);
				g.unitT[g.unitT.Length - 1] = unitT;
			}
			foreach (Hashtable jsonO in jsonA) {
				Hashtable jsonO2 = jsonObject(jsonO, "damage");
				ArrayList jsonA2 = jsonArray(jsonO, "canMake");
				UnitType unitT = g.unitTypeNamed(jsonString(jsonO, "name"));
				unitT.makeOnUnitT = g.unitTypeNamed(jsonString(jsonO, "makeOnUnitT"));
				unitT.damage = new int[g.unitT.Length];
				for (int i = 0; i < g.unitT.Length; i++) {
					unitT.damage[i] = (jsonO2 != null) ? (int)jsonDouble(jsonO2, g.unitT[i].name) : 0;
				}
				unitT.canMake = new bool[g.unitT.Length];
				for (int i = 0; i < g.unitT.Length; i++) {
					unitT.canMake[i] = false;
				}
				if (jsonA2 != null) {
					foreach (string s in jsonA2) {
						if (g.unitTypeNamed(s) != null) {
							unitT.canMake[g.unitTypeNamed(s).id] = true;
						}
					}
				}
			}
		}
		// tiles
		g.tiles = new Tile[g.tileLen(), g.tileLen()];
		for (int i = 0; i < g.tileLen(); i++) {
			for (int j = 0; j < g.tileLen(); j++) {
				g.tiles[i, j] = new Tile(g, i, j);
			}
		}
		// units
		jsonA = jsonArray(json, "units");
		if (jsonA != null) {
			foreach (Hashtable jsonO in jsonA) {
				if (g.playerNamed(jsonString(jsonO, "player")) != null) {
					ArrayList jsonA2 = jsonArray (jsonO, "types");
					List<Unit> units = new List<Unit>();
					if (jsonA2 != null) {
						foreach (string type in jsonA2) {
							if (g.unitTypeNamed(type) != null) {
								Unit unit = new Unit(g, g.units.Count, g.unitTypeNamed(type), g.playerNamed(jsonString(jsonO, "player")));
								g.units.Add (unit);
								units.Add (unit);
							}
						}
					}
					g.paths.Add (new Path(g, g.paths.Count, units, (long)jsonDouble(jsonO, "startTime"),
						jsonFPVector(jsonO, "startPos", new FP.Vector((long)(UnityEngine.Random.value * g.mapSize), (long)(UnityEngine.Random.value * g.mapSize)))));
					Move move = g.paths.Last ().moves[0];
					g.events.add(new TileMoveEvt(move.timeStart, g.paths.Count - 1, (int)(move.vecStart.x >> FP.Precision), (int)(move.vecStart.y >> FP.Precision)));
				}
			}
		}
		g.nRootPaths = g.paths.Count;
		// start game
		loadUI ();
		timeGame = 0;
		timeSpeedChg = (long)(Time.time * 1000) - 1000;
		timeNow = (long)(Time.time * 1000);
		return true;
	}
	
	private void loadUI() {
		// users
		int nUsers = 0;
		foreach (Player player in g.players) {
			if (player.user >= nUsers) nUsers = player.user + 1;
		}
		g.users = new User[nUsers];
		for (int i = 0; i < g.users.Length; i++) {
			g.users[i] = new User();
		}
		// unit types
		texUnits = new Texture[g.unitT.Length, g.players.Length];
		for (int i = 0; i < g.unitT.Length; i++) {
			for (int j = 0; j < g.players.Length; j++) {
				if (!(texUnits[i, j] = loadTexture (appPath + modPath + g.players[j].name + '.' + g.unitT[i].imgPath))) {
					if (!(texUnits[i, j] = loadTexture (appPath + modPath + g.unitT[i].imgPath))) {
						Debug.LogWarning ("Failed to load " + modPath + g.players[j].name + '.' + g.unitT[i].imgPath);
					}
				}
			}
		}
		// tiles
		texTile = new Texture2D(g.tileLen (), g.tileLen (), TextureFormat.ARGB32, false);
		// units
		if (sprUnits != null) {
			foreach (List<UnitSprite> sprs in sprUnits) {
				foreach (UnitSprite spr in sprs) {
					spr.dispose ();
				}
			}
		}
		sprUnits = new List<List<UnitSprite>>();
		// miscellaneous
		Camera.main.backgroundColor = g.backCol;
		border.line.material.color = g.borderCol;
		uiBarTop = Screen.height * g.uiBarHeight;
		selPlayer = g.players[0];
		while (selPlayer.user != g.selUser) selPlayer = g.players[(selPlayer.id + 1) % g.players.Length];
		selPaths = new List<UnitSelection>();
		curSelPaths = new Dictionary<Path, List<Unit>>();
		selFormation = Formation.Tight;
		makeUnitType = null;
		replay = false;
		showDeletedUnits = false;
		paused = false;
		speed = 0;
	}
	
	private Texture2D loadTexture(string path) {
		if (!File.Exists (path)) return null;
		Texture2D tex = new Texture2D(0, 0);
		byte[] imgBytes = File.ReadAllBytes (path);
		tex.LoadImage (imgBytes);
		return tex;
	}
	
	/// <summary>
	/// Update is called once per frame
	/// </summary>
	void Update () {
		updateTime ();
		if (!replay) {
			selPlayer.updatePast (timeGame);
			g.update (timeGame);
		}
		updateInput ();
		draw ();
	}
	
	private void updateTime() {
		timeDelta = (long)(Time.time * 1000) - timeNow;
		timeNow += timeDelta;
		if (!paused) {
			long timeGameDelta;
			if (timeDelta > g.updateInterval && timeGame + timeDelta >= g.timeSim) {
				// cap time difference to a max amount
				timeGameDelta = g.updateInterval;
			}
			else {
				// normal speed
				timeGameDelta = timeDelta;
			}
			timeGame += (long)(timeGameDelta * Math.Pow(2, speed)); // adjust game speed based on user setting
			if (replay && timeGame >= g.timeSim) timeGame = g.timeSim - 1;
		}
		// don't increment time past latest time that commands were synced across network
		if (g.networkView != null && timeGame >= g.timeUpdateEvt + g.updateInterval) {
			foreach (User user in g.users) {
				if (user.timeSync < g.timeUpdateEvt + g.updateInterval) {
					timeGame = g.timeUpdateEvt + g.updateInterval - 1;
					break;
				}
			}
		}
	}
	
	private void updateInput() {
		// update current unit selection
		curSelPaths.Clear ();
		foreach (SegmentUnit segmentUnit in SegmentUnit.current (selSegmentUnits (), timeGame)) {
			if (!curSelPaths.ContainsKey (segmentUnit.segment.path)) curSelPaths[segmentUnit.segment.path] = new List<Unit>();
			if (!curSelPaths[segmentUnit.segment.path].Contains (segmentUnit.unit)) curSelPaths[segmentUnit.segment.path].Add (segmentUnit.unit);
		}
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
			if (mouseDownPos[0].y > uiBarTop) {
				if (makeUnitType != null) {
					// make unit
					FP.Vector pos = makeUnitPos();
					if (pos.x != Sim.OffMap) g.cmdPending.add(new MakeUnitCmdEvt(g.timeSim, newCmdTime(), UnitCmdEvt.argFromPathDict (curSelPaths), makeUnitType.id, pos));
					makeUnitType = null;
				}
				else {
					// select paths
					Vector3 mouseMinPos = new Vector3(Math.Min (mouseDownPos[0].x, Input.mousePosition.x), Math.Min (mouseDownPos[0].y, Input.mousePosition.y), 0);
					Vector3 mouseMaxPos = new Vector3(Math.Max (mouseDownPos[0].x, Input.mousePosition.x), Math.Max (mouseDownPos[0].y, Input.mousePosition.y), 0);
					bool deselect = false;
					if (!Input.GetKey (KeyCode.LeftControl) && !Input.GetKey (KeyCode.LeftShift)) {
						selPaths.Clear();
						curSelPaths.Clear();
					}
					foreach (Path path in g.paths) {
						if (selPlayer == path.player && timeGame >= path.moves[0].timeStart
							&& FP.rectIntersects (drawToSimPos (mouseMinPos), drawToSimPos (mouseMaxPos),
							path.selMinPos(timeGame), path.selMaxPos(timeGame))) {
							// ISSUE #18: if not all units in path are selected, select remaining units instead of deselecting path
							if (curSelPaths.ContainsKey (path)) {
								curSelPaths.Remove(path);
								deselect = true;
							}
							else {
								curSelPaths[path] = new List<Unit>(path.activeSegment(timeGame).units);
								foreach (Unit unit in curSelPaths[path]) {
									selPaths.Add (new UnitSelection(path, unit, timeGame));
								}
							}
							if (SelBoxMin > (Input.mousePosition - mouseDownPos[0]).sqrMagnitude) break;
						}
					}
					if (deselect) {
						selPaths.Clear ();
						foreach (KeyValuePair<Path, List<Unit>> path in curSelPaths) {
							foreach (Unit unit in path.Value) {
								selPaths.Add (new UnitSelection(path.Key, unit, timeGame));
							}
						}
					}
				}
			}
		}
		if (Input.GetMouseButtonUp (1)) { // right button up
			mouseUpPos[1] = Input.mousePosition;
			if (mouseDownPos[1].y > uiBarTop) {
				if (makeUnitType != null) {
					// cancel making unit
					makeUnitType = null;
				}
				else {
					int stackPath = -1;
					for (int i = 0; i < g.paths.Count; i++) {
						if (selPlayer == g.paths[i].player && timeGame >= g.paths[i].moves[0].timeStart
							&& FP.rectContains (g.paths[i].selMinPos(timeGame), g.paths[i].selMaxPos(timeGame), drawToSimPos (Input.mousePosition))) {
							stackPath = i;
							break;
						}
					}
					if (EnableStacking && stackPath >= 0) {
						// stack selected paths onto clicked path
						g.cmdPending.add (new StackCmdEvt(g.timeSim, newCmdTime (), UnitCmdEvt.argFromPathDict (curSelPaths), stackPath));
					}
					else {
						// move selected paths
						g.cmdPending.add(new MoveCmdEvt(g.timeSim, newCmdTime(), UnitCmdEvt.argFromPathDict (curSelPaths), drawToSimPos (Input.mousePosition), selFormation));
					}
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
				selPlayer = g.players[(selPlayer.id + 1) % g.players.Length];
			} while (selPlayer.user != g.selUser);
			selPaths.Clear();
			makeUnitType = null;
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
		if (Input.GetKeyDown (KeyCode.T)) {
			// tight formation
			setFormation (Formation.Tight);
		}
		if (Input.GetKeyDown (KeyCode.L)) {
			// loose formation
			setFormation (Formation.Loose);
		}
		if (Input.GetKeyDown (KeyCode.R) && !Input.GetKey (KeyCode.LeftShift)) {
			// ring formation
			setFormation (Formation.Ring);
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
		if (Input.GetKeyDown (KeyCode.D) && Input.GetKey (KeyCode.LeftShift)) {
			// delete unselected paths of selected units (alternate shortcut)
			deleteOtherPaths ();
		}
		if (EnableStacking && Input.GetKeyDown (KeyCode.S) && !Input.GetKey (KeyCode.LeftShift)) {
			// share selected paths
			sharePaths ();
		}
		/*for (KeyCode keyCode = KeyCode.Alpha1; keyCode <= KeyCode.Alpha9; keyCode++) {
			// set # units that other players may see on selected paths
			if (Input.GetKeyDown (keyCode)) {
				foreach (Path path in selPaths.Keys) {
					path.nSeeUnits = keyCode - KeyCode.Alpha1 + 1; // this doesn't sync across multiplayer
				}
			}
		}*/
		if (Input.GetKeyDown (KeyCode.R) && Input.GetKey (KeyCode.LeftShift)) {
			// instant replay
			instantReplay ();
		}
		if (Input.GetKeyDown (KeyCode.O) && Input.GetKey (KeyCode.LeftShift)) {
			// open binary game file
			using (FileStream file = File.OpenRead (appPath + modPath + "savegame.sav")) {
				g = Serializer.Deserialize<Sim>(file);
			}
			loadUI ();
			GC.Collect ();
		}
		if (Input.GetKeyDown (KeyCode.S) && Input.GetKey (KeyCode.LeftShift)) {
			// save binary game file
			using (FileStream file = File.Create (appPath + modPath + "savegame.sav")) {
				Serializer.Serialize(file, g);
			}
		}
		// move camera
		if (Input.GetKey (KeyCode.LeftArrow) || (Input.mousePosition.x == 0 && Screen.fullScreen)) {
			g.camPos.x -= g.camSpeed * timeDelta;
			if (g.camPos.x < 0) g.camPos.x = 0;
		}
		if (Input.GetKey (KeyCode.RightArrow) || (Input.mousePosition.x == Screen.width - 1 && Screen.fullScreen)) {
			g.camPos.x += g.camSpeed * timeDelta;
			if (g.camPos.x > g.mapSize) g.camPos.x = g.mapSize;
		}
		if (Input.GetKey (KeyCode.DownArrow) || (Input.mousePosition.y == 0 && Screen.fullScreen)) {
			g.camPos.y -= g.camSpeed * timeDelta;
			if (g.camPos.y < 0) g.camPos.y = 0;
		}
		if (Input.GetKey (KeyCode.UpArrow) || (Input.mousePosition.y == Screen.height - 1 && Screen.fullScreen)) {
			g.camPos.y += g.camSpeed * timeDelta;
			if (g.camPos.y > g.mapSize) g.camPos.y = g.mapSize;
		}
		// zoom camera
		if (Input.GetKey (KeyCode.PageUp)) {
			g.zoom /= (float)Math.Exp (g.zoomSpeed * timeDelta);
		}
		if (Input.GetKey (KeyCode.PageDown)) {
			g.zoom *= (float)Math.Exp (g.zoomSpeed * timeDelta);
		}
		if (Input.mousePosition.y > uiBarTop && Input.GetAxis ("Mouse ScrollWheel") != 0) {
			g.zoom *= (float)Math.Exp (g.zoomMouseWheelSpeed * Input.GetAxis ("Mouse ScrollWheel"));
		}
		if (g.zoom < g.zoomMin) g.zoom = g.zoomMin;
		if (g.zoom > g.zoomMax) g.zoom = g.zoomMax;
	}
	
	private void draw() {
		Vector3 vec = new Vector3();
		// visibility tiles
		// ISSUE #19: don't draw tiles off map
		for (int tX = 0; tX < g.tileLen(); tX++) {
			for (int tY = 0; tY < g.tileLen(); tY++) {
				Color col = g.noVisCol;
				if (g.tiles[tX, tY].playerVisWhen(selPlayer, timeGame)) {
					col += g.playerVisCol;
					if (g.tiles[tX, tY].playerDirectVisWhen(selPlayer, timeGame, !showDeletedUnits)) col += g.unitVisCol;
					if (Sim.EnableNonLivePaths && (!replay || showDeletedUnits) && g.tiles[tX, tY].exclusiveWhen(selPlayer, timeGame)) col += g.exclusiveCol;
				}
				texTile.SetPixel (tX, tY, col);
			}
		}
		texTile.Apply ();
		texTile.filterMode = FilterMode.Point;
		sprTile.renderer.material.mainTexture = texTile;
		sprTile.transform.position = simToDrawPos (new FP.Vector((g.tileLen () << FP.Precision) / 2, (g.tileLen () << FP.Precision) / 2), TileDepth);
		sprTile.transform.localScale = simToDrawScl (new FP.Vector((g.tileLen () << FP.Precision) / 2, (g.tileLen () << FP.Precision) / 2));
		// map border
		border.setRect (simToDrawPos (new FP.Vector()), simToDrawPos(new FP.Vector(g.mapSize, g.mapSize)), BorderDepth);
		// unit move lines
		deleteLines.mesh.Clear ();
		keepLines.mesh.Clear ();
		if (!replay || showDeletedUnits) {
			deleteLines.draw (g.deleteLines, this, timeGame, MoveLineDepth);
			keepLines.draw (g.keepLines, this, timeGame, MoveLineDepth);
		}
		// units
		for (int i = 0; i < g.paths.Count; i++) {
			Segment segment = g.paths[i].activeSegment (timeGame);
			bool showPathDeletedUnits = showDeletedUnits && selPlayer == g.paths[i].player;
			if (i == sprUnits.Count) sprUnits.Add (new List<UnitSprite>());
			if (segment != null) {
				while (sprUnits[i].Count < segment.units.Count + segment.deletedUnits.Count) sprUnits[i].Add (new UnitSprite(quadPrefab));
			}
			for (int j = 0; j < sprUnits[i].Count; j++) {
				sprUnits[i][j].sprite.renderer.enabled = false;
				sprUnits[i][j].preview.renderer.enabled = false;
				sprUnits[i][j].healthBarBack.renderer.enabled = false;
				sprUnits[i][j].healthBarFore.renderer.enabled = false;
				sprUnits[i][j].pathLine.enabled = false;
			}
			if (pathDrawPos(g.paths[i], ref vec)) {
				for (int j = 0; j < segment.units.Count + (showPathDeletedUnits ? segment.deletedUnits.Count : 0); j++) {
					bool deleted = j >= segment.units.Count;
					Unit unit = deleted ? segment.deletedUnits[j - segment.units.Count] : segment.units[j];
					if (sprUnits[i][j].type != unit.type || sprUnits[i][j].player != unit.player) {
						sprUnits[i][j].sprite.renderer.material.mainTexture = texUnits[unit.type.id, unit.player.id];
						sprUnits[i][j].preview.renderer.material.mainTexture = texUnits[unit.type.id, unit.player.id];
						sprUnits[i][j].pathLine.material.color = g.pathCol;
						sprUnits[i][j].type = unit.type;
						sprUnits[i][j].player = unit.player;
					}
					if (g.paths[i].timeSimPast == long.MaxValue) {
						sprUnits[i][j].sprite.renderer.material.color = new Color(1, 1, 1, 1);
					}
					else {
						sprUnits[i][j].sprite.renderer.material.color = new Color(1, 1, 1, 0.5f); // ISSUE #16: make transparency amount customizable
					}
					sprUnits[i][j].sprite.transform.position = vec + simToDrawScl (unit.type.imgOffset);
					sprUnits[i][j].sprite.transform.localScale = unitScale (unit.type, unit.player);
					sprUnits[i][j].sprite.renderer.enabled = true;
					for (int k = i + 1; k < g.paths.Count; k++) {
						Segment segment2 = g.paths[k].activeSegment (timeGame);
						if (segment2 != null && g.paths[i].speed == g.paths[k].speed && g.paths[i].player == g.paths[k].player
							&& (segment2.units.Contains (unit) || (showPathDeletedUnits && segment2.deletedUnits.Contains (unit)))) {
							// unit path line
							sprUnits[i][j].pathLine.SetPosition (0, new Vector3(vec.x, vec.y, PathLineDepth));
							sprUnits[i][j].pathLine.SetPosition (1, simToDrawPos (g.paths[k].calcPos(timeGame), PathLineDepth));
							sprUnits[i][j].pathLine.enabled = true;
							break;
						}
					}
					if (Input.GetKey (KeyCode.LeftShift) && curSelPaths.ContainsKey(g.paths[i])) {
						// show final position if holding shift
						sprUnits[i][j].preview.renderer.material.color = sprUnits[i][j].sprite.renderer.material.color;
						sprUnits[i][j].preview.transform.position = simToDrawPos(g.paths[i].moves.Last ().vecEnd + unit.type.imgOffset, UnitDepth);
						sprUnits[i][j].preview.transform.localScale = sprUnits[i][j].sprite.transform.localScale;
						sprUnits[i][j].preview.renderer.enabled = true;
					}
				}
			}
		}
		// unit to be made
		if (makeUnitType != null) {
			FP.Vector pos = makeUnitPos();
			sprMakeUnit.renderer.material.mainTexture = texUnits[makeUnitType.id, selPlayer.id];
			if (pos.x != Sim.OffMap) {
				sprMakeUnit.renderer.material.color = new Color(1, 1, 1, 1);
				sprMakeUnit.transform.position = simToDrawPos(pos + makeUnitType.imgOffset, UnitDepth);
			}
			else {
				sprMakeUnit.renderer.material.color = new Color(1, 1, 1, 0.5f); // ISSUE #16: make transparency amount customizable
				sprMakeUnit.transform.position = new Vector3(Input.mousePosition.x, Input.mousePosition.y, UnitDepth) + simToDrawScl (makeUnitType.imgOffset);
			}
			sprMakeUnit.transform.localScale = unitScale (makeUnitType, selPlayer);
			sprMakeUnit.renderer.enabled = true;
		}
		else {
			sprMakeUnit.renderer.enabled = false;
		}
		// health bars
		foreach (Path path in curSelPaths.Keys) {
			if (pathDrawPos(path, ref vec)) {
				Segment segment = path.activeSegment (timeGame);
				for (int j = 0; j < segment.units.Count; j++) {
					Unit unit = segment.units[j];
					if (curSelPaths[path].Contains (unit)) {
						float f = ((float)unit.healthWhen(timeGame)) / unit.type.maxHealth;
						float f2 = vec.y + simToDrawScl (unit.type.selMaxPos.y) + g.healthBarYOffset * winDiag;
						// background
						if (unit.healthWhen(timeGame) > 0) {
							sprUnits[path.id][j].healthBarBack.renderer.material.color = g.healthBarBackCol;
							sprUnits[path.id][j].healthBarBack.transform.position = new Vector3(vec.x + g.healthBarSize.x * winDiag * f / 2, f2, HealthBarDepth);
							sprUnits[path.id][j].healthBarBack.transform.localScale = new Vector3(g.healthBarSize.x * winDiag * (1 - f) / 2, g.healthBarSize.y * winDiag / 2, 1);
							sprUnits[path.id][j].healthBarBack.renderer.enabled = true;
						}
						// foreground
						sprUnits[path.id][j].healthBarFore.renderer.material.color = g.healthBarEmptyCol + (g.healthBarFullCol - g.healthBarEmptyCol) * f;
						sprUnits[path.id][j].healthBarFore.transform.position = new Vector3(vec.x + g.healthBarSize.x * winDiag * (f - 1) / 2, f2, HealthBarDepth);
						sprUnits[path.id][j].healthBarFore.transform.localScale = new Vector3(g.healthBarSize.x * winDiag * f / 2, g.healthBarSize.y * winDiag / 2, 1);
						sprUnits[path.id][j].healthBarFore.renderer.enabled = true;
					}
				}
			}
		}
		// select box (if needed)
		if (Input.GetMouseButton (0) && makeUnitType == null && SelBoxMin <= (Input.mousePosition - mouseDownPos[0]).sqrMagnitude && mouseDownPos[0].y > uiBarTop) {
			selectBox.setRect (mouseDownPos[0], Input.mousePosition, SelectBoxDepth);
			selectBox.line.enabled = true;
		}
		else {
			selectBox.line.enabled = false;
		}
	}
	
	void OnGUI() {
		GUI.skin.button.fontSize = lblStyle.fontSize;
		GUI.skin.textField.fontSize = lblStyle.fontSize;
		// text at top left
		GUILayout.BeginArea (new Rect(0, 0, Screen.width, Screen.height * (1 - g.uiBarHeight)));
		if (!g.synced) {
			GUILayout.Label ("OUT OF SYNC", lblErrStyle);
		}
		GUILayout.Label (replay ? "REPLAY" : (timeGame >= g.timeSim) ? "LIVE" : "TIME TRAVELING", lblStyle);
		if (paused) GUILayout.Label ("PAUSED", lblStyle);
		if (Environment.TickCount < timeSpeedChg) timeSpeedChg -= UInt32.MaxValue;
		if (Environment.TickCount < timeSpeedChg + 1000) GUILayout.Label ("SPEED: " + Math.Pow(2, speed) + "x", lblStyle);
		if (selPlayer.timeGoLiveFail != long.MinValue) {
			GUILayout.Label ("ERROR: Going live may cause you to have negative resources " + (timeGame - selPlayer.timeNegRsc) / 1000 + " second(s) ago.", lblErrStyle);
		}
		// text at bottom left
		GUILayout.FlexibleSpace ();
		for (int i = 0; i < g.rscNames.Length; i++) {
			long rscMin = (long)Math.Floor(FP.toDouble(selPlayer.resource(timeGame, i, false, true)));
			long rscMax = (long)Math.Floor(FP.toDouble(selPlayer.resource(timeGame, i, true, true)));
			GUILayout.Label (g.rscNames[i] + ": " + rscMin + ((rscMax != rscMin) ? " to " + rscMax : ""), (rscMin >= 0) ? lblStyle : lblErrStyle);
		}
		if (Sim.EnableNonLivePaths || replay) {
			timeGame = (long)GUILayout.HorizontalSlider (timeGame, 0, g.timeSim);
			uiBarTop = Screen.height - GUILayoutUtility.GetLastRect ().y;
		}
		GUILayout.EndArea ();
		if (replay) {
			// replay UI
			GUILayout.BeginArea (new Rect(Screen.width * 0.3f, Screen.height * (1 - g.uiBarHeight), Screen.width * 0.4f, Screen.height * g.uiBarHeight));
			showDeletedUnits = GUILayout.Toolbar (showDeletedUnits ? 1 : 0, new string[] {"They See", "You See"}) != 0;
			lblStyle.wordWrap = true;
			if (showDeletedUnits) {
				GUILayout.Label ("You see all paths that your units took.", lblStyle);
			}
			else {
				GUILayout.Label ("Other players only see the final paths your units took.\nYou plausibly deny that you cheated.", lblStyle);
			}
			lblStyle.wordWrap = false;
			GUILayout.EndArea ();
		}
		else {
			// cheat menu
			// ISSUE #21: show text or hide button if can't do any of these actions
			if (curSelPaths.Count > 0) {
				string plural = (curSelPaths.Count == 1) ? "" : "s";
				GUILayout.BeginArea (new Rect(0, Screen.height * (1 - g.uiBarHeight), Screen.width / 4, Screen.height * g.uiBarHeight), (GUIStyle)"box");
				GUI.color = Color.yellow;
				GUILayout.Label ("Cheat Panel", lblStyle);
				GUI.color = Color.white;
				cmdsScrollPos = GUILayout.BeginScrollView (cmdsScrollPos);
				GUI.color = Color.yellow;
				if (GUILayout.Button ("New Path" + plural)) makePaths ();
				if (GUILayout.Button ("Delete Path" + plural)) deletePaths ();
				if (GUILayout.Button ("Delete Other Paths")) deleteOtherPaths ();
				if (EnableStacking && GUILayout.Button ("Share Paths")) sharePaths ();
				GUI.color = Color.white;
				GUILayout.EndScrollView ();
				GUILayout.EndArea ();
			}
			// command menu
			GUILayout.BeginArea (new Rect(Screen.width / 4, Screen.height * (1 - g.uiBarHeight), Screen.width / 4, Screen.height * g.uiBarHeight), (GUIStyle)"box");
			if (curSelPaths.Count > 0) {
				string tooltip;
				int inFormation = (Event.current.type == EventType.MouseUp) ? -1 : (int)selFormation;
				int outFormation = GUILayout.Toolbar(inFormation, new string[] {"Tight", "Loose", "Ring"});
				if (inFormation != outFormation) setFormation ((Formation)outFormation);
				makeUnitScrollPos = GUILayout.BeginScrollView (makeUnitScrollPos);
				foreach (UnitType unitT in g.unitT) {
					foreach (Path path in curSelPaths.Keys) {
						if (path.canMakeUnitType (timeGame, unitT)) { // ISSUE #22: sometimes canMake check should use existing selected units in path
							bool enoughRsc = true;
							tooltip = "Costs ";
							for (int i = 0; i < g.rscNames.Length; i++) {
								tooltip += FP.toDouble (unitT.rscCost[i]) + " " + g.rscNames[i];
								if (i != g.rscNames.Length - 1) tooltip += ", ";
								enoughRsc &= selPlayer.resource(timeGame, i, false, true) >= unitT.rscCost[i]; // TODO: should sometimes pass in includeNonLiveChildren = false, see makePath(), maybe simpler way would be calling canMakePath()
							}
							if (!enoughRsc) tooltip += " ";
							if (GUILayout.Button (new GUIContent("Make " + unitT.name, tooltip))) makeUnit (unitT);
							break;
						}
					}
				}
				GUILayout.EndScrollView ();
			}
			GUILayout.EndArea ();
			// command menu tooltip
			GUI.Label (new Rect(Screen.width / 4, Screen.height - uiBarTop - lblStyle.fontSize, Screen.width / 4, lblStyle.fontSize), GUI.tooltip, GUI.tooltip.EndsWith (" ") ? lblErrStyle : lblStyle);
			// unit selection bar
			GUILayout.BeginArea (new Rect(Screen.width / 2, Screen.height * (1 - g.uiBarHeight), Screen.width / 2, Screen.height * g.uiBarHeight));
			selUnitsScrollPos = GUILayout.BeginScrollView (selUnitsScrollPos, "box");
			foreach (KeyValuePair<Unit, int> item in selUnits()) {
				if (GUILayout.Button (item.Key.type.name + (item.Value != 1 ? " (" + item.Value + " paths)" : ""))) {
					if (Event.current.button == 0) { // left button
						// select unit
						for (int i = 0; i < selPaths.Count; i++) {
							if (selPaths[i].unit != item.Key) {
								selPaths.RemoveAt (i);
								i--;
							}
						}
					}
					else if (Event.current.button == 1) { // right button
						// deselect unit
						for (int i = 0; i < selPaths.Count; i++) {
							if (selPaths[i].unit == item.Key) {
								selPaths.RemoveAt (i);
								i--;
							}
						}
					}
				}
			}
			GUILayout.EndScrollView ();
			GUILayout.EndArea ();
		}
		// multiplayer GUI
		// ISSUE #23: implement main menu and move this there
		GUILayout.BeginArea (new Rect(0, Screen.height / 3, lblStyle.fontSize * 10, Screen.height));
		if (Network.peerType == NetworkPeerType.Disconnected) {
			serverAddr = GUILayout.TextField (serverAddr);
			serverPort = int.Parse (GUILayout.TextField (serverPort.ToString ()));
			if (GUILayout.Button ("Connect as Client")) {
				Network.Connect (serverAddr, serverPort);
			}
			if (GUILayout.Button ("Start Server")) {
				Network.InitializeSecurity ();
				Network.InitializeServer (g.users.Length - 1, serverPort, !Network.HavePublicAddress ());
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
		if (Network.connections.Length == g.users.Length - 1) {
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
	
	// ISSUE #24: add NetworkMessageInfo as last parameter to authenticate user, according to http://forum.unity3d.com/threads/141156-Determine-sender-of-RPC
	[RPC]
	void nextTurnWithCmds(int user, byte[] cmdData, int checksum) {
		g.users[user].cmdReceived.events.AddRange (Serializer.Deserialize<SimEvtList>(new MemoryStream(cmdData)).events);
		nextTurn (user, checksum);
	}
	
	[RPC]
	void nextTurn(int user, int checksum) {
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
	
	private void instantReplay() {
		timeGame = 0;
		replay = true;
		selPaths.Clear ();
		foreach (Path path in g.paths) {
			foreach (Segment segment in path.segments) {
				g.deleteOtherPaths (segment.segmentUnits ());
			}
		}
		foreach (Tile tile in g.tiles) {
			for (int i = 0; i < g.players.Length; i++) {
				tile.playerVis[i] = new List<long> { 0 };
			}
		}
	}
	
	/// <summary>
	/// returns dictionary of selected units (keys) and how many of their paths are selected (values)
	/// </summary>
	private Dictionary<Unit, int> selUnits() {
		Dictionary<Unit, int> ret = new Dictionary<Unit, int>();
		foreach (List<Unit> units in curSelPaths.Values) {
			foreach (Unit unit in units) {
				if (!ret.ContainsKey (unit)) ret.Add (unit, 0);
				ret[unit]++;
			}
		}
		return ret;
	}
	
	private IEnumerable<SegmentUnit> selSegmentUnits() {
		foreach (UnitSelection selection in selPaths) {
			Segment segment = selection.path.activeSegment (selection.time);
			if (segment.units.Contains (selection.unit)) yield return new SegmentUnit(segment, selection.unit);
		}
	}

	/// <summary>
	/// returns where to make new unit, or (Sim.OffMap, 0) if mouse is at invalid position
	/// </summary>
	private FP.Vector makeUnitPos() {
		if (FP.rectContains (new FP.Vector(), new FP.Vector(g.mapSize, g.mapSize), drawToSimPos(Input.mousePosition))) {
			if (makeUnitType.makeOnUnitT != null) {
				// selected unit type must be made on top of another unit of correct type
				// ISSUE #25: prevent putting multiple units on same unit (unless on different paths of same unit and maybe some other cases)
				foreach (Path path in g.paths) {
					if (timeGame >= path.segments[0].timeStart) {
						FP.Vector pos = path.calcPos(timeGame);
						if (g.tileAt (pos).playerVisWhen (selPlayer, timeGame)
							&& FP.rectContains (path.selMinPos (timeGame), path.selMaxPos (timeGame), drawToSimPos (Input.mousePosition))) {
							foreach (Unit unit in path.activeSegment (timeGame).units) {
								if (unit.type == makeUnitType.makeOnUnitT) {
									return pos;
								}
							}
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
	/// returns where new unit of specified type can move out of the way after specified path makes it
	/// </summary>
	/// <remarks>chooses a random location between makeUnitMinDist and makeUnitMaxDist away from path</remarks>
	private FP.Vector makeUnitMovePos(long time, Path path, UnitType type) {
		FP.Vector ret;
		do {
			ret = new FP.Vector((long)((UnityEngine.Random.value - 0.5) * type.makeUnitMaxDist * 2),
				(long)((UnityEngine.Random.value - 0.5) * type.makeUnitMaxDist * 2));
		} while (ret.lengthSq() < type.makeUnitMinDist * type.makeUnitMinDist
			|| ret.lengthSq() > type.makeUnitMaxDist * type.makeUnitMaxDist);
		return ret + path.calcPos(time);
	}

	/// <summary>
	/// returns where new path with specified units can move out of the way after specified path makes it
	/// </summary>
	/// <remarks>chooses a random location between makePathMinDist() and makePathMaxDist() away from path</remarks>
	private FP.Vector makePathMovePos(long time, Path path, List<Unit> units) {
		long makePathMinDist = path.makePathMinDist (time, units);
		long makePathMaxDist = path.makePathMaxDist (time, units);
		FP.Vector ret;
		do {
			ret = new FP.Vector((long)((UnityEngine.Random.value - 0.5) * makePathMaxDist * 2),
				(long)((UnityEngine.Random.value - 0.5) * makePathMaxDist * 2));
		} while (ret.lengthSq() < makePathMinDist * makePathMinDist
			|| ret.lengthSq() > makePathMaxDist * makePathMaxDist);
		return ret + path.calcPos(time);
	}
	
	/// <summary>
	/// applies specified formation to selected paths
	/// </summary>
	private void setFormation(Formation formation) {
		selFormation = formation;
		if (curSelPaths.Count > 0) {
			long minX = g.mapSize, maxX = 0, minY = g.mapSize, maxY = 0;
			foreach (KeyValuePair<Path, List<Unit>> path in curSelPaths) {
				if (path.Key.canMove (timeGame) && path.Key.activeSegment (timeGame).units.Intersect (path.Value).Any ()) {
					FP.Vector pos = path.Key.calcPos(timeGame);
					if (pos.x < minX) minX = pos.x;
					if (pos.x > maxX) maxX = pos.x;
					if (pos.y < minY) minY = pos.y;
					if (pos.y > maxY) maxY = pos.y;
				}
			}
			g.cmdPending.add(new MoveCmdEvt(g.timeSim, newCmdTime(), UnitCmdEvt.argFromPathDict (curSelPaths), new FP.Vector((minX + maxX) / 2, (minY + maxY) / 2), selFormation));
		}
	}
	
	/// <summary>
	/// creates new paths that selected units could take
	/// </summary>
	private void makePaths() {
		if (curSelPaths.Count > 0) {
			Dictionary<int, FP.Vector> pos = new Dictionary<int, FP.Vector>();
			foreach (KeyValuePair<Path, List<Unit>> path in curSelPaths) {
				pos[path.Key.id] = makePathMovePos(timeGame, path.Key, path.Value);
			}
			g.cmdPending.add(new MakePathCmdEvt(g.timeSim, newCmdTime(), UnitCmdEvt.argFromPathDict(curSelPaths), pos));
		}
	}
	
	/// <summary>
	/// deletes selected paths
	/// </summary>
	private void deletePaths() {
		if (curSelPaths.Count > 0) g.cmdPending.add(new DeletePathCmdEvt(g.timeSim, newCmdTime(), UnitCmdEvt.argFromPathDict(curSelPaths)));
	}
	
	/// <summary>
	/// deletes unselected paths of selected units
	/// </summary>
	private void deleteOtherPaths() {
		if (curSelPaths.Count > 0) g.cmdPending.add (new DeleteOtherPathsCmdEvt(g.timeSim, newCmdTime (), UnitCmdEvt.argFromPathDict (curSelPaths)));
	}
	
	/// <summary>
	/// shares selected paths
	/// </summary>
	private void sharePaths() {
		if (!EnableStacking) throw new InvalidOperationException("may not share paths when stacking is disabled");
		if (curSelPaths.Count > 0) g.cmdPending.add (new SharePathsCmdEvt(g.timeSim, newCmdTime (), UnitCmdEvt.argFromPathDict (curSelPaths)));
	}
	
	/// <summary>
	/// makes a new unit using selected units
	/// </summary>
	private void makeUnit(UnitType type) {
		foreach (KeyValuePair<Path, List<Unit>> path in curSelPaths) {
			if (type.speed > 0 && type.makeOnUnitT == null && path.Key.canMakeUnitType (timeGame, type)) {
				// make unit now
				g.cmdPending.add(new MakeUnitCmdEvt(g.timeSim, newCmdTime(),
					UnitCmdEvt.argFromPathDict (new Dictionary<Path, List<Unit>> { { path.Key, path.Value } }),
					type.id, makeUnitMovePos (timeGame, path.Key, type)));
				break;
			}
			else if (g.unitsCanMake (path.Value, type)) {
				// don't make unit yet; let user pick where to place it
				makeUnitType = type;
				break;
			}
		}
	}

	/// <summary>
	/// sets pos to where base of path should be drawn at, and returns whether it should be drawn
	/// </summary>
	private bool pathDrawPos(Path path, ref Vector3 pos) {
		FP.Vector simPos;
		if (timeGame < path.moves[0].timeStart || (selPlayer != path.player && path.timeSimPast != long.MaxValue)) return false;
		simPos = path.calcPos(timeGame);
		if (selPlayer != path.player && !g.tileAt(simPos).playerVisWhen(selPlayer, timeGame)) return false;
		pos = simToDrawPos(simPos, UnitDepth);
		return true;
	}
	
	/// <summary>
	/// returns localScale of unit sprite with specified properties
	/// </summary>
	private Vector3 unitScale(UnitType type, Player player) {
		return new Vector3(simToDrawScl (type.imgHalfHeight) * texUnits[type.id, player.id].width / texUnits[type.id, player.id].height,
			simToDrawScl (type.imgHalfHeight), 1);
	}
	
	/// <summary>
	/// returns suggested timeCmd field for a new CmdEvt, corresponding to when it would appear to be applied
	/// </summary>
	private long newCmdTime() {
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
