using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodgatePatcher
{
    internal static class CustomLog
    {
        public static bool active = false;
        internal static void Log(params string[] message)
        {
            if (!active)
            {
                Patcher.logger.LogDebug(message);
                return;
            }
            foreach(string msg in message)
            {
                Log(msg);
            }
        }
        internal static void Log(string message)
        {
            if (!active)
            {
                Patcher.logger.LogDebug(message);
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(ModLoader.LogPath, true))
                {
                    writer.WriteLine(message);
                }
            }catch
            {
                return;
            }
        }
        internal static void LogError(string message)
        {
            if (!active)
            {
                Patcher.logger.LogDebug(message);
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(ModLoader.LogPath, true))
                {
                    writer.WriteLine("[[ERROR]]" + message);
                }
            }
            catch
            {
                return;
            }
        }
    }
}
