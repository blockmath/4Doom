using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

class MDMFException : System.Exception {
    public MDMFException() {}
    public MDMFException(string message) : base(message) {}
    public MDMFException(string message, Exception inner) : base(message, inner) {}
}

class ParseException : MDMFException {
    public ParseException() {}
    public ParseException(string message, int line) { base("Parse error at line " + line.ToString() + ":" + message); }
    public ParseException(string message, Exception inner) : base(message, inner) {}
}

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
    public Dictionary<System.Type,object> Fields;
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

    public static Dictionary<string,string> Defines = new Dictionary<string, string>();

    public static bool StrictParse = true;

    void Awake() {
        if (singleton is null) {
            singleton = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }
    }

    public static List<string> FileLoad(string file) {
        string fileContents;
        using (StreamReader sr = new StreamReader(file, Encoding.UTF8)) {
            fileContents = sr.ReadToEnd();
        }
        List<string> loadingFile = new List<string>(fileContents.Split("\n"));
        Parallel.For(0, loadingFile.Length, i => {
            if (loadingFile[i].IndexOf("##") != -1) loadingFile[i] = loadingFile[i].Substring(0, loadingFile[i].IndexOf("##") - 1);
            if (loadingFile[i].IndexOf("//") != -1) loadingFile[i] = loadingFile[i].Substring(0, loadingFile[i].IndexOf("//") - 1);
            loadingFile[i] = loadingFile[i].Trim();
            loadingFile[i] = Regex.Replace(loadingFile[i], @"\s+", " ", RegexOptions.CultureInvariant);
        });
        loadingFile.RemoveAll(line => string.IsNullOrEmpty(line));
        return loadingFile;
    }

    public static void Load(string file) {
        
    }

    public static void Parse() {
        if (file[0].Split()[0] != "#Format") {
            throw new ParseException("File does not declare a #Format. If you are importing a 4D Golf track, prepend `#Format 4DGTrack` to the file before attempting to load.", 1);
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

    public static Dictionary<string,int> FindMDMFSegmentsAll(List<string> file = MDMFLoader.file) {
        Dictionary<string,int> segments = new Dictionary<string,int>();
        int i = 1;
        for (;;i++) {
            if (i >= file.Length) {
                if (StrictParse) {
                    throw new ParseException("Expected '#Segment End' or further data, found end-of-file.", i);
                } else {
                    break;
                }
            }
            if (file[i] == "#Segment End") {
                break;
            }
            if (file[i].Split()[0] == "#Segment") {
                if (file[i].Split()[1][0] != "[") {
                    throw new ParseException("Expected '[', found '" + file[i].Split()[1] + "'", i);
                }
                string segmentType = file.Substring(file[i].IndexOf("[") + 1, file[i].IndexOf("]") - file[i].IndexOf("[") - 1);
                foreach (string st in segmentType.Split(",")) {
                    segments.Add(st.Trim(), i);
                }
            }
        }
        return segments;
    }

    public static int FindMDMFSegment(string segmentType, List<string> file = MDMFLoader.file) {
        return FindMDMFSegmentsAll(file)[segmentType];
    }

    public static List<string> SegmentLoad(string segmentFilePath) {
        int pivot = segmentFilePath.IndexOf("::");
        string filePath = segmentFilePath.Substring(0, pivot);
        string segmentType = segmentFilePath.Substring(pivot + 2);
        List<string> segmentFile = (filePath == "")? file : FileLoad(filePath);
        int segmentBegin = FindMDMFSegment(segmentType, segmentFile);
        int i;
        for (i = segmentBegin + 1; i < segmentFile.Length; i++) {
            if (segmentFile[i].Split()[0] == "#Segment") {
                break;
            }
        }
        return segmentFile.GetRange(segmentBegin + 1, i - (segmentBegin + 1) - 1);
    }

    public static string DelimitTokensSpaces(string line) {
        string processedLine = line;
        processedLine = Regex.Replace(processedLine, "{:", "??1", RegexOptions.CultureInvariant);
        processedLine = Regex.Replace(processedLine, ":}", "??2", RegexOptions.CultureInvariant);
        processedLine = Regex.Replace(processedLine, @"([,{}[\]<>:])", "  \1", RegexOptions.CultureInvariant);
        processedLine = Regex.Replace(processedLine, @"\?\?1", "{:", RegexOptions.CultureInvariant);
        processedLine = Regex.Replace(processedLine, @"\?\?2", ":}", RegexOptions.CultureInvariant);
        return processedLine;
    }

    // Space-delimit tokens, replace defined constants, and tokenize a line
    public static List<string> MDMFLinePreprocess(string line) {
        string processedLine = line;
        processedLine = " " + DelimitTokensSpaces(processedLine) + " ";
        foreach (string sym in Defines.Keys) {
            processedLine = Regex.Replace(processedLine, " " + sym + " ", " " + Defines[sym] + " ", RegexOptions.CultureInvariant);
        }
        processedLine = DelimitTokensSpaces(processedLine);
        processedLine = processedLine.Trim();
        processedLine = Regex.Replace(processedLine, @"\s+", " ", RegexOptions.CultureInvariant);
        return processedLine.Split();
    }

    public static void ParseMDMFSegment(int segSt) {
        string segmentType = file[segSt].Split()[1];
        switch (segmentType) {
            case "Defines": {
                Defines.Clear();
                for (int i = segSt + 1;; i++) {
                    if (i >= file.Length) {
                        if (StrictParse) {
                            throw new ParseException("Expected '#Segment' or further data, found end-of-file.", i);
                        } else {
                            break;
                        }
                    }
                    if (file[i].Split()[0] == "#Segment") {
                        break;
                    }
                    if (file[i].Split()[0] != "#define") {
                        if (StrictParse) {
                            //a '" + file[i].Split()[0] + "'? In the '#define' factory? how queer!! ive never seen such a thing -- i must inquire about this further with the developer post-haste!
                            throw new ParseException("In segment 'Defines': Expected '#define', found '" + file[i].Split()[0] + "'", i);
                        } else {
                            //i guess we doin '" + file[i].Split()[0] + "' now
                        }
                    } else {
                        List<string> line = MDMFLinePreprocess(file[i]);
                        string processedLine = "";
                        for (int j = 2; j < line.Length; j++) {
                            processedLine += line[j];
                            if (j != line.Length - 1) processedLine += " ";
                        }
                        Defines.Add(line[1], processedLine);
                    }
                }
                break;
            }
            case "TextDefs": {
                break;
            }
            case "Scripts": {
                break;
            }
            case "Lines": {
                break;
            }
            case "Sectors": {
                break;
            }
        }
    }

    public static void ParseStrippedMDMF() {
        // Load metadata -- all directives before the first #Segment directive
        int i = 1;
        for (;;i++) {
            if (i >= file.Length) {
                throw new ParseException("File has no segments. Aborting.", i);
            }
            if (file[i].Split()[0] == "#Segment") {
                break;
            }
            switch (file[i].Split()[0]) {
                case "#Theme":
                    Theme = file[i].Split()[1];
                    break;
                case "#Name": // Should probably store this in the future for display on pause menu, etc... currently don't see a use besides viewing the name in a list, which would be parsed by a different function
                    break;
            }
        }
        // Imports are recursive -- hack fix here. Depth limited to 32 imports to prevent infinite loops. Note that you can still crash the parser outright if you make a fork bomb (so don't do that)
        List<int> allImports = new List<int>();
        for(int l = 0; l < 32; l++) {
            // Find all imports
            allImports.Clear();
            for (i = 1; i < file.Length; i++) {
                if (file[i].Split()[0] == "#import" || file[i].Split()[0] == "#include") {
                    allImports.Add(i);
                }
            }
            if (allImports.Length == 0) {
                break;
            }
            // Iterate through the import list *backwards*, ensuring that massive amounts of added lines do not invalidate discovered import indices
            allImports = allImports.OrderByDescending(i => i);
            foreach (int i in allImports) {
                string importSource = file[i].Split()[1];
                file.RemoveAt(i);
                List<string> importFile = SegmentLoad(importSource);
                importFile.Reverse();
                foreach (string line in importFile) {
                    file.Insert(i, line);
                }
            }
        }
        if (allImports.Length != 0) {
            throw new ParseException("Maximum import depth (32) exceeded. Aborting.", i);
        }
        Dictionary<string,int> segments = FindMDMFSegmentsAll(file);
        
        if ("Defines" in segments.Keys) {
            ParseMDMFSegment(segments["Defines"]);
        }
        if ("TextDefs" in segments.Keys) {
            ParseMDMFSegment(segments["TextDefs"]);
        }
        if ("Scripts" in segments.Keys) {
            ParseMDMFSegment(segments["Scripts"]);
        }
        if ("Lines" in segments.Keys) {
            ParseMDMFSegment(segments["Lines"]);
        }
        if ("Sectors" in segments.Keys) {
            ParseMDMFSegment(segments["Sectors"]);
        } else {
            if (StrictParse) {
                throw new ParseException("File has no defined sectors. Aborting.", i);
            }
        }
    }
}