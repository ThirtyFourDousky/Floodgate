using Menu.Remix.MixedUI;
using UnityEngine;

namespace Floodgate;

public class RemixInterface : OptionInterface
{

    public OpCheckBox _showWorkshopDate;
    public readonly Configurable<bool> ShowWorkshopDate;

    public RemixInterface()
    {
        ShowWorkshopDate = config.Bind("fgshowworkshop", false);
    }

    public override void Initialize()
    {
        Vector2 a = new Vector2(300f, 443f);
        Vector2 b = new Vector2(20f, 450f);
        Vector2 c = new Vector2(0f, -30f);
        OpTab floodgateOptions = new(this, "Remix Options");
        int separator = 0;
        floodgateOptions.AddItems(
            new OpLabel(10, 540, "Floodgate", true) { alignment = FLabelAlignment.Left },
            _showWorkshopDate = new(ShowWorkshopDate, a + c * separator), new OpLabel(b + c * separator++, new(300f,24f), "Show Workshop Last Update")
            );

        OpTab debug = new(this, "Debug");
        separator = 0;
        OpSimpleButton rescan = new OpSimpleButton(b + c * separator++, new(300f,24f), "Rescan Floodgate Paths");
        rescan.OnClick += Rescan_OnClick;
        debug.AddItems(
            new OpLabel(10, 540, "Floodgate", true) { alignment = FLabelAlignment.Left },
            rescan
            );
        Tabs = [floodgateOptions, debug];
    }

    private void Rescan_OnClick(UIfocusable trigger)
    {
        Registry.Rescan();
        World.CustomMerger.Rescan();
    }
}
