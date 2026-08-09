using FloodgatePatcher;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RegionKit.OptionsMenu;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ModCompat.RegionKit;

public static class TurboBakerStuff
{
    public static readonly List<IDetour> hooks = new();
    public static void Apply()
    {
        hooks.Add(new ILHook(typeof(global::RegionKit.ModOptions).GetMethod("Initialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), IL_ModOptions_Initialize));
        hooks.Add(new ILHook(typeof(TurboBakerTab).GetMethod("Initialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), IL_Initialize));
        hooks.Add(new ILHook(typeof(TurboBakerTab).GetMethod("Update", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), IL_Update));
    }

    public static IEnumerable<Region> EnumerableLoadAllRegions(SlugcatStats.Timeline timelineIndex, RainWorldGame game)
    {
        string path = AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + "regions.txt");
        string[] array = new string[1] { "" };
        
        if (File.Exists(path))
        {
            array = File.ReadAllLines(path);
        }

        Region[] array2 = new Region[array.Length];
        int num = 0;
        for (int i = 0; i < array.Length; i++)
        {
            yield return array2[i] = new Region(array[i], num, i, game, timelineIndex);
            num += array2[i].numberOfRooms;
        }
    }

    public static void IL_ModOptions_Initialize(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            c.GotoNext(i => i.MatchNewobj<TurboBakerTab>());
            c.Emit(OpCodes.Newobj, il.Import(typeof(FGTurboBakerTab).GetConstructor(new Type[] { typeof(OptionInterface) })));
            c.Remove();
        }
        catch (Exception ex)
        {
            CustomLog.LogError(ex.ToString());
        }
    }

    public static void IL_Initialize(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate(static delegate (TurboBakerTab self)
            {
                if (self is not FGTurboBakerTab fgtab)
                {
                    return;
                }
                fgtab.AddItems(fgtab.ThreadBakerButton = new OpSimpleButton(new Vector2(230f, 0f), new Vector2(80f, 30f), "Bake!!"));
                fgtab.ThreadBakerButton.description = "Added by Floodgate";
                fgtab.ThreadBakerButton.OnClick += fgtab._BakeClick;
            });
            c.GotoNext(MoveType.After,
                (Instruction x) => x.MatchLdarg(0),
                (Instruction x) => x.MatchLdcR4(250),
                (Instruction x) => x.MatchLdcR4(5),
                (Instruction x) => x.MatchLdstr("")
                );
            c.Prev.Previous.Operand = 45f;
        }
        catch (Exception ex)
        {
            CustomLog.LogError(ex.ToString());
        }
    }

    public static void IL_Update(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate(static delegate (TurboBakerTab self)
            {
                if(self is not FGTurboBakerTab fgtab)
                {
                    return;
                }
                fgtab.InnerUpdate();
            });
        }
        catch (Exception ex)
        {
            CustomLog.LogError(ex.ToString());
        }
    }

    public static readonly ConcurrentDictionary<string, RoomLoaderData> LoadRooms = new();
    public static void On_LoadAbstractRoom(On.WorldLoader.orig_LoadAbstractRoom orig, World world, string roomName, AbstractRoom room, RainWorldGame.SetupValues setupValues)
    {
        if(!LoadRooms.ContainsKey(roomName))
        {
            LoadRooms[roomName] = new(roomName);
        }
        Task task = new(() => { orig(world, roomName, room, setupValues); });
        LoadRooms[roomName].tasks.Enqueue(task);
        task.GetAwaiter().GetResult();
    }

    public static bool RoomSettings_Load(On.RoomSettings.orig_Load_Timeline orig, RoomSettings self, SlugcatStats.Timeline timelinePoint)
    {
        return false;
    }

    public class RoomLoaderData(string roomName)
    {
        public string roomname = roomName;
        public ConcurrentQueue<Task> tasks = new ConcurrentQueue<Task>();
        public Task lastTask = null;
        public bool Completed => lastTask is null || lastTask.IsCompleted;
    }

    public class FGTurboBakerTab : TurboBakerTab
    {
        public OpSimpleButton ThreadBakerButton;
        public Task BakerTask;
        public string statusText = "";
        public FGTurboBakerTab(OptionInterface owner) : base(owner)
        {
        }
        private int _updateTimer2 = 0;
        public void InnerUpdate()
        {
            if (!this.Baking && BakerTask is not null)
            {
                this._updateTimer2++;
                if (this._updateTimer2 >= 40)
                {
                    this._updateTimer2 = 0;
                    TimeSpan runTime = DateTime.Now - this.BakeStartTime;
                    this.StatusLabel.text = $"Baking Time: {runTime.Hours * 60 + runTime.Minutes:D2}:{runTime.Seconds:D2}\r\n{this.statusText}";
                }
            }
            ThreadBakerButton.greyedOut = BakeButton.greyedOut;
            if(BakerTask is not null && BakerTask.IsCompleted)
            {
                BakerTask.GetAwaiter().GetResult();
                BakerTask = null;
                Baking = true;
                On.WorldLoader.LoadAbstractRoom -= On_LoadAbstractRoom;
                On.RoomSettings.Load_Timeline -= RoomSettings_Load;
                statusText = "";

            }
        }

        public void _BakeClick(UIfocusable trigger)
        {
            On.WorldLoader.LoadAbstractRoom += On_LoadAbstractRoom;
            On.RoomSettings.Load_Timeline += RoomSettings_Load;
            //List<string> list = (from x in this.Regions
            //                     where x.Value.GetValueBool()
            //                     select x.Key).ToList<string>();
            //if (list.Count == 0)
            if(!(this.Regions.Any(x=>x.Value.GetValueBool())))
            {
                trigger.PlaySound(SoundID.MENU_Error_Ping);
                return;
            }
            this.BakeButton.greyedOut = trigger.greyedOut = true;
            this.BakeStartTime = DateTime.Now;
            BakerTask = Task.Run(this._TurboBake);
        }
        public void _TurboBake()
        {
            try
            {
                var parOpt = new ParallelOptions
                {
                    MaxDegreeOfParallelism = ValueExt.GetValueInt(ThreadsInput)
                };
                IEnumerable<string> regionsToBake = (from x in Regions
                                              where x.Value.GetValueBool()
                                              select x.Key);

                List<WorldLoader> loaders = new List<WorldLoader>();

                string[] names = ExtEnumBase.GetNames(typeof(SlugcatStats.Name));

                //Dictionary<string, IEnumerable<Region>> loadedRegions = new();

                statusText = "Creating World Loaders";

                foreach (string scug in names)
                {
                    SlugcatStats.Name name = new SlugcatStats.Name(scug);
                    if (!ValueExt.GetValueBool(HiddenSlugcatsInput) && SlugcatStats.HiddenOrUnplayableSlugcat(name))
                    {
                        continue;
                    }
                    //foreach (Region item in from x in Region.LoadAllRegions(SlugcatStats.SlugcatToTimeline(name), null)
                    //                        where regionsToBake.Contains(x.name)
                    //                        select x)
                    foreach (Region item in EnumerableLoadAllRegions(SlugcatStats.SlugcatToTimeline(name), null).Where(x=>regionsToBake.Contains(x.name)))
                    {
                        WorldLoader worldLoader = new WorldLoader(null, name, SlugcatStats.SlugcatToTimeline(name), singleRoomWorld: false, item.name, item, RainWorld.LoadSetupValues(distributionBuild: true), WorldLoader.LoadingContext.MAPMERGE);
                        worldLoader.NextActivity();
                        loaders.Add(worldLoader);
                    }
                }

                statusText = "Running World Loaders";
                Task.Run(async () =>
                {
                    while (loaders.Any(i => !i.Finished))
                    {
                        foreach (var i in LoadRooms)
                        {
                            if (i.Value.Completed && i.Value.tasks.TryDequeue(out Task absLoad))
                            {
                                i.Value.lastTask = absLoad;
                                absLoad.Start();
                            }
                        }
                        await Task.Delay(1);
                    }
                });

                Parallel.ForEach(loaders, parOpt, async (loader) =>
                {
                    while (!loader.Finished)
                    {
                        loader.Update();
                        await Task.Delay(1);
                    }
                });
                statusText = "Preparing rooms";
                List<string> rooms = new List<string>();
                foreach (WorldLoader loader in loaders)
                {
                    World world = loader.ReturnWorld();
                    for (int num2 = 0; num2 < loader.roomAdder.Count; num2++)
                    {
                        string text = loader.roomAdder[num2][0];
                        if (rooms.Contains(loader.roomAdder[num2][0]))
                        {
                            CustomLog.Log("Skipping already prepared room: " + text);
                            continue;
                        }
                        rooms.Add(text);
                        CustomLog.Log("Started preparing room: " + text);
                        string[] roomText = File.ReadAllLines(WorldLoader.FindRoomFile(text, includeRootDirectory: false, ".txt"));
                        if (int.Parse(roomText[9].Split('|')[0], NumberStyles.Any, CultureInfo.InvariantCulture) < world.preProcessingGeneration || ValueExt.GetValueBool(ForceBakeInput))
                        {
                            AbstractRoom abstractRoom = loader.abstractRooms[num2];
                            int generation = world.preProcessingGeneration;
                            Room room = new Room(null, world, abstractRoom);
                            RoomPreparer roomPreparer = new RoomPreparer(room, loadAiHeatMaps: false, falseBake: false, shortcutsOnly: false);
                            TaskData taskData = new TaskData(abstractRoom.name)
                            {
                                Size = room.Width * room.Height
                            };
                            CustomLog.Log("Done preparing room: " + abstractRoom.name);
                            Action bakingTask = delegate
                            {
                                try
                                {
                                    CustomLog.Log("Started baking room: " + abstractRoom.name);
                                    lock (taskData)
                                    {
                                        taskData.StartTime = DateTime.Now;
                                    }
                                    taskData.Started = true;
                                    RunToCompletion2(roomPreparer);
                                    abstractRoom.InitNodes(roomPreparer.ReturnRoomConnectivity(), roomText[1]);
                                    roomText[9] = global::RoomPreprocessor.ConnMapToString(generation, abstractRoom.nodes);
                                    roomText[10] = global::RoomPreprocessor.CompressAIMapsToString(room.aimap);
                                    File.WriteAllLines(WorldLoader.FindRoomFile(abstractRoom.name, includeRootDirectory: false, ".txt"), roomText);
                                    CustomLog.Log("Done baking room: " + abstractRoom.name);
                                    lock (taskData)
                                    {
                                        taskData.EndTime = DateTime.Now;
                                    }
                                    taskData.Finished = true;
                                }
                                catch (Exception ex2)
                                {
                                    CustomLog.LogError(ex2.ToString());
                                }
                            };
                            taskData.BakingTask = bakingTask;
                            Tasks.Add(taskData);
                        }
                        else
                        {
                            CustomLog.Log("Skipping already baked room: " + text);
                        }
                    }
                }
                Tasks = Tasks.OrderByDescending((TaskData x) => x.Size).ToList();
                ActualBakeStartTime = DateTime.Now;
                new Thread((ThreadStart)delegate
                {
                    Parallel.Invoke(parOpt, Tasks.Select((TaskData x) => x.BakingTask).ToArray());
                }).Start();
            }
            catch (Exception ex)
            {
                CustomLog.LogError(ex.ToString());
                ThreadBakerButton.PlaySound(SoundID.MENU_Error_Ping);
            }
        }

        public void RunToCompletion2(RoomPreparer preparer)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");
            while (!preparer.scMapper.done)
            {
                preparer.scMapper.Update();
            }
            preparer.scMapper = null;
            preparer.aiMapper = new AImapper(preparer.room);
            while (!preparer.aiMapper.done)
            {
                preparer.aiMapper.Update();
            }
            preparer.room.aimap = preparer.aiMapper.ReturnAIMap();
            preparer.aiDataPreprocessor = new AIdataPreprocessor(preparer.room.aimap, false);
            while (!preparer.aiDataPreprocessor.done)
            {
                preparer.aiDataPreprocessor.Update();
            }
        }
    }
}
