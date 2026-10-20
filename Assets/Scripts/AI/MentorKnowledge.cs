// ============================================================
// MentorKnowledge.cs  —  AR Virtual Lab
// What the AI mentor knows about each experiment. The same text is used two ways:
//   * as the briefing sent to the language model (so its answers stay on this experiment and at Class 10 level), and
//   * for built-in answers when the phone is offline or no API key is set, matched by keywords in the question.
// Plain ASCII chemistry ("H2SO4", "->") so it renders in the app font on any phone.
// ============================================================
using System.Collections.Generic;

namespace ARVirtualLab.AI
{
    public class MentorQA
    {
        public string[] Keys;      // any of these words/phrases in the question selects this answer
        public string Answer;
        public string AnswerTe;    // Telugu version, shown when the student has chosen Telugu
    }

    public class MentorExperiment
    {
        public string ShownTitle { get { return ARVirtualLab.Localization.Loc.TeluguMode && !string.IsNullOrEmpty(TitleTe) ? TitleTe : Title; } }
        public string[] ShownQuickQuestions { get { return ARVirtualLab.Localization.Loc.TeluguMode && QuickQuestionsTe != null ? QuickQuestionsTe : QuickQuestions; } }
        public string Id, Title, Equation, Briefing;
        public string[] QuickQuestions;
        public string TitleTe;
        public string[] QuickQuestionsTe;
        public MentorQA[] Answers;
    }

    public static class MentorKnowledge
    {
        public const string Magnesium = "magnesium";
        public const string HydrogenLab = "hydrogenlab";
        public const string HydrogenGas = "hydrogengas";
        public const string Electrolysis = "electrolysis";
        public const string Acid = "acid";

        private static Dictionary<string, MentorExperiment> _all;

        public static MentorExperiment Get(string id)
        {
            if (_all == null) { Build(); MentorTelugu.Apply(_all); }
            MentorExperiment e;
            return _all.TryGetValue(id ?? "", out e) ? e : _all[Magnesium];
        }

        private static MentorQA QA(string answer, params string[] keys) { return new MentorQA { Keys = keys, Answer = answer }; }

        private static void Build()
        {
            _all = new Dictionary<string, MentorExperiment>();

            // ---------------------------------------------------------------- Magnesium
            _all[Magnesium] = new MentorExperiment
            {
                Id = Magnesium, Title = "Burning of Magnesium Ribbon", Equation = "2Mg + O2 -> 2MgO",
                Briefing =
                    "Experiment: burning a magnesium ribbon in air (Class 10 Chemistry, Chemical Reactions and Equations). " +
                    "A short strip of magnesium ribbon is cleaned with sandpaper to remove the dull magnesium oxide layer, held with tongs, " +
                    "and heated in a Bunsen burner flame. It burns with a dazzling white flame and leaves a white powder, magnesium oxide (MgO), " +
                    "collected in a watch glass. Equation: 2Mg + O2 -> 2MgO. It is a combination reaction, also a combustion and an oxidation reaction, " +
                    "and it is exothermic (gives out heat and light). Magnesium oxide dissolves a little in water to form magnesium hydroxide, a base " +
                    "that turns red litmus blue. Safety: never look directly at the burning ribbon (the bright light is harmful), hold it with tongs, " +
                    "keep long hair and loose clothes away from the flame.",
                QuickQuestions = new[] { "Why is the ribbon cleaned first?", "Why is the flame so bright?", "What is the white powder?", "What type of reaction is this?" },
                Answers = new[]
                {
                    QA("Magnesium ribbon is rubbed with sandpaper first because its surface is covered with a dull layer of magnesium oxide, formed slowly by reacting with oxygen in the air. That layer stops the metal from touching the flame and oxygen properly, so cleaning it lets the ribbon catch fire easily.", "clean", "sandpaper", "rub", "dull", "oxide layer", "why sand"),
                    QA("The flame is dazzling white because the reaction releases a huge amount of heat and light very quickly. The temperature is so high that it gives off intense white light, including ultraviolet rays. That is why you should never look straight at it, and why magnesium was once used in camera flash bulbs.", "bright", "dazzl", "white flame", "light", "flame so", "intense"),
                    QA("The white powder is magnesium oxide (MgO). It forms when magnesium combines with the oxygen of the air: 2Mg + O2 -> 2MgO. Some of it also floats off as white smoke.", "powder", "ash", "white solid", "formed", "product", "what is formed", "mgo"),
                    QA("It is a combination reaction, because two substances (magnesium and oxygen) join to make one new substance (magnesium oxide). It is also a combustion reaction (it burns in oxygen), an oxidation reaction (magnesium gains oxygen) and an exothermic reaction (it gives out heat and light).", "type", "combination", "combustion", "exothermic", "oxidation", "kind of reaction"),
                    QA("Tongs are used because the ribbon becomes extremely hot and the flame is very bright. Holding it with tongs keeps your fingers safe from the heat and sparks.", "tongs", "hold", "why use"),
                    QA("Safety: do not look directly at the burning ribbon, because the light is very bright and can hurt your eyes. Hold the ribbon with tongs, keep it away from your face, and tie back long hair.", "safe", "eye", "look at", "danger", "harm", "careful"),
                    QA("The balanced equation is 2Mg + O2 -> 2MgO. Two magnesium atoms react with one oxygen molecule (two oxygen atoms) to give two units of magnesium oxide. There are 2 Mg and 2 O on each side, so it is balanced.", "equation", "balanced", "formula", "write"),
                    QA("Magnesium oxide is a basic oxide. When it dissolves in water it forms magnesium hydroxide, Mg(OH)2, which turns red litmus paper blue. So the ash shows the product is basic.", "litmus", "basic", "base", "water", "dissolve", "ph"),
                }
            };

            // ---------------------------------------------------------------- Hydrogen lab (preparation)
            _all[HydrogenLab] = new MentorExperiment
            {
                Id = HydrogenLab, Title = "Preparing Hydrogen Gas", Equation = "Zn + H2SO4 -> ZnSO4 + H2",
                Briefing =
                    "Experiment: preparing hydrogen gas in the laboratory (Class 10 Chemistry, Chemical Reactions and Equations / Acids, Bases and Salts). " +
                    "Zinc granules are placed in a flask, dilute sulphuric acid is poured on them, and the flask is closed with a cork carrying a delivery tube. " +
                    "Bubbles of hydrogen form on the zinc (effervescence). Equation: Zn + H2SO4 -> ZnSO4 + H2 (g). It is a displacement reaction: zinc is more " +
                    "reactive than hydrogen, so it displaces hydrogen from the acid and forms zinc sulphate. Hydrogen is colourless, odourless, the lightest gas, " +
                    "and burns with a pop sound. Safety: keep flames away from the flask, handle acid with care, make sure the cork is tight so gas does not leak.",
                QuickQuestions = new[] { "Why do bubbles form?", "What is the gas formed?", "Why is the cork fitted tightly?", "What type of reaction is this?" },
                Answers = new[]
                {
                    QA("Bubbles form because zinc reacts with dilute sulphuric acid and releases hydrogen gas. The gas appears as bubbles on the surface of the zinc granules; this fizzing is called effervescence. Zn + H2SO4 -> ZnSO4 + H2.", "bubble", "fizz", "effervesc", "why bubbles"),
                    QA("The gas is hydrogen (H2). It is colourless and odourless, it is the lightest of all gases, and it burns with a 'pop' sound when a flame is brought near.", "gas", "what is formed", "product", "hydrogen"),
                    QA("The cork is fitted tightly so that no hydrogen escapes from the flask. The gas then has only one way out, through the delivery tube, where it can be collected or tested safely.", "cork", "tight", "leak", "delivery tube", "stopper"),
                    QA("It is a displacement reaction: zinc is more reactive than hydrogen, so zinc displaces hydrogen from the sulphuric acid and takes its place, forming zinc sulphate and releasing hydrogen gas.", "type", "displacement", "single", "replace", "reactive"),
                    QA("Zn + H2SO4 -> ZnSO4 + H2. Zinc reacts with sulphuric acid to give zinc sulphate (a salt) and hydrogen gas. It is already balanced: 1 Zn, 2 H, 1 S and 4 O on each side.", "equation", "balanced", "formula"),
                    QA("Safety: hydrogen is flammable, so keep naked flames away from the flask while the gas is being made. Handle the dilute acid carefully, keep it off your skin and eyes, and make sure the cork is tight.", "safe", "danger", "careful", "flame", "acid"),
                    QA("Dilute sulphuric acid is used because it reacts steadily with zinc. Very strong acid would react too violently, and pure water would not react at all.", "dilute", "why acid", "sulphuric", "sulfuric"),
                }
            };

            // ---------------------------------------------------------------- Hydrogen gas test
            _all[HydrogenGas] = new MentorExperiment
            {
                Id = HydrogenGas, Title = "Testing Hydrogen Gas", Equation = "2H2 + O2 -> 2H2O",
                Briefing =
                    "Experiment: testing hydrogen gas with soap bubbles and a flame (Class 10 Chemistry). " +
                    "Zinc granules react with dilute sulphuric acid in a test tube fixed to a stand: Zn + H2SO4 -> ZnSO4 + H2. The hydrogen travels through a delivery tube " +
                    "into soap solution and forms bubbles filled with hydrogen, which rise because hydrogen is lighter than air. A burning candle brought near a bubble " +
                    "makes the hydrogen burn with a 'pop' sound; this is the test for hydrogen. When hydrogen burns it combines with oxygen to form water: 2H2 + O2 -> 2H2O. " +
                    "Safety: do not bring the flame near the test tube, only near the bubbles.",
                QuickQuestions = new[] { "Why do the bubbles rise?", "Why does it make a pop sound?", "How do we test for hydrogen?", "What is formed when hydrogen burns?" },
                Answers = new[]
                {
                    QA("The bubbles rise because they are filled with hydrogen, which is much lighter than air, so the bubbles float upwards like small balloons.", "rise", "float", "lighter", "up", "why bubbles"),
                    QA("The pop is the sound of hydrogen burning very quickly. The small amount of hydrogen in the bubble combines with oxygen of the air in an instant, releasing heat and making a sudden expansion of gas that we hear as a pop.", "pop", "sound", "noise", "burn"),
                    QA("To test for hydrogen, bring a burning candle or splinter near the gas (here, near a soap bubble). If the gas is hydrogen it burns with a squeaky 'pop' sound.", "test", "identify", "confirm", "how do we"),
                    QA("When hydrogen burns it combines with oxygen to form water: 2H2 + O2 -> 2H2O. That is why hydrogen is called a clean fuel.", "formed", "product", "water", "burns"),
                    QA("Soap solution is used because it traps the gas in thin bubbles that we can see and carry to the flame. Plain water would just let the gas escape as bubbles that vanish at once.", "soap", "why soap", "solution"),
                    QA("Safety: never hold the flame near the test tube where the gas is being made, only near the bubbles in the air. Hydrogen is flammable and mixes explosively with air in larger amounts.", "safe", "danger", "careful", "explo"),
                    QA("The gas comes from zinc reacting with dilute sulphuric acid: Zn + H2SO4 -> ZnSO4 + H2. It passes through the delivery tube into the soap solution.", "where", "gas come", "zinc", "acid", "equation"),
                }
            };

            // ---------------------------------------------------------------- Electrolysis
            _all[Electrolysis] = new MentorExperiment
            {
                Id = Electrolysis, Title = "Electrolysis of Water", Equation = "2H2O -> 2H2 + O2",
                Briefing =
                    "Experiment: electrolysis of water (Class 10 Chemistry, Chemical Reactions and Equations, Activity 1.7). " +
                    "Two carbon (graphite) electrodes are fixed through the base of a plastic mug and joined to a 6 volt battery. The mug is filled with water and a few drops of " +
                    "dilute sulphuric acid are added, because pure water is a poor conductor and the acid supplies ions. Test tubes filled with water are inverted over each electrode. " +
                    "When the current is switched on, bubbles of gas form at both electrodes: hydrogen collects at the cathode (negative) and oxygen at the anode (positive). " +
                    "The volume of hydrogen is twice the volume of oxygen, matching the formula H2O. Tests: the hydrogen burns with a pop; oxygen makes a burning candle burn brighter. " +
                    "Equation: 2H2O (l) -> 2H2 (g) + O2 (g). It is a decomposition reaction caused by electricity.",
                QuickQuestions = new[] { "Why is acid added to the water?", "Which gas collects at the cathode?", "Why is there twice as much hydrogen?", "How do we test the gases?" },
                Answers = new[]
                {
                    QA("Pure water conducts electricity very poorly. A few drops of dilute sulphuric acid put ions in the water, so a current can flow and the water can be split into hydrogen and oxygen. The acid itself is not used up.", "acid", "why add", "drops", "sulphuric", "sulfuric", "conduct"),
                    QA("Hydrogen gas collects at the cathode, the negative electrode. Oxygen collects at the anode, the positive electrode.", "cathode", "anode", "which gas", "negative", "positive", "electrode"),
                    QA("Water is H2O: every molecule has two hydrogen atoms for one oxygen atom. When water splits, 2H2O gives 2H2 + O2, so twice as many hydrogen molecules are produced as oxygen molecules, and the hydrogen tube fills about twice as fast.", "twice", "double", "more hydrogen", "volume", "ratio", "2:1"),
                    QA("Test the gases with a flame. Hydrogen burns with a squeaky 'pop'. Oxygen does not burn itself, but it makes a burning candle flare up and burn brighter.", "test", "identify", "flame", "candle", "pop", "confirm"),
                    QA("It is a decomposition reaction: one compound (water) breaks down into simpler substances (hydrogen and oxygen), here using electrical energy. Such a reaction is called electrolysis.", "type", "decomposition", "electrolysis", "kind of reaction"),
                    QA("The equation is 2H2O -> 2H2 + O2. Two molecules of water split into two molecules of hydrogen and one molecule of oxygen. It is balanced: 4 H and 2 O on each side.", "equation", "balanced", "formula", "write"),
                    QA("Carbon (graphite) electrodes are used because carbon conducts electricity and is not attacked by the acid or the gases formed, so it does not dissolve or react.", "carbon", "graphite", "why electrode", "rod"),
                    QA("Safety: dilute sulphuric acid can irritate skin and eyes, so handle it carefully. Keep the 6 volt supply away from spills, and bring the flame only near the mouth of a test tube, not inside it.", "safe", "danger", "careful"),
                }
            };

            // ---------------------------------------------------------------- Acid conductivity
            _all[Acid] = new MentorExperiment
            {
                Id = Acid, Title = "Do Solutions Conduct Electricity?", Equation = "HCl -> H+ + Cl-",
                Briefing =
                    "Experiment: do all solutions conduct electricity? (Class 10 Chemistry, Acids, Bases and Salts, Activity 2.8). " +
                    "Two nails fixed on a cork dip into a beaker. They are connected through a switch and a bulb to a 6 volt battery. Different solutions are poured into the beaker. " +
                    "With dilute hydrochloric acid (HCl) and dilute sulphuric acid (H2SO4) the bulb glows, because acids break up in water into ions (HCl -> H+ + Cl-) and moving ions carry the current. " +
                    "With glucose and alcohol solutions the bulb stays dark: they contain hydrogen but do not form ions in water, so they do not conduct. " +
                    "Conclusion: not every compound containing hydrogen is an acid; an acid gives hydrogen ions (H+) in water. The beaker is emptied and rinsed between solutions so they do not mix.",
                QuickQuestions = new[] { "Why does the bulb glow with acid?", "Why not with glucose?", "What are ions?", "Why empty the beaker between tests?" },
                Answers = new[]
                {
                    QA("The bulb glows because the acid dissolves in water and breaks up into ions, for example HCl -> H+ + Cl-. These ions are free to move, and moving ions carry the electric current round the circuit, so the bulb lights.", "glow", "light up", "bulb", "why acid", "acid conduct", "current"),
                    QA("Glucose and alcohol contain hydrogen, but they dissolve in water as whole molecules and do not form ions. With no ions to carry the current, the circuit is not completed through the liquid and the bulb stays dark. So having hydrogen alone does not make a substance an acid.", "glucose", "alcohol", "not glow", "dark", "does not", "doesn't", "why not"),
                    QA("Ions are atoms or groups of atoms that carry an electric charge. In an acid solution there are positive hydrogen ions (H+) and negative ions such as chloride (Cl-). Because they can move in the liquid, they carry electric current.", "ion", "charge", "what are"),
                    QA("The beaker is emptied and rinsed between tests so that the earlier solution does not mix with the next one. A mixture would give a misleading result, since you would not know which liquid made the bulb glow.", "empty", "rinse", "waste", "pour out", "wash", "mix", "between"),
                    QA("An acid is a substance that gives hydrogen ions (H+) when dissolved in water. Hydrochloric acid and sulphuric acid do this, so they conduct electricity. This experiment shows that containing hydrogen is not enough.", "what is an acid", "acid", "define", "hydrogen"),
                    QA("The two nails are the electrodes: they carry the current from the battery into the solution and out again. The bulb shows whether current is flowing, and the switch lets you close or open the circuit.", "nail", "electrode", "cork", "battery", "switch", "circuit"),
                    QA("HCl -> H+ + Cl- for hydrochloric acid, and H2SO4 -> 2H+ + SO4 (2-) for sulphuric acid. Both release hydrogen ions in water.", "equation", "formula", "ionis", "ioniz", "dissociat"),
                    QA("Safety: dilute hydrochloric and sulphuric acid can irritate skin and eyes, so handle them carefully and wash your hands afterwards. Never taste any of the liquids.", "safe", "danger", "careful", "taste"),
                }
            };
        }
    }
}
