using Floodgate.NotEnums;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Floodgate.World;

public static class CustomMerger
{
    const string wrdCLINKS = "CONDITIONAL LINKS";
    const string wrdROOMS = "ROOMS";
    const string wrdCRIT = "CREATURES";
    const string wrdBLK = "BAT MIGRATION BLOCKAGES";

    const string opREMOVE = "REMOVE"; //removes line that matches specified string
    const string opREMOVEALL = "REMOVEALL"; //removes all lines that contains the specified string
    const string opREMOVELINESTART = "REMOVELINESTART"; //removes lines that starts with the specific string
    const string opREPLACEALL = "REPLACEALL"; //replace specific string by another, regex.replace
    const string opREPLACE = "REPLACE"; // three parameter line, replaces a specific string under a line that matches the first parameter
    const string opMERGE = "MERGE"; //default, replaces rooms with new connections

    const string kwCUSTOM = "CUSTOM:"; //Custom Condition, runs a bool method if it has been assigned, otherwise line is ignored

    public static bool CRSpresent = false;
    public static System.Reflection.Assembly CRS;

    //public static readonly Dictionary<string, string> replacedRoomName = new();

    public static readonly Dictionary<string, List<string>> RegisteredPaths = new();
    private static bool applied = false;

    public static readonly List<IDetour> hooks = new();
    internal static void Apply()
    {
        if (applied) return;

        if (CRSpresent = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "CustomRegionsSupport"))
        {
            CRS = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "CustomRegionsSupport");
        }

        IL.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues += WorldLoader_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues1;

        Rescan();

        applied = true;
    }

    private static void WorldLoader_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues1(MonoMod.Cil.ILContext il)
    {
        bool error = false;
        try
        {
            ILProcessor processor = il.Body.GetILProcessor();
            Instruction stlocv5 = processor.Body.Instructions.LastOrDefault(i => i.MatchStloc(5));
            processor.InsertAfter(stlocv5, processor.Create(OpCodes.Call, processor.Import(typeof(CustomMerger).GetMethod("RealizeCustomMerge", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))));
            processor.InsertAfter(stlocv5, processor.Create(OpCodes.Ldloca_S, (byte)5));
            processor.InsertAfter(stlocv5, processor.Create(OpCodes.Ldarg_0));
        }
        catch (Exception ex)
        {
            error = true;
            FloodgatePatcher.CustomLog.LogError("Error on WorldLoader.Ctor ILHook\n  " + ex.Message);
        }
        finally
        {
            if (!error)
            {
                Plugin.logger.LogInfo("Applied IL Hook for WorldLoader constructor");
            }
        }
    }

    public static void Rescan()
    {
        RegisteredPaths.Clear();
        string[] paths = AssetManager.ListDirectory("floodgate", false, true);
        FloodgatePatcher.CustomLog.Log("Scanning for custom mergers");
        foreach (string hpath in paths)
        {
            string path = hpath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string key = path.trimStart(Path.DirectorySeparatorChar).trimEnd('.').ToUpperInvariant();
            if (!RegisteredPaths.ContainsKey(key))
            {
                RegisteredPaths.Add(key, new List<string>());
            }
            if (RegisteredPaths[key].Contains(path)) { continue; }
            RegisteredPaths[key].Add(path);
            FloodgatePatcher.CustomLog.Log(" " + key + "  - " + path);
        }
    }

    static string overridepath = "floodgate" + Path.DirectorySeparatorChar + "override" + Path.DirectorySeparatorChar;
    public static void RealizeCustomMerge(WorldLoader self, ref string[] worldfile)
    {
        //replacedRoomName.Clear();
        List<string> WorldLines = worldfile.ToList();
        List<string> fallback = [.. WorldLines];
        bool error = false;
        bool skippedMerging = false;
        try
        {
            if (self.game != null && (self.game.IsArenaSession || self.game.session is SandboxGameSession))
            {
                return;
            }
            if (self.game == null) FloodgatePatcher.CustomLog.Log("[Floodgate Custom Merger] current game is null, possible baking process or warp map loading");
            string worldName = self.worldName;
            string playerCharacter = self.playerCharacter == null ? "_null" : self.playerCharacter.value;
            string timelinePosition = self.timelinePosition == null ? "_null" : self.timelinePosition.value;
            if (!RegisteredPaths.ContainsKey(worldName.ToUpperInvariant()))
            {
                FloodgatePatcher.CustomLog.Log("[Floodgate Custom Merger] Loading region " + worldName + "\n  Current slugcat: " + playerCharacter + "\n  Current timeline: " + timelinePosition);
                skippedMerging = true;
                goto LOWBUDGETMODDEDEXPERIENCE;
            }

            FloodgatePatcher.CustomLog.Log("[Floodgate Custom Merger] Loading custom merge for " + worldName + "\n  Current slugcat: " + playerCharacter + "\n  Current timeline: " + timelinePosition);
            CustomLines current = new(WorldLines);
            CustomLines mLines = new(RegisteredPaths[worldName.ToUpperInvariant()], timelinePosition, playerCharacter, self.world);
            FloodgatePatcher.CustomLog.Log("[Floodgate Custom Merger] Lines Loaded:\n" + string.Join("\n  ", mLines.Lines));
            //conditional links
            System.Threading.Tasks.Task conlTask = System.Threading.Tasks.Task.Run(() =>
            {
                foreach (string mLine in mLines.conditionallinks)
                {
                    CustomLine merge = mLine;
                    merge.line = merge.line.Replace("%SLUGCAT%", playerCharacter);
                    merge.line = merge.line.Replace("%TIMELINE%", timelinePosition);
                    if (!string.IsNullOrWhiteSpace(merge.line))
                    {
                        if (merge.operand == opMERGE || string.IsNullOrWhiteSpace(merge.operand))
                        {
                            current.conditionallinks.Add(merge.line);
                        }
                        else
                        {
                            DoOperation(ref current.conditionallinks, mLine);
                        }
                    }
                }
                current.conditionallinks = current.conditionallinks.Distinct().ToList();
            });
            //rooms, creatures
            System.Threading.Tasks.Task roomTask = System.Threading.Tasks.Task.Run(() =>
            {
                foreach (string mLine in mLines.rooms)
                {
                    DoOperation(ref current.rooms, mLine);
                }
                current.rooms = current.rooms.Distinct().ToList();
            });
            System.Threading.Tasks.Task critTask = System.Threading.Tasks.Task.Run(() =>
            {
                foreach (string mLine in mLines.creatures)
                {
                    DoOperation(ref current.creatures, mLine);
                }
                mLines.creatures = current.creatures.Distinct().ToList();
            });

            conlTask.GetAwaiter().GetResult(); roomTask.GetAwaiter().GetResult(); critTask.GetAwaiter().GetResult();

            WorldLines = current.Lines;
        }
        catch (Exception e)
        {
            error = true;
            FloodgatePatcher.CustomLog.LogError("[Floodgate Custom Merger] Exception on Custom Merger\n==============================\n" + e.ToString() + "\n==============================");
            WorldLines = fallback;
        }
        finally
        {
            if (error)
            {
                FloodgatePatcher.CustomLog.Log("[Floodgate Custom Merger] Undoing custom merging");
            }
            else
            {
                if (skippedMerging)
                {
                    //something else
                }
                else
                {
                    FloodgatePatcher.CustomLog.Log("[Floodgate Custom Merger] World Lines Result:\n" + string.Join("\n  ", WorldLines));
                }
            }
        }

    //low budget modded experience
    LOWBUDGETMODDEDEXPERIENCE:
        bool moddedcritskip = false;
        if (!Plugin.RemixOptions.LowBudgetModdedExperience.Value)
        {
            worldfile = WorldLines.ToArray();
            return;
        }
        error = false;
        fallback = [.. WorldLines]; //bad practice, doing it twice
        try
        {
            CustomLines current = new(WorldLines);

            for (int i = 0; i < current.creatures.Count; i++)
            {
                current.creatures[i] = LowBudgetModdedExperience.AddCreatures(current.creatures[i]);
            }

            WorldLines = current.Lines;
        }
        catch (Exception e)
        {
            error = true;
            FloodgatePatcher.CustomLog.LogError("[Floodgate \"\"Modded Creatures\"\"] Exception on adding modded creatures\n==============================\n" + e.ToString() + "\n==============================");
            WorldLines = fallback;
        }
        finally
        {
            if (!moddedcritskip)
            {
                if (error)
                {
                    FloodgatePatcher.CustomLog.LogError("[Floodgate \"\"Modded Creatures\"\"] Reverting modded creatures");
                }
                else
                {
                    FloodgatePatcher.CustomLog.Log("[Floodgate \"\"Modded Creatures\"\"] Modded Spawns Result:\n" + string.Join("\n  ", (WorldLines.GetRange(WorldLines.IndexOf(wrdCRIT) + 1, WorldLines.IndexOf("END " + wrdCRIT) - WorldLines.IndexOf(wrdCRIT) - 1))));
                }
            }
        }
        worldfile = WorldLines.ToArray();
    }

#warning remember to refactor these stupid linqs
    public static void DoOperation(ref List<string> lines, CustomLine merge)
    {
        if (string.IsNullOrWhiteSpace(merge.line))
        {
            FloodgatePatcher.CustomLog.Log("[World Loader] line is empty\n[" + merge.operand + "]" + merge.line);
            //lines = lines.Distinct().ToList();
            return;
        }
        FloodgatePatcher.CustomLog.Log("[World Loader] doing operation [" + merge.operand + "] with line " + merge.line);
        if (merge.operand == opREMOVE)
        {
            FloodgatePatcher.CustomLog.Log("[World Loader] removing line " + merge.line);
            lines.RemoveAll(i => i == merge.line);
        }
        else if (merge.operand == opREMOVEALL)
        {
            FloodgatePatcher.CustomLog.Log("[World Loader] removing all lines that contains " + merge.line);
            lines.RemoveAll(i => i.Contains(merge.line));
        }
        else if(merge.operand == opREMOVELINESTART)
        {
            FloodgatePatcher.CustomLog.Log("[World Loader] removing all lines that starts with " + merge.line);
            lines.RemoveAll(i => i.StartsWith(merge.line));
        }
        else if (merge.operand == opREPLACE)
        {
            int i = 0;
            string[] parameters = merge.line.Split([" :;: "], StringSplitOptions.None);
            if (parameters.Length == 3)
            {
                for (; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(parameters[0]))
                    {
                        FloodgatePatcher.CustomLog.Log("[World Loader] replacing occurrence\n" + parameters[1] + "  =>  " + parameters[2] + "\n on line:\n" + lines[i]);
                        lines[i] = lines[i].Replace(parameters[1], parameters[2]);
                        FloodgatePatcher.CustomLog.Log("Result:\n" + lines[i]);
                    }
                }
            }
            else
            {
                FloodgatePatcher.CustomLog.LogError("[World Loader] line " + merge.line + " is out of range a REPLACE operation (" + (parameters.Length < 3 ? "not enough" : "too many") + " parameters)");
            }
        }
        else if (merge.operand == opREPLACEALL)
        {
            string[] sub = merge.line.Split([" :;: "], StringSplitOptions.None);
            for (int i = 0; sub.Length == 2 && i < lines.Count; i++)
            {
                FloodgatePatcher.CustomLog.Log("[World Loader] replacing occurrence " + sub[0] + " with " + sub[1] + " in line:\n" + lines[i]);
                lines[i] = lines[i].Replace(sub[0], sub[1]);
            }
        }
        else if (merge.operand == opMERGE || string.IsNullOrWhiteSpace(merge.operand))
        {
            string pattern = merge.line.Split(':')[merge.line.StartsWith("LINEAGE") ? 1 : 0] + ":";
            if (lines.Any(i => i.StartsWith(pattern)))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(pattern))
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
        //lines = lines.Distinct().ToList();
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
        public CustomLines(List<string> paths, string timelinePosition, string characterName, global::World world)
        {
            foreach (string path in paths)
            {
                List<string> wLines;
                try
                {
                    wLines = File.ReadLines(path).ToList();
                }
                catch (Exception ex)
                {
                    Plugin.logger.LogError(ex);
                    continue;
                }
                List<string> lines = new();
                for (int i = 0; i < wLines.Count; i++)
                {
                    string cur = wLines[i];

                    if (string.IsNullOrWhiteSpace(cur)) continue;

                    int scugStart = cur.IndexOf("((");
                    int scugEnd = cur.IndexOf("))");
                    if (scugStart != -1 && scugEnd != -1)
                    {
                        var slugcats = cur.Substring(scugStart + 2, scugEnd - scugStart - 2).Split(',');
                        if (slugcats.Length > 0)
                        {
                            List<string> pSlugcats = slugcats.Where(i => !i.StartsWith("!")).ToList();
                            if (pSlugcats.Count > 0 && !(pSlugcats.Contains(timelinePosition) || pSlugcats.Contains(characterName)))
                            {
                                continue;
                            }
                            List<string> nSlugcats = slugcats.Where(i => i.StartsWith("!")).ToList();
                            if (nSlugcats.Count > 0 && (nSlugcats.Contains("!" + timelinePosition) || nSlugcats.Contains("!" + characterName)))
                            {
                                continue;
                            }
                        }
                        cur = cur.Remove(scugStart, scugEnd - scugStart + 2);

                    }
                    else if (scugStart == -1 ^ scugEnd == -1)
                    {
                        FloodgatePatcher.CustomLog.LogError("Broken line\n    " + cur + "\n    missing " + (scugStart == -1 ? "start `((`" : "end `))`") + " player character array pattern");
                        continue;
                    }
                    int modsStart = cur.IndexOf("{{");
                    int modsEnd = cur.IndexOf("}}");
                    if (modsStart != -1 && modsEnd != -1)
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
                        cur = cur.Remove(modsStart, modsEnd - modsStart + 2);
                    }
                    else if (modsStart == 1 ^ modsEnd == 1)
                    {
                        FloodgatePatcher.CustomLog.LogError("Broken line\n    " + cur + "\n    missing " + (modsStart == -1 ? "start `{{`" : "end `}}`") + " mods array pattern");
                        continue;
                    }
                    if (cur.Contains("[") ^ cur.Contains("]"))
                    {
                        FloodgatePatcher.CustomLog.LogError("Broken line\n    " + cur + "\n    missing " + (cur.Contains("]") ? "start `[`" : "end `]`") + " operands array pattern");
                        continue;
                    }

                    if (cur.StartsWith(kwCUSTOM))
                    {
                        int keyEnd = cur.IndexOf(" ;;; ");
                        if (keyEnd == -1)
                        {
                            FloodgatePatcher.CustomLog.LogError("Broken line condition (missing separator \" ;;; \"):\n  " + cur);
                            continue;
                        }
                        if (CustomConditions.TryGetValue(cur.Substring(7, keyEnd - 7), out CustomCondition condition))
                        {
                            if (condition(world))
                            {
                                cur = cur.Substring(keyEnd + 5).TrimStart();
                                goto END;
                            }
                        }
                        continue;
                    }

                    //line added
                END:
                    lines.Add(cur);
                }
                if (lines.Contains(wrdCLINKS) && lines.Contains("END " + wrdCLINKS))
                {
                    conditionallinks.AddRange(lines.GetRange(lines.IndexOf(wrdCLINKS) + 1, lines.IndexOf("END " + wrdCLINKS) - lines.IndexOf(wrdCLINKS) - 1));
                }
                if (lines.Contains(wrdROOMS) && lines.Contains("END " + wrdROOMS))
                {
                    rooms.AddRange(lines.GetRange(lines.IndexOf(wrdROOMS) + 1, lines.IndexOf("END " + wrdROOMS) - lines.IndexOf(wrdROOMS) - 1));
                }
                if (lines.Contains(wrdCRIT) && lines.Contains("END " + wrdCRIT))
                {
                    creatures.AddRange(lines.GetRange(lines.IndexOf(wrdCRIT) + 1, lines.IndexOf("END " + wrdCRIT) - lines.IndexOf(wrdCRIT) - 1));
                }
                if (lines.Contains(wrdBLK) && lines.Contains("END " + wrdBLK))
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
            if (lines.Contains(wrdCLINKS) && lines.Contains("END " + wrdCLINKS))
            {
                conditionallinks.AddRange(lines.GetRange(lines.IndexOf(wrdCLINKS) + 1, lines.IndexOf("END " + wrdCLINKS) - lines.IndexOf(wrdCLINKS) - 1));
            }
            if (lines.Contains(wrdROOMS) && lines.Contains("END " + wrdROOMS))
            {
                rooms.AddRange(lines.GetRange(lines.IndexOf(wrdROOMS) + 1, lines.IndexOf("END " + wrdROOMS) - lines.IndexOf(wrdROOMS) - 1));
            }
            if (lines.Contains(wrdCRIT) && lines.Contains("END " + wrdCRIT))
            {
                creatures.AddRange(lines.GetRange(lines.IndexOf(wrdCRIT) + 1, lines.IndexOf("END " + wrdCRIT) - lines.IndexOf(wrdCRIT) - 1));
            }
            if (lines.Contains(wrdBLK) && lines.Contains("END " + wrdBLK))
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
            else if (line.Contains("[") ^ line.Contains("]"))
            {
                FloodgatePatcher.CustomLog.Log("line error\n" + line + "\nline contains unclosed operand");
                return "";
            }
            else
            {
                return new(line, string.Empty);
            }
        }
    }

    //low budget modded experience
    public static class LowBudgetModdedExperience
    {
        public static string AddCreatures(string line)
        {
            Dictionary<string, int> modCreatures = new(100);
            Dictionary<CreatureTemplate.Type, LCritItem> linecrits = new();
            if (line.ToLowerInvariant().Contains("lineage"))
            {
                //to do
            }
            else
            {
                string[] creatureText = line.trimStart(" : ").Split([", "], StringSplitOptions.None);
                for (int i = 0; i < creatureText.Length; i++)
                {
                    string[] parsing = creatureText[i].Split('-');
                    CreatureTemplate.Type type = WorldLoader.CreatureTypeFromString(parsing[1]);
                    if (type == null)
                    {
                        continue;
                    }
                    string flagtext = "";
                    int den = int.Parse(parsing[0]);
                    int count = 1;
                    if (parsing.Length > 2)
                    {
                        bool flags = false;
                        for (int j = 2; j < parsing.Length; j++)
                        {
                            //shameless copy of game's code
                            if (parsing[j].Length > 0 && parsing[j][0] == '{')
                            {
                                flagtext = parsing[j];
                                flags = true;
                            }
                            else if (flags)
                            {
                                flagtext += "-" + parsing[j];
                            }
                            else
                            {
                                try
                                {
                                    count = Convert.ToInt32(parsing[j], System.Globalization.CultureInfo.InvariantCulture);
                                }
                                catch
                                {
                                    count = 1;
                                }
                            }
                            if (parsing[j].Contains("}"))
                            {
                                flags = false;
                            }
                        }
                    }
                    else
                    {
                        count = 1;
                    }
                    linecrits[type] = new(count, flagtext, den);
                }
                foreach (var lcrit in linecrits)
                {
                    LCritItem i = lcrit.Value;
                    string crittext = i.den + "-" + lcrit.Key.value + (!string.IsNullOrWhiteSpace(i.customFlag) ? "-" + i.customFlag : "");
                    if (modCreatures.ContainsKey(crittext))
                    {
                        modCreatures[crittext] += i.count;
                    }
                    else
                    {
                        modCreatures.Add(crittext, i.count);
                    }
                }
                foreach (var lcrit in linecrits)
                {
                    CreatureTemplate.Type type = lcrit.Key;
                    int count = lcrit.Value.count;
                    int den = lcrit.Value.den;
                    string flagtext = lcrit.Value.customFlag;

                    Dictionary<string, CritItem> creaturesToAdd = GetCreature(type, count);
                    bool addflag = !string.IsNullOrWhiteSpace(flagtext);
                    foreach (var creature in creaturesToAdd)
                    {
                        if (creature.Value.count < 1)
                        {
                            continue;
                        }
                        string crittext = den + "-" + creature.Key + (addflag && !creature.Value.ignoreFlag ? "-" + flagtext : "") + (!string.IsNullOrWhiteSpace(creature.Value.customFlag) ? "-" + creature.Value.customFlag : "");
                        if (modCreatures.ContainsKey(crittext))
                        {
                            modCreatures[crittext] += creature.Value.count;
                        }
                        else
                        {
                            modCreatures.Add(crittext, creature.Value.count);
                        }
                    }
                }
                line = line.trimEnd(" : ") + " : ";
                bool first = true;
                foreach (var modcrit in modCreatures)
                {
                    line = line + (first ? "" : ", ") + modcrit.Key + "-" + modcrit.Value;
                    first = false;
                }
            }
            return line;
        }
        //this makes the game chaotic as it adds creatures instead of replacing them
        public static Dictionary<string, CritItem> GetCreature(CreatureTemplate.Type type, int count)
        {
            Dictionary<string, CritItem> res = new();

            Dictionary<string, CritR> crit = new();
            if (type == global::CreatureTemplate.Type.MirosBird)
            {
                crit[CreatureTemplateType.Blizzor.Name] = new(false, 0.5f);
            }
            else if (type == global::CreatureTemplate.Type.Snail)
            {
                crit[CreatureTemplateType.BouncingBall.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.WaterBlob.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.Spider)
            {
                crit[CreatureTemplateType.ChipChop.Name] = new(false, 0.333333343f);
            }
            else if (type == global::DLCSharedEnums.CreatureTemplateType.Yeek)
            {
                crit[CreatureTemplateType.ChipChop.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.WaterBlob.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.JetFish || type == global::CreatureTemplate.Type.BigEel)
            {
                crit[CreatureTemplateType.CommonEel.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.MiniLeviathan.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.PoleMimic)
            {
                //crit[CreatureTemplateType.Denture] = 0.5f;
            }
            else if (type == global::CreatureTemplate.Type.TentaclePlant)
            {
                //crit[CreatureTemplateType.Denture] = 0.25f;
            }
            else if (type == global::DLCSharedEnums.CreatureTemplateType.StowawayBug)
            {
                crit[CreatureTemplateType.Denture.Name] = new(false, 0.5f);
            }
            else if (type == global::CreatureTemplate.Type.SeaLeech || type == global::DLCSharedEnums.CreatureTemplateType.JungleLeech)
            {
                crit[CreatureTemplateType.DivingBeetle.Name] = new(false, 0.1f);
                crit[CreatureTemplateType.MiniBlackLeech.Name] = new(false, 0.333333343f);
            }
            else if (type == global::DLCSharedEnums.CreatureTemplateType.EelLizard)
            {
                crit[CreatureTemplateType.DivingBeetle.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.CommonEel.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.KingVulture || type == global::CreatureTemplate.Type.Vulture)
            {
                //crit[CreatureTemplateType.FlyingBigEel] = 0.25f;
                crit[CreatureTemplateType.FatFireFly.Name] = new(false, 0.20f);
            }
            else if (type == global::DLCSharedEnums.CreatureTemplateType.MirosVulture)
            {
                crit[CreatureTemplateType.FatFireFly.Name] = new(false, 0.50f);
            }
            else if (type == global::CreatureTemplate.Type.BigSpider || type == global::DLCSharedEnums.CreatureTemplateType.MotherSpider)
            {
                crit[CreatureTemplateType.Glowpillar.Name] = new(false, 0.5f);
                crit[CreatureTemplateType.MaracaSpider.Name] = new(false, 0.6f);
            }
            else if (type == global::CreatureTemplate.Type.Hazer)
            {
                crit[CreatureTemplateType.HazerMom.Name] = new(false, 0.5f);
            }
            else if (type == global::CreatureTemplate.Type.CicadaA || type == global::CreatureTemplate.Type.CicadaB)
            {
                crit[CreatureTemplateType.Hoverfly.Name] = new(false, 0.3f);
                crit[CreatureTemplateType.Teuthicada.Name] = new(false, 0.6f);
            }
            else if (type == global::CreatureTemplate.Type.CyanLizard || type == global::CreatureTemplate.Type.WhiteLizard)
            {
                crit[CreatureTemplateType.HunterSeeker.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.Gecko.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.Centipede)
            {
                crit[CreatureTemplateType.Killerpillar.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.Scutigera.Name] = new(false, 0.7f);
            }
            else if (type == global::CreatureTemplate.Type.Leech)
            {
                crit[CreatureTemplateType.MiniBlackLeech.Name] = new(false, 0.6f);
            }
            else if (type == global::CreatureTemplate.Type.BigNeedleWorm)
            {
                //crit[CreatureTemplateType.MiniFlyingBigEel] = 0.333333343f;
                crit[CreatureTemplateType.SnootShootNoot.Name] = new(false, 0.333333343f);
            }
            else if (type == global::CreatureTemplate.Type.SmallCentipede)
            {
                crit[CreatureTemplateType.MiniScutigera.Name] = new(false, 0.4f);
            }
            else if (type == global::CreatureTemplate.Type.BlackLizard)
            {
                crit[CreatureTemplateType.MoleSalamander.Name] = new(false, 0.4f);
            }
            else if (type == global::CreatureTemplate.Type.BlueLizard || type == global::DLCSharedEnums.CreatureTemplateType.ZoopLizard)
            {
                crit[CreatureTemplateType.NoodleEater.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.BabyCroaker.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.Salamander)
            {
                crit[CreatureTemplateType.Polliwog.Name] = new(false, 0.25f);
                crit[CreatureTemplateType.WaterSpitter.Name] = new(false, 0.5f);
                crit[CreatureTemplateType.MoleSalamander.Name] = new(false, 0.75f);
            }
            else if (type == global::CreatureTemplate.Type.YellowLizard)
            {
                crit[CreatureTemplateType.Polliwog.Name] = new(false, 0.4f);
            }
            else if (type == global::CreatureTemplate.Type.RedCentipede)
            {
                crit[CreatureTemplateType.RedHorrorCenti.Name] = new(false, 0.5f);
            }
            else if (type == global::CreatureTemplate.Type.Centiwing)
            {
                crit[CreatureTemplateType.RedHorrorCenti.Name] = new(false, 0.1f);
                //crit[CreatureTemplateType.MiniFlyingBigEel] = 0.4f;
            }
            else if (type == global::CreatureTemplate.Type.SpitterSpider)
            {
                crit[CreatureTemplateType.MaracaSpider.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.Sporantula.Name] = new(false, 0.6666667f);
                crit[CreatureTemplateType.ToxicSpider.Name] = new(false, 0.166666676f);
            }
            else if (type == global::CreatureTemplate.Type.GreenLizard || type == global::DLCSharedEnums.CreatureTemplateType.SpitLizard)
            {
                crit[CreatureTemplateType.SilverLizard.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.WaterSpitter.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.PinkLizard)
            {
                crit[CreatureTemplateType.SilverLizard.Name] = new(false, 0.333333343f);
                crit[CreatureTemplateType.NoodleEater.Name] = new(false, 0.385f);
            }
            else if (type == global::CreatureTemplate.Type.EggBug || type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.FireBug)
            {
                if (type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.FireBug)
                {
                    crit[CreatureTemplateType.ThornBug.Name] = new(false, 0.333333343f);
                }
                else
                {
                    crit[CreatureTemplateType.SurfaceSwimmer.Name] = new(false, 0.333333343f);
                }
                crit[CreatureTemplateType.TintedBeetle.Name] = new(false, 0.6666667f);
            }
            else if (type == global::CreatureTemplate.Type.DropBug)
            {
                crit[CreatureTemplateType.ThornBug.Name] = new(false, 0.5f);
            }
            else if (type == global::CreatureTemplate.Type.RedLizard)
            {
                crit[CreatureTemplateType.SilverLizard.Name] = new(false, 0.4f);
            }
            else if (type == CreatureTemplate.Type.Scavenger)
            {
                crit[CreatureTemplateType.Scrounger.Name] = new(false, 0.5f);
            }
            else if (type == DLCSharedEnums.CreatureTemplateType.ScavengerElite)
            {
                crit[CreatureTemplateType.ScavengerSentinel] = new(false, 0.5f);
            }

            while (count > 0)
            {
                //add
                foreach (var item in crit)
                {
                    if (string.IsNullOrWhiteSpace(item.Key))
                    {
                        continue;
                    }
                    int val = Mathf.FloorToInt(item.Value.rand);
                    if (res.ContainsKey(item.Key))
                    {
                        res[item.Key].count += val;
                    }
                    else
                    {
                        res[item.Key] = new(item.Value.ignoreFlag, val);
                    }
                    item.Value.rand -= val;
                    if (UnityEngine.Random.value < item.Value.rand)
                    {
                        res[item.Key].count += 1;
                    }
                }
                count--;
            }
            return res;
        }
    }
    public class CritR(bool ignoreFlag, float rand)
    {
        public bool ignoreFlag = ignoreFlag;
        public float rand = rand;
    }
    public class CritItem(bool ignoreFlag, int count)
    {
        public bool ignoreFlag = ignoreFlag;
        public int count = count;
        public string customFlag = string.Empty;
    }
    public class LCritItem(int count, string customFlag, int den)
    {
        public int count = count;
        public string customFlag = customFlag;
        public int den = den;
    }


    public delegate bool CustomCondition(global::World world);
    internal static readonly Dictionary<string, CustomCondition> CustomConditions = new();
    public static void AddCondition(string key, CustomCondition condition)
    {
        if (CustomConditions.TryGetValue(key, out CustomCondition prev))
        {
            FloodgatePatcher.CustomLog.Log("[World Loader] WARNING: Custom Condition [" + key + "] already exists, replacing it\n  From: " + prev.Method.DeclaringType.AssemblyQualifiedName + "::" + prev.Method.Name +
                "; To: " + condition.Method.DeclaringType.AssemblyQualifiedName + "::" + condition.Method.Name + ";");
        }
        CustomConditions[key] = condition;
    }
}