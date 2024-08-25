using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class MDMFException : System.Exception {}

class ParseException : MDMFException {}

enum FieldTag {
    TagErr = -1,
    TagNone = 0,
    NoWall,
    BottomWall,
    TopWall,
    HoleWall,
    NoCollision,
    NoCeiling,
    NoFloor,
    ActorPasses,
    ActorTouches,
    TriggerOnce,
    TriggerMultiple,
    TriggerForwardOnly,
}

struct Autofield {
    public Dictionary<System.Type, object> Fields;
    public List<FieldTag> Tags;
}

class SectorBoundary {
    public Vector3 Kernel;
    public List<Vector3> Vertices;
    public List<Autofield> Triangles;
}

struct SectorTransform {
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

class Sector {
    public SectorBoundary Boundary;
    public SectorTransform Transform;
    public float CellFloor, CellHeight;
    public int SectorId;
    public Autofield Tags;
}

class PlaneDef {
    public List<Vector3> Vertices;
    public int SectorId;
    public Autofield Line;
}

struct ScriptInstruction {
    public List<string> Tokens;
}

class ScriptManager {
    public static Dictionary<int, Script> allScripts;
    public static Dictionary<Script, double> activeScripts;

    public static void RunScript(List<double> args) { // args[0] is the script ID
        allScripts[(int)args[0]].ScriptRun(args);
    }

    public static void RunScript(int script) {
        RunScript(new List<double>{script});
    }

    public static void Update() {
        bool didExecuteAnything = true;
        int i = 0;
        while (didExecuteAnything && i++ < 2048) {
            didExecuteAnything = false;
            foreach (Script s in activeScripts.Keys) {
                if (activeScripts[s] <= Time.time) {
                    s.ScriptTick();
                    didExecuteAnything = true;
                }
            }
        }
    }
}

class ScriptExecutionException : MDMFException {}

class Script {
    public List<ScriptInstruction> Contents;
    public int SectorId;

    public Script(int _SectorId) {
        this.SectorId = _SectorId;
        ScriptManager.allScripts.Add(_SectorId, this);
    }
    private int eip;
    private List<double> argv;
    public void ScriptRun(List<double> args) {
        Assert(args[0] == SectorId);
        if (execActive) {
            throw new ScriptExecutionException("Cannot run an already-running script");
        }
        argv = new List<double>(args);
        eip = 0;
        execActive = true;
        ScriptManager.activeScripts.Add(this, Time.time);
    }

    private bool execActive = false;
    public void ScriptTick() {
        if (!execActive) {
            ScriptManager.activeScripts.Remove(this);
        }
        ScriptInstruction instr = new ScriptInstruction { new List<string>(Contents[eip++]) };
        for (int i = 1; i < instr.Tokens.Length; i++) {
            // Replace parameters with their respective values
            if (instr.Tokens[i][0] == "$") {
                instr.Tokens[i] = argv[int.Parse(instr.Tokens[i].Substring(1))].ToString();
            }
        }
        switch (instr.Tokens[0]) {
            case "done":
                execActive = false;
                ScriptManager.activeScripts.Remove(this);
                return;
            case "Delay":
                if (double.Parse(instr[1]) <= 0.0) {
                    throw new ScriptExecutionException("Delay intervals must be positive");
                }
                ScriptManager.activeScripts[this] = Time.time + double.Parse(instr[1]);
                return;
            case "ScriptRun": {
                List<double> args = new List<double>();
                for (int i = 1; i < instr.Tokens.Length; i++) {
                    args.Add(double.Parse(instr.Tokens[i]));
                }
                ScriptManager.RunScript(args);
                return;
            }
        }
    }
}

struct MDMFToken {

}

struct MDMFLine {
    public List<MDMFToken> Tokens;
}

public class MDMFLoader : MonoBehaviour {
    public static MDMFLoader singleton;

    public static List<string> file;

    public static string Theme {get; private set;};

    void Awake() {
        if (singleton is null) {
            singleton = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }
    }

    public static void Load(string file) {
        
    }

    public static void Parse(string fileContents) {
        file = new List<string>(fileContents.Split("\n"));
        Parallel.For(0, file.Length, i => {
            if (file[i].IndexOf("##") != -1) file[i] = file[i].Substring(0, file[i].IndexOf("##"));
            if (file[i].IndexOf("//") != -1) file[i] = file[i].Substring(0, file[i].IndexOf("//"));
            file[i] = file[i].Trim();
        });
        file.RemoveAll(line => string.IsNullOrEmpty(line));
        if (file[0].Split()[0] != "#Format") {
            throw new ParseException("File does not declare a #Format. If you are importing a 4D Golf track, prepend `#Format 4DGTrack` to the file before attempting to load.");
        } else {
            string format = file[0].Split()[1];
            if (format == "MDMF") {
                ParseStrippedMDMF();
            } else if (format == "4DGTrack") {
                throw new NotImplementedException("Loading of 4D Golf tracks is not currently implemented, due to complexity of track pieces. Todo: this");
            } else {
                throw new NotImplementedException("The format '" + format + "' is not recognized by the engine.");
            }
        }
    }

    public static void ParseMDMFSegment(int segBegin, int segEnd) {

    }

    public static void ParseStrippedMDMF() {
        // Load metadata -- all directives before the first #Segment directive
        for (int i = 1;; i++) {
            if (i >= file.Length) {
                throw new ParseException("File has no segments. Aborting.");
            }
        }
    }
}