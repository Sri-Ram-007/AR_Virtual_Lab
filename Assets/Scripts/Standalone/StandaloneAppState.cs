// ============================================================
// StandaloneAppState.cs  —  AR Virtual Lab Standalone
// Enums for the standalone virtual lab experiment.
// ============================================================
namespace ARVirtualLab.Standalone
{
    public enum StandaloneState
    {
        Introduction,
        Ready,              // Step 1: Take ribbon
        CleaningRibbon,     // Step 2: Rub ribbon against sandpaper
        RibbonReady,        // Ribbon is clean
        TongsReady,         // Step 3: Ribbon held with tongs
        BurnerPreparation,  // Step 4: Light burner
        Heating,            // Step 5: Hold in flame (heating delay)
        Burning,            // Step 6: Combustion (bright white flame)
        Cooling,            // Step 7: Cool down
        Collection,         // Step 8: Drag to watch glass
        Completed           // Show observation, equation, result
    }

    public enum StandaloneObjectType
    {
        None,
        MagnesiumRibbon,
        Sandpaper,
        Tongs,
        BunsenBurner,
        WatchGlass,
        MgOResidue
    }
}
