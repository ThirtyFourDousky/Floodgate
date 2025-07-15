using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floodgate.World;

public static class CustomMerger
{
    const string wrdCLINKS = "CONDITIONAL LINKS";
    const string wrdROOMS = "ROOMS";
    const string wrdCRIT = "CREATURES";
    const string wrdBLK = "BAT MIGRATION BLOCKAGES";

    const string opREMOVE = "REMOVE"; //removes line that matches specified string
    const string opREMOVEALL = "REMOVEALL"; //removes all lines that contains the specified string
    const string opREPLACE = "REPLACE"; //replace specific string by another, regex.replace
    const string opMERGE = "MERGE"; //default, replaces rooms with new connections


    public static readonly Dictionary<string,List<string>> RegisteredPaths = new();
    private static bool applied = false;
    internal static void Apply()
    {
        if(applied) return;

        On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues += WorldLoader_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues;
        On.WorldLoader.FindRoomFile += WorldLoader_FindRoomFile;

        string[] paths = AssetManager.ListDirectory("floodgate", false, true);

        Plugin.logger.LogDebug("Scanning for custom mergers");
        foreach (string hpath in paths)
        {
            string path = hpath.Replace('/', Path.DirectorySeparatorChar);
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            string key = path.trimStart(Path.DirectorySeparatorChar).trimEnd('.').ToUpperInvariant();
            if (!RegisteredPaths.ContainsKey(key))
            {
                RegisteredPaths.Add(key, new List<string>());
            }
            RegisteredPaths[key].Add(path);
            Plugin.logger.LogDebug(" " + key + "  - " + path);
        }

        applied = true;
    }

    static string overridepath = "floodgate" + Path.DirectorySeparatorChar + "override" + Path.DirectorySeparatorChar;
    private static string WorldLoader_FindRoomFile(On.WorldLoader.orig_FindRoomFile orig, string roomName, bool includeRootDirectory, string additionalAppend, bool showWarning)
    {
        List<string> commonPaths = [
            "World" + Path.DirectorySeparatorChar + roomName.Split('_')[0] + "-Rooms" + Path.DirectorySeparatorChar,
            "World" + Path.DirectorySeparatorChar + "Gates" + Path.DirectorySeparatorChar,
            "World" + Path.DirectorySeparatorChar + "Gates" + Path.DirectorySeparatorChar + "gate_shelters" + Path.DirectorySeparatorChar,
            "Levels" + Path.DirectorySeparatorChar,
            ];
        if (ModManager.MSC && roomName.ToLowerInvariant().Contains("challenge"))
        {
            commonPaths.Add("Levels" + Path.DirectorySeparatorChar + "Challenges" + Path.DirectorySeparatorChar);
        }
        string path;
        foreach(string hint in commonPaths)
        {
            path = AssetManager.ResolveFilePath(overridepath + hint + roomName + additionalAppend);
            if (File.Exists(path))
            {
                Plugin.logger.LogDebug("Loaded Floodgate Room " + roomName + "\n  - " + path);
                return includeRootDirectory ? "file:///" + path : path;
            }
        }
        return orig(roomName, includeRootDirectory, additionalAppend, showWarning);
    }

    private static void WorldLoader_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues(On.WorldLoader.orig_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, SlugcatStats.Timeline timelinePosition, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
    {
        orig(self, game, playerCharacter, timelinePosition, singleRoomWorld, worldName, region, setupValues);

        if (!RegisteredPaths.ContainsKey(worldName.ToUpperInvariant()))
        {
            return;
        }

        Plugin.logger.LogDebug("Trying to load Custom merging for " + worldName);
        CustomLines current = new(self.lines);
        CustomLines mLines = new(RegisteredPaths[worldName.ToUpperInvariant()], playerCharacter.value);
        Plugin.logger.LogDebug("Lines Loaded:\n" + string.Join("\n  ", mLines.Lines));
        //conditional links
        foreach (string mLine in mLines.conditionallinks)
        {
            CustomLine merge = mLine;
            if (merge.operand == opMERGE || string.IsNullOrWhiteSpace(merge.operand))
            {
                current.conditionallinks.Add(merge.line);
            }
            else
            {
                DoOperation(ref current.conditionallinks, mLine);
            }
        }

        //rooms, creatures, bat migration blockages
        foreach (string mLine  in mLines.rooms)
        {
            DoOperation(ref current.rooms, mLine);
        }
        foreach(string mLine  in mLines.creatures)
        {
            DoOperation(ref current.creatures, mLine);
        }

        self.lines = current.Lines;
        Plugin.logger.LogDebug("World Lines Result:\n" + string.Join("\n  ", self.lines));
    }

    public static void DoOperation(ref List<string> lines, CustomLine merge)
    {
        if(merge.operand == opREMOVE)
        {
            lines.RemoveAll(i=>i == merge.line);
        }
        else if(merge.operand == opREMOVEALL)
        {
            lines.RemoveAll(i=>i.Contains(merge.line));
        }
        else if(merge.operand == opREPLACE)
        {
            string[] sub = merge.line.Split([" : "] ,StringSplitOptions.None);
            for (int i = 0; sub.Length == 2 && i < lines.Count; i++)
            {
                lines[i] = lines[i].Replace(sub[0], sub[1]);
            }
        }
        else if(merge.operand == opMERGE || string.IsNullOrWhiteSpace(merge.operand))
        {
            string pattern = merge.line.Split(':')[0];
            if (lines.Any(i => i.StartsWith(pattern)))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(merge.line.Split(':')[0]))
                    {
                        lines[i] = merge.line;
                    }
                }
            }
            else
            {
                lines.Add(merge.line);
            }
        }
        lines = lines.Distinct().ToList();
    }


    public class CustomLines
    {
        public List<string> conditionallinks = new();
        public List<string> rooms = new();
        public List<string> creatures = new();
        public List<string> batmigrationblockages = new();
        public List<string> Lines => [
            wrdCLINKS, ..conditionallinks, "END " + wrdCLINKS,
            wrdROOMS, ..rooms, "END " + wrdROOMS,
            wrdCRIT, ..creatures, "END " + wrdCRIT,
            wrdBLK, ..batmigrationblockages, "END " + wrdBLK,
        ];
        public CustomLines(List<string> paths, string name)
        {
            foreach (string path in paths)
            {
                List<string> wLines;
                try
                {
                    wLines = File.ReadLines(path).ToList();
                }catch (Exception ex)
                {
                    Plugin.logger.LogError(ex);
                    continue;
                }
                List<string> lines = new();
                for (int i = 0; i < wLines.Count; i++)
                {
                    string cur = wLines[i];

                    int scugStart = cur.IndexOf("((");
                    int scugEnd = cur.IndexOf("))");
                    if (scugStart > 0 && scugEnd > 0)
                    {
                        var slugcats = cur.Substring(scugStart + 2, scugEnd - scugStart - 2).Split(',');
                        if (slugcats.Length > 0)
                        {
                            List<string> pSlugcats = slugcats.Where(i => !i.StartsWith("!")).ToList();
                            if (pSlugcats.Count > 0 && !pSlugcats.Contains(name))
                            {
                                continue;
                            }
                            List<string> nSlugcats = slugcats.Where(i => i.StartsWith("!")).ToList();
                            if (nSlugcats.Count > 0 && nSlugcats.Contains(name.trimStart('!')))
                            {
                                continue;
                            }
                        }
                    }
                    int modsStart = cur.IndexOf("{{");
                    int modsEnd = cur.IndexOf("}}");
                    if (modsStart > 0 && modsEnd > 0)
                    {
                        var mods = cur.Substring(modsStart + 2, modsEnd - modsStart - 2).Split(',');
                        if (mods.Length > 0)
                        {
                            var ActiveModIDs = ModManager.ActiveMods.Select(i => i.id).ToList();
                            List<string> pMods = mods.Where(i => !i.StartsWith("!")).ToList();
                            List<bool> pmod = new();
                            foreach (var mod in pMods)
                            {
                                pmod.Add(ActiveModIDs.Contains(mod));
                            }
                            if (!pmod.All(i => i == true))
                            {
                                continue;
                            }
                            List<string> nMods = mods.Where(i => i.StartsWith("!")).ToList();
                            bool nmod = false;
                            foreach (string mod in nMods)
                            {
                                if (ActiveModIDs.Contains(mod.trimStart('!')))
                                {
                                    nmod = true;
                                    break;
                                }
                            }
                            if (nmod)
                            {
                                continue;
                            }
                        }
                    }
                    int cuttingPoint = scugEnd > 0 || modsEnd > 0 ? ((scugEnd > modsEnd ? scugEnd : modsEnd) + 2) : 0;

                    lines.Add(cur.Substring(cuttingPoint));
                }
                if (lines.Contains(wrdCLINKS))
                {
                    conditionallinks.AddRange(lines.GetRange(lines.IndexOf(wrdCLINKS) + 1, lines.IndexOf("END " + wrdCLINKS) - lines.IndexOf(wrdCLINKS) - 1));
                }
                if (lines.Contains(wrdROOMS))
                {
                    rooms.AddRange(lines.GetRange(lines.IndexOf(wrdROOMS) + 1, lines.IndexOf("END " + wrdROOMS) - lines.IndexOf(wrdROOMS) - 1));
                }
                if (lines.Contains(wrdCRIT))
                {
                    creatures.AddRange(lines.GetRange(lines.IndexOf(wrdCRIT) + 1, lines.IndexOf("END " + wrdCRIT) - lines.IndexOf(wrdCRIT) - 1));
                }
                if (lines.Contains(wrdBLK))
                {
                    batmigrationblockages.AddRange(lines.GetRange(lines.IndexOf(wrdBLK) + 1, lines.IndexOf("END " + wrdBLK) - lines.IndexOf(wrdBLK) - 1));
                }
            }
            conditionallinks = conditionallinks.Distinct().ToList();
            rooms = rooms.Distinct().ToList();
            creatures = creatures.Distinct().ToList();
            batmigrationblockages = batmigrationblockages.Distinct().ToList();
        }
        public CustomLines(List<string> lines)
        {
            if (lines.Contains(wrdCLINKS))
            {
                conditionallinks.AddRange(lines.GetRange(lines.IndexOf(wrdCLINKS) + 1, lines.IndexOf("END " + wrdCLINKS) - lines.IndexOf(wrdCLINKS) - 1));
            }
            if (lines.Contains(wrdROOMS))
            {
                rooms.AddRange(lines.GetRange(lines.IndexOf(wrdROOMS) + 1, lines.IndexOf("END " + wrdROOMS) - lines.IndexOf(wrdROOMS) - 1));
            }
            if (lines.Contains(wrdCRIT))
            {
                creatures.AddRange(lines.GetRange(lines.IndexOf(wrdCRIT) + 1, lines.IndexOf("END " + wrdCRIT) - lines.IndexOf(wrdCRIT) - 1));
            }
            if (lines.Contains(wrdBLK))
            {
                batmigrationblockages.AddRange(lines.GetRange(lines.IndexOf(wrdBLK) + 1, lines.IndexOf("END " + wrdBLK) - lines.IndexOf(wrdBLK) - 1));
            }
        }
    }
    public class CustomLine(string line, string operand)
    {
        public string line = line;
        public string operand = operand;

        public static implicit operator CustomLine(string line)
        {
            if (line.Contains("[") && line.Contains("]"))
            {
                return new(line.trimStart(']'), line.trimEnd(']').trimStart('['));
            }
            else
            {
                return new(line, string.Empty);
            }
        }
    }
}
