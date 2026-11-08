using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace EventHorizon.Core
{
    /// <summary>A language listed in Resources/Localization/languages.csv.</summary>
    public class LanguageInfo
    {
        public string code;        // "en", "mn"
        public string folder;      // "English", "Mongolian"
        public string nativeName;  // "English", "Монгол хэл"
        public string englishName;
        public bool hasVoice;      // recorded voice lines exist for this language
    }

    /// <summary>
    /// Data-driven localization. Every visible string has a stable key (MENU_NEW_GAME, Q_C02_TITLE ...). Text lives in
    /// CSV tables under Resources/Localization/&lt;Language&gt;/ (UI, Missions, Dialogue, Science, Tutorials), one
    /// "key,text" row per string. Adding a language means adding a folder and a row in languages.csv.
    ///
    /// Interface, subtitle and voice languages are separate settings. Changing a language raises <see cref="Changed"/>
    /// and every screen rebuilds itself, so no restart is needed. Missing keys fall back to English, then to the key.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";

        static List<LanguageInfo> s_languages;
        static readonly Dictionary<string, Dictionary<string, string>> s_tables = new();
        static readonly HashSet<string> s_reported = new();

        /// <summary>Raised after the interface or subtitle language changes.</summary>
        public static event Action Changed;

        public static IReadOnlyList<LanguageInfo> Languages => s_languages ??= LoadLanguages();

        static LanguageSettings Settings => SettingsService.Current.language;

        /// <summary>Interface language code.</summary>
        public static string Current => Valid(Settings.ui);
        public static string SubtitleLanguage => Valid(string.IsNullOrEmpty(Settings.subtitles) ? Settings.ui : Settings.subtitles);
        /// <summary>Voice language; falls back to English while a language has no recordings.</summary>
        public static string VoiceLanguage
        {
            get
            {
                var v = Info(Valid(Settings.voice));
                return v != null && v.hasVoice ? v.code : English;
            }
        }

        public static LanguageInfo Info(string code)
        {
            foreach (var l in Languages) if (l.code == code) return l;
            return null;
        }

        static string Valid(string code) => Info(code) != null ? code : English;

        public static void SetLanguage(string code, bool alsoSubtitles = true, bool save = true)
        {
            code = Valid(code);
            var s = Settings;
            bool subtitlesFollowed = string.IsNullOrEmpty(s.subtitles) || s.subtitles == s.ui;
            s.ui = code;
            if (alsoSubtitles && subtitlesFollowed) s.subtitles = code;
            if (save) { s.chosen = true; SettingsService.Save(); }
            Changed?.Invoke();
        }

        public static void SetSubtitleLanguage(string code)
        {
            Settings.subtitles = Valid(code);
            SettingsService.Save();
            Changed?.Invoke();
        }

        public static void SetVoiceLanguage(string code)
        {
            Settings.voice = Valid(code);
            SettingsService.Save();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ lookup

        /// <summary>Text for <paramref name="key"/> in the interface language.</summary>
        public static string T(string key) => Get(Current, key);

        /// <summary>Formatted text: placeholders {0}, {1} ... are filled with <paramref name="args"/> (invariant numbers).</summary>
        public static string T(string key, params object[] args) => Format(Get(Current, key), args);

        /// <summary>Text in the subtitle language (dialogue, radio, recordings).</summary>
        public static string Sub(string key) => Get(SubtitleLanguage, key);

        public static string Sub(string key, params object[] args) => Format(Get(SubtitleLanguage, key), args);

        /// <summary>Text in a specific language, without falling back to the interface language.</summary>
        public static string In(string language, string key) => Get(Valid(language), key);

        public static bool Has(string key) => Table(English).ContainsKey(key) || Table(Current).ContainsKey(key);

        /// <summary>Text if the key exists, otherwise <paramref name="fallback"/> (for data that may predate a key).</summary>
        public static string TOr(string key, string fallback) => !string.IsNullOrEmpty(key) && Has(key) ? T(key) : fallback;

        public static string Upper(string s) => s?.ToUpper(CultureInfo.InvariantCulture);

        public static string Format(string text, params object[] args)
        {
            if (args == null || args.Length == 0) return text;
            try { return string.Format(CultureInfo.InvariantCulture, text, args); }
            catch (FormatException)
            {
                Report("format:" + text, $"[Event Horizon] Bad placeholders in localized text: \"{text}\"");
                return text;
            }
        }

        static string Get(string language, string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (Table(language).TryGetValue(key, out var text)) return text;
            if (language != English && Table(English).TryGetValue(key, out text))
            {
                Report(language + ":" + key, $"[Event Horizon] Missing {language} translation for {key}; using English.");
                return text;
            }
            Report("missing:" + key, $"[Event Horizon] Missing localization key {key}.");
            return key;
        }

        static void Report(string id, string message)
        {
            if (s_reported.Add(id)) Debug.LogWarning(message);
        }

        // ------------------------------------------------------------------ loading

        /// <summary>All key/text pairs for a language (used by the validator and the language test).</summary>
        public static Dictionary<string, string> Table(string language)
        {
            if (s_tables.TryGetValue(language, out var t)) return t;
            t = new Dictionary<string, string>();
            var info = Info(language);
            if (info != null)
            {
                foreach (var asset in Resources.LoadAll<TextAsset>("Localization/" + info.folder))
                    foreach (var row in ParseCsv(asset.text))
                    {
                        if (row.Length < 2 || string.IsNullOrWhiteSpace(row[0]) || row[0] == "key" || row[0].StartsWith("#")) continue;
                        t[row[0].Trim()] = row[1].Replace("\\n", "\n");
                    }
            }
            s_tables[language] = t;
            return t;
        }

        /// <summary>Drops cached tables so edited CSV files are re-read (editor tools).</summary>
        public static void Reload()
        {
            s_tables.Clear();
            s_languages = null;
            Changed?.Invoke();
        }

        static List<LanguageInfo> LoadLanguages()
        {
            var list = new List<LanguageInfo>();
            var asset = Resources.Load<TextAsset>("Localization/languages");
            if (asset != null)
                foreach (var row in ParseCsv(asset.text))
                {
                    if (row.Length < 4 || row[0] == "code" || row[0].StartsWith("#")) continue;
                    list.Add(new LanguageInfo
                    {
                        code = row[0].Trim(), folder = row[1].Trim(), nativeName = row[2].Trim(), englishName = row[3].Trim(),
                        hasVoice = row.Length > 4 && row[4].Trim().Equals("yes", StringComparison.OrdinalIgnoreCase),
                    });
                }
            if (list.Count == 0) list.Add(new LanguageInfo { code = English, folder = "English", nativeName = "English", englishName = "English", hasVoice = true });
            return list;
        }

        /// <summary>RFC 4180 CSV: comma-separated, fields may be quoted, "" is a quote, quoted fields may span lines.</summary>
        public static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"': quoted = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(field.ToString()); field.Clear();
                        rows.Add(row.ToArray()); row.Clear();
                        break;
                    case '﻿': break; // byte-order mark
                    default: field.Append(c); break;
                }
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
            return rows;
        }
    }

    /// <summary>Language choices, saved with the other settings.</summary>
    [Serializable]
    public class LanguageSettings
    {
        public string ui = Loc.English;
        public string subtitles = Loc.English;
        public string voice = Loc.English;
        /// <summary>False until the player picks a language on first start.</summary>
        public bool chosen;
    }
}
