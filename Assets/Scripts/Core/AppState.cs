// ============================================================
// AppState.cs
// Central enums for the AR Virtual Lab state machine.
// ============================================================
namespace ARVirtualLab.Core
{
    /// <summary>
    /// Top-level application states.
    /// </summary>
    public enum AppState
    {
        /// <summary>App launched, waiting for Vuforia + camera.</summary>
        Initializing,

        /// <summary>Camera running, scanning for image target.</summary>
        Scanning,

        /// <summary>Image target detected – showing experiment menu.</summary>
        Menu,

        /// <summary>Instructional video is playing.</summary>
        VideoPhase,

        /// <summary>Interactive experiment is active.</summary>
        ExperimentPhase,

        /// <summary>Experiment completed – results panel visible.</summary>
        ResultPhase,

        /// <summary>AI explanation panel is open.</summary>
        AIPhase
    }

    /// <summary>
    /// Steps inside the Magnesium Ribbon experiment.
    /// </summary>
    public enum ExperimentStep
    {
        NotStarted = 0,
        ShowMagnesiumRibbon = 1,
        ShowLabSetup = 2,
        HeatRibbon = 3,
        BurningAnimation = 4,
        ShowMgO = 5,
        ShowEquation = 6,
        ShowObservation = 7,
        Completed = 8
    }

    /// <summary>
    /// Type tag for interactable lab objects.
    /// </summary>
    public enum LabObjectType
    {
        None,
        MagnesiumRibbon,
        Tongs,
        Burner,
        Flame,
        MagnesiumOxide
    }
}
