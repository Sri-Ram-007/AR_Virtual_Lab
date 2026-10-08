using UnityEngine;

namespace ARVirtualLab.AppShell
{
    public enum AppLanguage { English = 0, Telugu = 1 }

    /// <summary>Student preferences, saved on the phone. Screens read them when they are built.</summary>
    public static class AppSettings
    {
        private const string KeyLanguage = "settings.language";
        private const string KeyDarkMode = "settings.darkMode";

        private static bool _loaded;
        private static AppLanguage _language;
        private static bool _darkMode;

        private static void Load()
        {
            if (_loaded) return;
            _language = (AppLanguage)PlayerPrefs.GetInt(KeyLanguage, (int)AppLanguage.English);
            _darkMode = PlayerPrefs.GetInt(KeyDarkMode, 0) == 1;
            _loaded = true;
        }

        public static AppLanguage Language
        {
            get { Load(); return _language; }
            set { Load(); _language = value; PlayerPrefs.SetInt(KeyLanguage, (int)value); PlayerPrefs.Save(); }
        }

        public static bool DarkMode
        {
            get { Load(); return _darkMode; }
            set { Load(); _darkMode = value; PlayerPrefs.SetInt(KeyDarkMode, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
