using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;

namespace Floodgate.Steam;

public static class Workshop
{
    public static readonly Dictionary<ulong, DateTime> ModLastUpdatedDT = new Dictionary<ulong, DateTime>();
    private static CallResult<SteamUGCQueryCompleted_t> queryCallback;
    private static UGCQueryHandle_t lastQueryHandle;
    public static void Apply()
    {
        if (SteamManager.Initialized)
        {
            queryCallback = CallResult<SteamUGCQueryCompleted_t>.Create(OnQueryResult);

            AppId_t rwID = new AppId_t(RainWorldSteamManager.APP_ID);
            PublishedFileId_t[] modIds = ModManager.InstalledMods.Where(i => i.workshopMod).Select(i => (PublishedFileId_t)i.workshopId).ToArray();
            lastQueryHandle = SteamUGC.CreateQueryUGCDetailsRequest(modIds, (uint)modIds.Length);
            queryCallback.Set(SteamUGC.SendQueryUGCRequest(lastQueryHandle));
        }
    }

    public static void TryFetch()
    {
        if(SteamManager.Initialized && ModLastUpdatedDT.Count == 0)
        {
            AppId_t rwID = new AppId_t(RainWorldSteamManager.APP_ID);
            PublishedFileId_t[] modIds = ModManager.InstalledMods.Where(i => i.workshopMod).Select(i => (PublishedFileId_t)i.workshopId).ToArray();
            lastQueryHandle = SteamUGC.CreateQueryUGCDetailsRequest(modIds, (uint)modIds.Length);
            queryCallback.Set(SteamUGC.SendQueryUGCRequest(lastQueryHandle));
        }
    }

    public static void OnQueryResult(SteamUGCQueryCompleted_t callback, bool ioFailure)
    {
        if(callback.m_eResult == EResult.k_EResultOK)
        {
            for(uint i = 0u; i < callback.m_unTotalMatchingResults; i++)
            {
                if(SteamUGC.GetQueryUGCResult(lastQueryHandle, i, out var pDetails))
                {
                    ModLastUpdatedDT.Add((ulong)pDetails.m_nPublishedFileId, DateTimeOffset.FromUnixTimeSeconds(pDetails.m_rtimeUpdated).UtcDateTime);
                }
            }
        }
    }
}
