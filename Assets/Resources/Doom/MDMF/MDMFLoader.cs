using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
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

class ScriptExecutionException : MDMFException {}

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
    public Autofield Tags;
}

struct ScriptInstruction {
    public List<string> Tokens;

    ScriptInstruction(List<string> line) {
        Tokens = new List<string>(line);
    }
}

public class ScriptManager {
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
        // Scripts are limited to 2048 commands per frame
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
        ScriptInstruction instr = new ScriptInstruction(Contents[eip++]);
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

struct YieldObject {
    object _obj;
    int endl;
}

public struct LevelData {
    public List<Sector> Sectors;
    public Dictionary<int, PlaneDef> PlaneDefs;
}

public class MDMFLoader : MonoBehaviour {
    public static MDMFLoader singleton;

    public static List<string> file;

    public static string Theme {get; private set;};

    public static Dictionary<string,string> Defines = new Dictionary<string, string>();
    public static List<Sector> Sectors;
    public static Dictionary<int, Autofield> Lines;
    public static Dictionary<int, PlaneDef> PlaneDefs;
    public static LevelData resultData;

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

    public static LevelData Load(string _file) {
        file = FileLoad(_file);
        Parse();
        return resultData;
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
        processedLine = Regex.Replace(processedLine, @"([,{}[\]<>:%$])", "  \1 ", RegexOptions.CultureInvariant);
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

    public static YieldObject MDMFGetObject(List<string> _obj, int objSt, int ln) {
        YieldObject yield = new YieldObject();
        try if (_obj[objSt + 0] == "[" && Regex.Match(_obj[objSt + 1], "^[A-Za-z_][0-9A-Za-z_]*$", RegexOption.IgnoreCase | RegexOptions.CultureInvariant).Success && _obj[objSt + 2] == "]" && _obj[_objSt + 3] == "{") goto _ObjectFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _ObjectFactory:
            System.Type _type = Type.GetType(_obj[objSt + 1]);
            int _objSt = objSt + 3;
            if (StrictParse && _obj[_objSt] != "{") {
                if (_objSt >= _obj.Length) {
                    throw new ParseException("Expected '{', got end-of-line", ln);
                } else {
                    throw new ParseException("Expected '{', got '" + _obj[_objSt] + "'", ln);
                }
            }
            int _objEnd;
            int depth = 1;
            for (_objEnd = _objSt + 1; depth > 0; _objEnd++) {
                if (_obj[_objEnd] == "{") depth++;
                if (_obj[_objEnd] == "}") depth--;
                if (_objEnd >= _obj.Length) {
                    throw new ParseException("Expected '}', got end-of-line", ln);
                }
            }
            yield.endl = _objEnd;
            int i = _objSt + 1;
            // *inhales* REFLECTION
            yield._obj = (object)Activator.CreateInstance(_type);
            while (i < _objEnd - 1) {
                if (_obj[i] == ",") i++;
                if (i >= _objEnd || _obj[i+1] != ":") {
                    throw new ParseException("Expected ':', got '" + _obj[i+1] + "'", ln);
                }
                YieldObject parsedObj = MDMFGetObject(_obj, i + 2, ln);
                _type.GetField(_obj[i]).SetValue(yield._obj, (_type.GetField(_obj[i]).FieldType)parsedObj._obj);
                i = parsedObj.endl + 1;
            }
            return yield;
        } else try if (_obj[objSt + 0] == "[" && Regex.Match(_obj[objSt + 1], "^[A-Za-z_][0-9A-Za-z_]*$", RegexOption.IgnoreCase | RegexOptions.CultureInvariant).Success && _obj[objSt + 2] == "]" && _obj[_objSt + 3] == "[") goto _ListFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _ListFactory:
            System.Type _type = Type.GetType(_obj[objSt + 1]);
            int _objSt = objSt + 3;
            if (StrictParse && _obj[_objSt] != "[") {
                if (_objSt >= _obj.Length) {
                    throw new ParseException("Expected '[', got end-of-line", ln);
                } else {
                    throw new ParseException("Expected '[', got '" + _obj[_objSt] + "'", ln);
                }
            }
            int _objEnd;
            int depth = 1;
            for (_objEnd = _objSt + 1; depth > 0; _objEnd++) {
                if (_obj[_objEnd] == "[") depth++;
                if (_obj[_objEnd] == "]") depth--;
                if (_objEnd >= _obj.Length) {
                    throw new ParseException("Expected ']', got end-of-line", ln);
                }
            }
            yield.endl = _objEnd;
            int i = _objSt + 1;
            yield._obj = (object)(new List<_type>());
            while (i < _objEnd - 1) {
                if (_obj[i] == ",") i++;
                YieldObject parsedObj = MDMFGetObject(_obj, i, ln);
                ((List<_type>)yield._obj).Add((_type)parsedObj._obj);
                i = parsedObj.endl + 1;
            }
            return yield;
        } else try if (_obj[objSt + 0] == "{:") goto _AutofieldFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _AutofieldFactory:
            int _objSt = objSt;
            int _objEnd;
            int depth = 1;
            for (_objEnd = _objSt + 1; depth > 0; _objEnd++) {
                if (_obj[_objEnd] == "{:") depth++;
                if (_obj[_objEnd] == ":}") depth--;
                if (_objEnd >= _obj.Length) {
                    throw new ParseException("Expected ':}', got end-of-line", ln);
                }
            }
            yield.endl = _objEnd;
            int i = _objSt + 1;
            yield._obj = (object)(new Autofield());
            while (i < _objEnd - 1) {
                if (_obj[i] == ",") i++;
                if (_obj[i][0] == "#") {
                    FieldTag tag;
                    Enum.TryParse(_obj[i].Substring(1), out tag);
                    ((Autofield)yield._obj).Tags.Add(tag);
                    i = i + 1;
                } else {
                    YieldObject parsedObj = MDMFGetObject(_obj, i, ln);
                    System.Type _type = parsedObj.GetType();
                    ((Autofield)yield._obj).Fields.Add(_type, parsedObj._obj);
                    i = parsedObj.endl + 1;
                }
            }
            return yield;
        } else try if (_obj[objSt + 0] == "<") goto _VectorFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _VectorFactory:
            yield.endl = objSt + 6;
            yield._obj = (object)(new Vector3(double.Parse(_obj[objSt + 1]), double.Parse(_obj[objSt + 3]), double.Parse(_obj[objSt + 5])));
            return yield;
        } else try if (_obj[objSt + 0] == "%" && _obj[objSt + 1] == "<") goto _IntVectorFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _IntVectorFactory:
            yield.endl = objSt + 7;
            yield._obj = (object)(new Vector3Int(int.Parse(_obj[objSt + 2]), int.Parse(_obj[objSt + 4]), int.Parse(_obj[objSt + 6])));
            return yield;
        } else try if (_obj[objSt + 0] == "$" && _obj[objSt + 1] == "<") goto _QuaternionFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _QuaternionFactory:
            yield.endl = objSt + 7;
            yield._obj = (object)(Quaternion.Euler(double.Parse(_obj[objSt + 2]), double.Parse(_obj[objSt + 4]), double.Parse(_obj[objSt + 6])));
            return yield;
        } else try if (_obj[objSt + 0][0] == "#") goto _TagFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _TagFactory:
            throw new ParseException("Unexpected tag: '" + _obj[objSt + 0] + "'. Tags must be enclosed in an autofield.", ln);
        } else try if (_obj[objSt + 0][0] == "@") goto _IdentifierFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _IdentifierFactory:
            yield.endl = objSt;
            yield._obj = (object)(int.Parse(_obj[objSt + 0].Substring(1)));
            return yield;
        } else try if (Regex.Match(_obj[objSt + 0], @"-?([0-9]+|[0-9]*\.[0-9]+)", RegexOption.IgnoreCase | RegexOptions.CultureInvariant).Success) goto _DoubleFactory; catch (IndexOutOfRangeException ignored); if (false) {
        _DoubleFactory:
            yield.endl = objSt;
            yield._obj = (object)(double.Parse(_obj[objSt + 0]));
            return yield;
        } else {
            throw new ParseException("Unrecognized object token: '" + _obj[objSt] + "'", ln);
        }
    }

    public static void ParseMDMFSegment(int segSt) {
        string segmentType = file[segSt].Split()[1];
        switch (segmentType) {
            case "Defines": {
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
            case "Scripts": {
                for (int i = segSt + 1;;) {
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
                    List<string> line = MDMFLinePreprocess(file[i]);
                    if (line[0][0] != "@") {
                        throw new ParseException("Expected reference ID, got '" + line[0] + "'", i);
                    }
                    if (line[1] != "begin") {
                        throw new ParseException("Expected script initial delimiter ('begin'), got '" + line[1] + "'", i);
                    }
                    Script script = new Script();
                    script.SectorId = int.Parse(line[0].Substring(1));
                    script.Contents = new List<ScriptInstruction>();
                    for (;;i++) {
                        line = MDMFLinePreprocess(file[i]);
                        if (line[0] == "end") {
                            break;
                        } else if (line[0] == "#Segment") {
                            throw new ParseException("Unexpected segment delimiter: Expected script delimiter ('end'), got '#Segment'", i);
                        } else {
                            script.Contents.Add(new ScriptInstruction(line));
                        }
                    }
                    ScriptManager.allScripts.Add(script);
                }
                break;
            }
            case "Lines": {
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
                    List<string> line = MDMFLinePreprocess(file[i]);
                    YieldObject _objIndex = MDMFGetObject(line, 0, i);
                    int li = (int)(_objIndex._obj);
                    YieldObject yObj = MDMFGetObject(line, _objIndex.endl + 1, i);
                    if (StrictParse && yObj.endl < line.Length - 1) {
                        throw new ParseException("Expected end-of-line, got '" + line[yObj.endl + 1] + "'", i);
                    }
                    Lines.Add(li, (Autofield)(yObj._obj));
                }
                break;
            }
            case "Sectors": {
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
                    List<string> line = MDMFLinePreprocess(file[i]);
                    YieldObject yObj = MDMFGetObject(line, 0, i);
                    if (StrictParse && yObj.endl < line.Length - 1) {
                        throw new ParseException("Expected end-of-line, got '" + line[yObj.endl + 1] + "'", i);
                    }
                    Sectors.Add((Sector)(yObj._obj));
                }
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

        Sectors.Clear();
        Lines.Clear();
        ScriptManager.activeScripts.Clear();
        ScriptManager.allScripts.Clear();
        Defines.Clear();
        
        if ("Defines" in segments.Keys) {
            ParseMDMFSegment(segments["Defines"]);
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

        PlaneDefs.Clear();
        
        foreach (int index in Lines.Keys) {
            Autofield tags = new Autofield();
            tags.Tags = new List<FieldTag>(Lines[index].Tags);
            PlaneDefs.Add(index, new PlaneDef { new List<Vector3>(), index, tags });
        }

        foreach (Sector sector in Sectors) {
            foreach (Autofield tri in sector.Boundary.Triangles) {
                if (int in tri.Fields.Keys) {
                    if ((int)(tri.Fields[int]) not in PlaneDefs.Keys) {
                        throw new MDMFException("Could not find a planedef with ID " + (int)(tri.Fields[int]).ToString());
                    }
                    Vector3Int vi = (Vector3Int)(tri.Fields[Vector3Int]);
                    Vector3 a = sector.Boundary.Vertices[vi.x], b = sector.Boundary.Vertices[vi.y], c = sector.Boundary.Vertices[vi.z];
                    PlaneDefs[(int)(tri.Fields[int])].Vertices.AddRange(new List<Vector3>{ a, b, c });
                }
            }
        }

        resultData = new LevelData();
        resultData.Sectors = Sectors;
        resultData.PlaneDefs = PlaneDefs;
    }
}