// ============================================================
// TextbookCatalog.cs  —  AR Virtual Lab
// The textbook pages the app can recognise, and the experiment each one opens.
// Single source of truth for both the Home screen and the Textbook Scan screen.
//
// To add a page: drop its picture into Assets/Resources/Textbook/ as <id>.bytes (the PNG file renamed to .bytes),
// optionally <id>_fig.bytes (a crop of just the figure – improves recognition), and add a row below.
// ============================================================
using ARVirtualLab.AppShell;

namespace ARVirtualLab.Scan
{
    public class TextbookPage
    {
        public string Id;            // also the reference-image name and the Resources file name
        public int    PageNumber;
        public string Activity;      // e.g. "Activity 1.1"
        public string Title;
        public string Chapter;
        public string Summary;
        public string SceneName;     // experiment scene to open
        public float  PageWidthMeters = 0.14f;    // real width of the printed page (Class 10 textbook ≈ 14 cm)
        public float  FigureWidthFraction = 0.85f; // width of the <id>_fig.bytes crop as a fraction of the page width

        public void Launch(bool arMode = false)
        {
            AppNavigation.LaunchScene(SceneName, arMode);
        }
    }

    public static class TextbookCatalog
    {
        public static readonly TextbookPage[] Pages =
        {
            new TextbookPage
            {
                Id = "page04_magnesium", PageNumber = 4, FigureWidthFraction = 0.88f, Activity = "Activity 1.1",
                Title = "Burning of a Magnesium Ribbon in Air",
                Chapter = "Chemical Reactions and Equations",
                Summary = "Clean the ribbon, hold it with tongs, burn it in the flame and collect the white ash in a watch-glass.",
                SceneName = AppNavigation.LAB_SCENE
            },
            new TextbookPage
            {
                Id = "page06_hydrogen", PageNumber = 6, FigureWidthFraction = 0.38f, Activity = "Activity 1.3",
                Title = "Zinc Granules with Dilute Acid",
                Chapter = "Chemical Reactions and Equations",
                Summary = "Add dilute acid to zinc granules in a conical flask and watch hydrogen gas bubble out.",
                SceneName = AppNavigation.HYDROGEN_SCENE
            },
            new TextbookPage
            {
                Id = "page42_gastest", PageNumber = 42, FigureWidthFraction = 0.86f, Activity = "Activity 2.3",
                Title = "Testing Hydrogen Gas by Burning",
                Chapter = "Acids, Bases and Salts",
                Summary = "Pass the gas into soap solution and bring a burning candle near a hydrogen-filled bubble.",
                SceneName = AppNavigation.HYDROGEN_GAS_TEST_SCENE
            }
        };

        /// <summary>Reference images are named "<id>" or "<id>_fig"; both map back to the same page.</summary>
        public static TextbookPage FindByReferenceName(string referenceName)
        {
            if (string.IsNullOrEmpty(referenceName)) return null;
            foreach (var p in Pages)
                if (referenceName == p.Id || referenceName == p.Id + "_fig") return p;
            return null;
        }
    }
}
