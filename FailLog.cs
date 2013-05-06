/* FailLog.cs

by PapaCharlie9, MorpheusX(AUT)

Permission is hereby granted, free of charge, to any person or organization
obtaining a copy of the software and accompanying documentation covered by
this license (the "Software") to use, reproduce, display, distribute,
execute, and transmit the Software, and to prepare derivative works of the
Software, and to permit third-parties to whom the Software is furnished to
do so, without restriction.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.

*/

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;
using System.Xml;
using System.Windows.Forms;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;


namespace PRoConEvents
{

/* Aliases */

using EventType = PRoCon.Core.Events.EventType;
using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

/* Main Class */

public class FailLog : PRoConPluginAPI, IPRoConPluginInterface
{
    /* Enums */

    public enum MessageType { Warning, Error, Exception, Normal, Debug };

    /* Constants & Statics */

    public const int CRASH_COUNT_HEURISTIC = 16; // player count difference signifies a blaze dump

    public const double CHECK_FOR_UPDATES_MINS = 12*60; // every 12 hours

    public const double MAX_LIST_PLAYERS_SECS = 80; // should be at least every 30 seconds

    public const int MIN_UPDATE_USAGE_COUNT = 10; // minimum number of plugin updates in use

    /* Classes */


/* Inherited:
    this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
    this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
*/

// General
private bool fIsEnabled;
private int fServerUptime = -1;
private bool fServerCrashed = false; // because fServerUptime >  fServerInfo.ServerUptime
private DateTime fEnabledTimestamp = DateTime.MinValue;
private bool fGotLogin;
private CServerInfo fServerInfo;
private DateTime fLastVersionCheckTimestamp;
private Dictionary<String,String> fFriendlyMaps = null;
private Dictionary<String,String> fFriendlyModes = null;
private int fLastPlayerCount;
private DateTime fLastListPlayersTimestamp;
private String fHost;
private String fPort;
private int fMaxPlayers;
private bool fJustConnected;
private int fAfterPlayers;
private int fLastUptime;

// Settings support
private Dictionary<int, Type> fEasyTypeDict = null;
private Dictionary<int, Type> fBoolDict = null;
private Dictionary<int, Type> fListStrDict = null;

// Settings

/* ===== SECTION 1 - Settings ===== */

public int DebugLevel;
public bool EnableLogToFile;  // if true, sandbox must not be in sandbox!
public String LogFileSuffix;
public int BlazeDisconnectHeuristic;

/* ===== SECTION 2 - Server Description ===== */

public String RankedServerProvider;
public String ServerOwnerOrCommunity;
public String MyrconForumUserName;
public String ServerRegion; // Should this be an enum?
public String AdditionalInformation;

/* Constructor */

public FailLog() {
    /* Private members */
    fIsEnabled = false;
    fServerUptime = 0;
    fServerCrashed = false;
    fGotLogin = false;
    fServerInfo = null;
    fLastVersionCheckTimestamp = DateTime.MinValue;
    fFriendlyMaps = new Dictionary<String,String>();
    fFriendlyModes = new Dictionary<String,String>();
    fLastPlayerCount = 0;
    fLastListPlayersTimestamp = DateTime.MinValue;
    fAfterPlayers = 0;
    fLastUptime = 0;

    fEasyTypeDict = new Dictionary<int, Type>();
    fEasyTypeDict.Add(0, typeof(int));
    fEasyTypeDict.Add(1, typeof(Int16));
    fEasyTypeDict.Add(2, typeof(Int32));
    fEasyTypeDict.Add(3, typeof(Int64));
    fEasyTypeDict.Add(4, typeof(float));
    fEasyTypeDict.Add(5, typeof(long));
    fEasyTypeDict.Add(6, typeof(String));
    fEasyTypeDict.Add(7, typeof(string));
    fEasyTypeDict.Add(8, typeof(double));

    fBoolDict = new Dictionary<int, Type>();
    fBoolDict.Add(0, typeof(Boolean));
    fBoolDict.Add(1, typeof(bool));

    fListStrDict = new Dictionary<int, Type>();
    fListStrDict.Add(0, typeof(String[]));
    
    /* Settings */

    /* ===== SECTION 1 - Settings ===== */

    DebugLevel = 2;
    EnableLogToFile = false;
    LogFileSuffix = "fail.log";
    BlazeDisconnectHeuristic = CRASH_COUNT_HEURISTIC;

    /* ===== SECTION 2 - Server Description ===== */

    RankedServerProvider = String.Empty;
    ServerOwnerOrCommunity = String.Empty;
    MyrconForumUserName = String.Empty;
    ServerRegion = String.Empty;
    AdditionalInformation = String.Empty;

}

// Properties

public String FriendlyMap { 
    get {
        if (fServerInfo == null) return "???";
        String r = null;
        return (fFriendlyMaps.TryGetValue(fServerInfo.Map, out r)) ? r : fServerInfo.Map;
    }
}
public String FriendlyMode { 
    get {
        if (fServerInfo == null) return "???";
        String r = null;
        return (fFriendlyModes.TryGetValue(fServerInfo.GameMode, out r)) ? r : fServerInfo.GameMode;
    }
}



public String GetPluginName() {
    return "FailLog";
}

public String GetPluginVersion() {
    return "1.0.0.1";
}

public String GetPluginAuthor() {
    return "PapaCharlie9, MorpheusX(AUT)";
}

public String GetPluginWebsite() {
    return "https://github.com/PapaCharlie9/fail-log";
}

public String GetPluginDescription() {
    return FailLogUtils.HTML_DOC;
}









/* ======================== SETTINGS ============================= */









public List<CPluginVariable> GetDisplayPluginVariables() {


    List<CPluginVariable> lstReturn = new List<CPluginVariable>();

    try {
        
        /* ===== SECTION 1 - Settings ===== */
        
        lstReturn.Add(new CPluginVariable("1 - Settings|Debug Level", DebugLevel.GetType(), DebugLevel));
        
        lstReturn.Add(new CPluginVariable("1 - Settings|Enable Log To File", EnableLogToFile.GetType(), EnableLogToFile));

        if (EnableLogToFile) {
            lstReturn.Add(new CPluginVariable("1 - Settings|Log File Suffix", LogFileSuffix.GetType(), LogFileSuffix));
        }

        lstReturn.Add(new CPluginVariable("1 - Settings|Blaze Disconnect Heuristic", BlazeDisconnectHeuristic.GetType(), BlazeDisconnectHeuristic));
        
        /*
        var_name = "3 - Round Phase and Population Settings|Spelling Of Speed Names Reminder";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(Speed))) + ")";

        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(Speed), SpellingOfSpeedNamesReminder)));
        */

 /*

        var_name = "4 - Scrambler|Scramble By";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), ScrambleBy)));
 */

 
        /* ===== SECTION 2 - Server Description ===== */
        
        lstReturn.Add(new CPluginVariable("2 - Server Description|Ranked Server Provider", RankedServerProvider.GetType(), RankedServerProvider));

        lstReturn.Add(new CPluginVariable("2 - Server Description|Server Owner Or Community", ServerOwnerOrCommunity.GetType(), ServerOwnerOrCommunity));

        lstReturn.Add(new CPluginVariable("2 - Server Description|Myrcon Forum User Name", MyrconForumUserName.GetType(), MyrconForumUserName));

        lstReturn.Add(new CPluginVariable("2 - Server Description|Server Region", ServerRegion.GetType(), ServerRegion));

        lstReturn.Add(new CPluginVariable("2 - Server Description|Additional Information", AdditionalInformation.GetType(), AdditionalInformation));

    } catch (Exception e) {
        ConsoleException(e);
    }

    return lstReturn;
}

public List<CPluginVariable> GetPluginVariables() {
    return GetDisplayPluginVariables();
}

public void SetPluginVariable(String strVariable, String strValue) {

    if (fIsEnabled) DebugWrite(strVariable + " <- " + strValue, 6);

    try {
        String tmp = strVariable;
        int pipeIndex = strVariable.IndexOf('|');
        if (pipeIndex >= 0) {
            pipeIndex++;
            tmp = strVariable.Substring(pipeIndex, strVariable.Length - pipeIndex);
        }

        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        String propertyName = Regex.Replace(tmp, @"[^a-zA-Z_0-9]", String.Empty);
        
        FieldInfo field = this.GetType().GetField(propertyName, flags);
        
        Type fieldType = null;


        if (field != null) {
            fieldType = field.GetValue(this).GetType();
            if (fEasyTypeDict.ContainsValue(fieldType)) {
                field.SetValue(this, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
            } else if (fListStrDict.ContainsValue(fieldType)) {
                if (DebugLevel >= 8) ConsoleDebug("String array " + propertyName + " <- " + strValue);
                field.SetValue(this, CPluginVariable.DecodeStringArray(strValue));
            } else if (fBoolDict.ContainsValue(fieldType)) {
                if (fIsEnabled) DebugWrite(propertyName + " strValue = " + strValue, 6);
                if (Regex.Match(strValue, "true", RegexOptions.IgnoreCase).Success) {
                    field.SetValue(this, true);
                } else {
                    field.SetValue(this, false);
                }
            } else {
                if (DebugLevel >= 8) ConsoleDebug("Unknown var " + propertyName + " with type " + fieldType);
            }
        }
    } catch (System.Exception e) {
        ConsoleException(e);
    } finally {
        
        // Validate all values and correct if needed
        ValidateSettings(strVariable,  strValue);

    }
}

private bool ValidateSettings(String strVariable, String strValue) {
    try {
                
        /* ===== SECTION 1 - Settings ===== */

        if (strVariable.Contains("Debug Level")) ValidateIntRange(ref DebugLevel, "Debug Level", 0, 9, 2, false);

        if (strVariable.Contains("Blaze Disconnect Heuristic")) ValidateIntRange(ref BlazeDisconnectHeuristic, "Blaze Disconnect Heuristic", 4, 64, 8, false);
    
        /* ===== SECTION 2 - Exclusions ===== */
    
        // All strings, no validation needed so far


    } catch (Exception e) {
        ConsoleException(e);
    }
    return true;
}










/* ======================== OVERRIDES ============================= */










public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
    fHost = strHostName;
    fPort = strPort;

    this.RegisterEvents(this.GetType().Name, 
        "OnLogin",
        "OnServerInfo",
        "OnListPlayers",
        "OnMaxPlayers"
    );
}


public void OnPluginEnable() {
    fIsEnabled = true;
    fEnabledTimestamp = DateTime.Now;
    fJustConnected = true;

    ConsoleWrite("^bEnabled!^n Version = " + GetPluginVersion());

    GatherProconGoodies();

    ServerCommand("serverInfo");
    ServerCommand("admin.listPlayers", "all");

    CheckForPluginUpdate();
}


public void OnPluginDisable() {
    fIsEnabled = false;

    try {
        fEnabledTimestamp = DateTime.MinValue;

        Reset();
    } catch (Exception e) {
        ConsoleException(e);
    }
}


public override void OnLogin() {
    if (!fIsEnabled) return;
    
    DebugWrite("^9Got ^bOnLogin^n", 8);
    try {
        if (fJustConnected) return;
        fGotLogin = true;
        Failure("PROCON_RECONNECTED");
        fGotLogin = false;
    } catch (Exception e) {
        ConsoleException(e);
    }
}



public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnListPlayers^n, " + players.Count, 8);

    try {
        if (subset.Subset != CPlayerSubset.PlayerSubsetType.All) return;

        fJustConnected = false;

        // Check if player count is less than expected
        if ((players.Count + BlazeDisconnectHeuristic) < fLastPlayerCount) {
            fAfterPlayers = players.Count;
            Failure("BLAZE_DISCONNECT");
        }
        fLastPlayerCount = players.Count;

        // Check if last list players update took longer than expected
        if (fLastListPlayersTimestamp != DateTime.MinValue && DateTime.Now.Subtract(fLastListPlayersTimestamp).TotalSeconds > MAX_LIST_PLAYERS_SECS) {
            Failure("NETWORK_CONGESTION");
        }
        fLastListPlayersTimestamp = DateTime.Now;


    } catch (Exception e) {
        ConsoleException(e);
    }
}


public override void OnServerInfo(CServerInfo serverInfo) {
    if (!fIsEnabled || serverInfo == null) return;

    DebugWrite("^9^bGot OnServerInfo^n: Debug level = " + DebugLevel, 8);
    
    try {

        bool newMapMode = false;

        if (fServerInfo == null || fServerInfo.GameMode != serverInfo.GameMode || fServerInfo.Map != serverInfo.Map) {
            newMapMode = true;
        }
    
        // Check if serverInfo up-time is inconsistent
        if (fServerUptime > 0 && fServerUptime > serverInfo.ServerUptime + 2) { // +2 for rounding error on server side!
            DebugWrite("OnServerInfo fServerUptime = " + fServerUptime + ", serverInfo.ServerUptime = " + serverInfo.ServerUptime, 3);
            fLastUptime = fServerUptime;
            fServerCrashed = true;
            Failure("GAME_SERVER_CRASH");
            fServerCrashed = false;
        }

        fServerInfo = serverInfo;
        fServerUptime = serverInfo.ServerUptime;

        if (newMapMode) {
            DebugWrite("New map/mode: " + this.FriendlyMap + "/" + this.FriendlyMode, 3);
        }

        // Check for plugin updates periodically
        if (fLastVersionCheckTimestamp != DateTime.MinValue 
        && DateTime.Now.Subtract(fLastVersionCheckTimestamp).TotalMinutes > CHECK_FOR_UPDATES_MINS) {
            CheckForPluginUpdate();
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}


public override void OnMaxPlayers(int limit) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9Got ^bOnMaxPlayers^n", 8);
    try {
        fMaxPlayers = limit;
    } catch (Exception e) {
        ConsoleException(e);
    }
}


















/* ======================== SUPPORT FUNCTIONS ============================= */












private void Failure(String type) {
    if (fServerInfo == null) {
        ConsoleWarn("Failure: fServerInfo == null!");
        return;
    }
    String utcTime = DateTime.UtcNow.ToString("yyyyMMdd_HH:mm:ss");
    //Match rm = Regex.Match(TimeSpan.FromSeconds(fLastUptime).ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
    //String upTime = (rm.Success) ? rm.Groups[1].Value : "?";
    String upTime = TimeSpan.FromSeconds(fLastUptime).ToString();
    String players = fMaxPlayers.ToString() + "/" + fLastPlayerCount + "/" + fAfterPlayers;
    String details = String.Format("\"{0},{1},{2},{3},{4},{5}\"",
        RankedServerProvider,
        ServerOwnerOrCommunity,
        MyrconForumUserName, 
        ServerRegion,
        fServerInfo.ServerRegion + "/" + fServerInfo.ServerCountry,
        AdditionalInformation);
    String line = String.Format("Type:{0}, UTC:{1}, Server:\"{2}\", Host:{3}, Port:{4}, Map:{5}, Mode:{6}, Players:{7}, Uptime:{8}, Details:{9}",
        type,
        utcTime,
        fServerInfo.ServerName,
        fHost,
        fPort,
        this.FriendlyMap,
        this.FriendlyMode,
        players,
        upTime,
        details);

    ConsoleWrite("^8" + line);
    if (EnableLogToFile) {
        ServerLog(LogFileSuffix, line);
    }
    // TBD - Put PHP update here?
}

private String FormatMessage(String msg, MessageType type) {
    String prefix = "[^b" + GetPluginName() + "^n] ";

    if (Thread.CurrentThread.Name != null) prefix += "Thread(^b" + Thread.CurrentThread.Name + "^n): ";

    if (type.Equals(MessageType.Warning))
        prefix += "^1^bWARNING^0^n: ";
    else if (type.Equals(MessageType.Error))
        prefix += "^1^bERROR^0^n: ";
    else if (type.Equals(MessageType.Exception))
        prefix += "^1^bEXCEPTION^0^n: ";
    else if (type.Equals(MessageType.Debug))
        prefix += "^9^bDEBUG^n: ";

    return prefix + msg;
}


public void LogWrite(String msg)
{
    this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
}

public void ConsoleWrite(String msg, MessageType type)
{
    LogWrite(FormatMessage(msg, type));
}

public void ConsoleWrite(String msg)
{
    ConsoleWrite(msg, MessageType.Normal);
}

public void ConsoleWarn(String msg)
{
    ConsoleWrite(msg, MessageType.Warning);
}

public void ConsoleError(String msg)
{
    ConsoleWrite(msg, MessageType.Error);
}

public void ConsoleException(Exception e)
{
    if (DebugLevel >= 3) ConsoleWrite(e.ToString(), MessageType.Exception);
}

public void DebugWrite(String msg, int level)
{
    if (DebugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
}

public void ConsoleDebug(String msg)
{
    if (DebugLevel >= 4) ConsoleWrite(msg, MessageType.Debug);
}

public void Log(String path, String line) {
    try {
        if (!Path.IsPathRooted(path))
            path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, path);

        // Add timestamp
        line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + "\n";

        using (FileStream fs = File.Open(path, FileMode.Append)) {
            Byte[] info = new UTF8Encoding(true).GetBytes(line);
            fs.Write(info, 0, info.Length);
        }
    } catch (Exception ex) {
        ConsoleError("unable to append data to file " + path);
        ConsoleException(ex);
    }
}

public void ServerLog(String suffix, String line) {
    String YYYYMMDD = DateTime.Now.ToString("yyyyMMdd"); // TBD: procon instance local time, not game server time!
    String path = Path.Combine("Logs", fHost + "_" + fPort);
    path = Path.Combine(path, YYYYMMDD + "_" + suffix);
    Log(path, line);
}

private void ServerCommand(params String[] args)
{
    List<String> list = new List<String>();
    list.Add("procon.protected.send");
    list.AddRange(args);
    this.ExecuteCommand(list.ToArray());
}

private void TaskbarNotify(String title, String msg) {
    this.ExecuteCommand("procon.protected.notification.write", title, msg);
}


private void Reset() {
    fServerInfo = null; // release Procon reference
    fIsEnabled = false;
    fServerUptime = 0;
    fServerCrashed = false;
    fGotLogin = false;
    fLastVersionCheckTimestamp = DateTime.MinValue;
    fFriendlyMaps.Clear();
    fFriendlyModes.Clear();
    fLastPlayerCount = 0;
    fLastListPlayersTimestamp = DateTime.MinValue;
    fAfterPlayers = 0;
    fLastUptime = 0;
}





private void GatherProconGoodies() {
    fFriendlyMaps.Clear();
    fFriendlyModes.Clear();
    List<CMap> bf3_defs = this.GetMapDefines();
    foreach (CMap m in bf3_defs) {
        if (!fFriendlyMaps.ContainsKey(m.FileName)) fFriendlyMaps[m.FileName] = m.PublicLevelName;
        if (!fFriendlyModes.ContainsKey(m.PlayList)) fFriendlyModes[m.PlayList] = m.GameMode;
    }
    if (DebugLevel >= 8) {
        foreach (KeyValuePair<String,String> pair in fFriendlyMaps) {
            DebugWrite("friendlyMaps[" + pair.Key + "] = " + pair.Value, 8);
        }
        foreach (KeyValuePair<String,String> pair in fFriendlyModes) {
            DebugWrite("friendlyModes[" + pair.Key + "] = " + pair.Value, 8);
        }
    }
    DebugWrite("Friendly names loaded", 6);
}




private void ValidateInt(ref int val, String propName, int def) {
    if (val < 0) {
        ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
        val = def;
        return;
    }
}


private void ValidateIntRange(ref int val, String propName, int min, int max, int def, bool zeroOK) {
    if (zeroOK && val == 0) return;
    if (val < min || val > max) {
        String zero = (zeroOK) ? " or equal to 0" : String.Empty;
        ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
        val = def;
    }
}


private void ValidateDouble(ref double val, String propName, double def) {
    if (val < 0) {
        ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
        val = def;
        return;
    }
}


private void ValidateDoubleRange(ref double val, String propName, double min, double max, double def, bool zeroOK) {
    if (zeroOK && val == 0.0) return;
    if (val < min || val > max) {
        String zero = (zeroOK) ? " or equal to 0" : String.Empty;
        ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
        val = def;
        return;
    }
}






public void CheckForPluginUpdate() {
	try {
		XmlDocument xml = new XmlDocument();
        try {
            xml.Load("http://myrcon.com/procon/plugins/plugin/FailLog");
            //WebClient c = new WebClient();
            //String x = c.DownloadString("http://myrcon.com/procon/plugins/plugin/FailLog");
            //xml.LoadXml(x);
        } catch (System.Security.SecurityException e) {
            if (DebugLevel >= 8) ConsoleException(e);
            ConsoleWrite(" ");
            ConsoleWrite("^8^bNOTICE! Unable to check for plugin update!");
            ConsoleWrite("Tools => Options... => Plugins tab: ^bPlugin security^n is set to ^bRun plugins in a sandbox^n.");
            //ConsoleWrite("Please add ^bmyrcon.com^n to your trusted ^bOutgoing connections^n");
            ConsoleWrite("Consider changing to ^bRun plugins with no restrictions.^n");
            ConsoleWrite("Alternatively, check the ^bPlugins^n forum for an update to this plugin.");
            ConsoleWrite(" ");
            fLastVersionCheckTimestamp = DateTime.MaxValue;
            return;
        } 
        if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Got " + xml.BaseURI);
		XmlNodeList rows = xml.SelectNodes("//tr");
        if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: # rows = " + rows.Count);
        Dictionary<String,int> versions = new Dictionary<String,int>();
		foreach (XmlNode tr in rows) {
            XmlNode ver = tr.SelectSingleNode("td[1]");
            XmlNode count = tr.SelectSingleNode("td[2]");
            if (ver != null && count != null) {
                if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Version: " + ver.InnerText + ", Count: " + count.InnerText);
                int n = 0;
                if (!Int32.TryParse(count.InnerText, out n)) continue; 
                versions[ver.InnerText] = n;
            }
		}

        // Select current version and any "later" versions
        int usage = 0;
        String myVersion = GetPluginVersion();
        if (!versions.TryGetValue(myVersion, out usage)) {
            DebugWrite("CheckForPluginUpdate: " + myVersion + " not found!", 8);
            return;
        }

        // Update check time
        fLastVersionCheckTimestamp = DateTime.Now;

        // numeric sort
        List<String> byNumeric = new List<String>();
        byNumeric.AddRange(versions.Keys);
        // Sort numerically descending
        byNumeric.Sort(delegate(String lhs, String rhs) {
            if (lhs == rhs) return 0;
            if (String.IsNullOrEmpty(lhs)) return 1;
            if (String.IsNullOrEmpty(rhs)) return -1;
            uint l = VersionToNumeric(lhs);
            uint r = VersionToNumeric(rhs);
            if (l < r) return 1;
            if (l > r) return -1;
            return 0;
        });
        DebugWrite("CheckForPluginUpdate: sorted version list:", 7);
        foreach (String u in byNumeric) {
            DebugWrite(u + " (" + String.Format("{0:X8}", VersionToNumeric(u)) + "), count = " + versions[u], 7);
        }

        int position = byNumeric.IndexOf(myVersion);

        DebugWrite("CheckForPluginUpdate: found " + position + " newer versions", 5);

        if (position != 0) {
            // Newer versions found
            // Find the newest version with the largest number of usages
            int hasMost = -1;
            int most = 0;
            for (int i = position-1; i >= 0; --i) {
                int newerVersionCount = versions[byNumeric[i]];
                if (hasMost == -1 || most < newerVersionCount) {
                    // Skip newer versions that don't have enough usage yet
                    if (most > 0 && newerVersionCount < MIN_UPDATE_USAGE_COUNT) continue;
                    hasMost = i;
                    most = versions[byNumeric[i]];
                }
            }

            if (hasMost != -1 && hasMost < byNumeric.Count && most >= MIN_UPDATE_USAGE_COUNT) {
                String newVersion = byNumeric[hasMost];
                ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                ConsoleWrite(" ");
                ConsoleWrite("^8^bA NEW VERSION OF THIS PLUGIN IS AVAILABLE!");
                ConsoleWrite(" ");
                ConsoleWrite("^8^bPLEASE UPDATE TO VERSION: ^0" + newVersion);
                ConsoleWrite(" ");
                ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

                TaskbarNotify(GetPluginName() + ": new version available!", "Please download and install " + newVersion); 
            }
        }
	} catch (Exception e) {
		if (DebugLevel >= 8) ConsoleException(e);
	}
}

private uint VersionToNumeric(String ver) {
    uint numeric = 0;
    byte part = 0;
    Match m = Regex.Match(ver, @"^\s*([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)(\w*)\s*$");
    if (m.Success) {
        for (int i = 1; i < 5; ++i) {
            if (!Byte.TryParse(m.Groups[i].Value, out part)) {
                part = 0;
            }
            numeric = (numeric << 8) | part;
        }
    }
    return numeric;
}



} // end FailLog











/* ======================== UTILITIES ============================= */

#region UTILITIES







static class FailLogUtils {
    
    public static String ArrayToString(double[] a) {
        String ret = String.Empty;
        bool first = true;
        if (a == null || a.Length == 0) return ret;
        for (int i = 0; i < a.Length; ++i) {
            if (first) {
                ret = a[i].ToString("F0");
                first = false;
            } else {
                ret = ret + ", " + a[i].ToString("F0");
            }
        }
        return ret;
    }

    /*
    public static String ArrayToString(FailLog.Speed[] a) {
        String ret = String.Empty;
        bool first = true;
        if (a == null || a.Length == 0) return ret;
        for (int i = 0; i < a.Length; ++i) {
            if (first) {
                ret = Enum.GetName(typeof(FailLog.Speed), a[i]);
                first = false;
            } else {
                ret = ret + ", " + Enum.GetName(typeof(FailLog.Speed), a[i]);
            }
        }
        return ret;
    }
    */

    public static double[] ParseNumArray(String s) {
        double[] nums = new double[3] {-1,-1,-1}; // -1 indicates a syntax error
        if (String.IsNullOrEmpty(s)) return nums;
        if (!s.Contains(",")) return nums;
        String[] strs = s.Split(new Char[] {','});
        if (strs.Length != 3) return nums;
        for (int i = 0; i < nums.Length; ++i) {
            bool parsedOk = Double.TryParse(strs[i], out nums[i]);
            if (!parsedOk) {
                nums[i] = -1;
                return nums;
            }
        }
        return nums;
    }

    /*
    public static FailLog.Speed[] ParseSpeedArray(FailLog plugin, String s) {
        FailLog.Speed[] speeds = new FailLog.Speed[3] {
            FailLog.Speed.Adaptive,
            FailLog.Speed.Adaptive,
            FailLog.Speed.Adaptive
        };
        if (String.IsNullOrEmpty(s) || !s.Contains(",")) {
            if (s == null) s = "(null)";
            plugin.ConsoleWarn("Bad balance speed setting: " + s);
            return speeds;
        }
        String[] strs = s.Split(new Char[] {','});
        if (strs.Length != 3) {
            plugin.ConsoleWarn("Wrong number of speeds, should be 3, separated by commas: " + s);
            return speeds;
        }
        for (int i = 0; i < speeds.Length; ++i) {
            try {
                speeds[i] = (FailLog.Speed)Enum.Parse(typeof(FailLog.Speed), strs[i]);
            } catch (Exception) {
                plugin.ConsoleWarn("Bad balance speed value: " + strs[i]);
                speeds[i] = FailLog.Speed.Adaptive;
            }
        }
        return speeds;
    }
    */

#region HTML_DOC
    public const String HTML_DOC = @"
<h1>Fail Log</h1>

<p>For BF3, this plugin logs game server crashes, layer disconnects and Blaze dumps.</p>

<h2>Description</h2>
<p>TBD</p>


<h2>Development</h2>
<p>This plugin is an open source project hosted on GitHub.com. The repo is located at
<a href='https://github.com/PapaCharlie9/fail-log'>https://github.com/PapaCharlie9/fail-log</a> and
the master branch is used for public distributions. See the <a href='https://github.com/PapaCharlie9/fail-log/tags'>Tags</a> tab for the latest ZIP distributions. If you would like to offer bug fixes or new features, feel
free to fork the repo and submit pull requests.</p>
";
#endregion

} // end FailLogUtils
#endregion

} // end namespace PRoConEvents


