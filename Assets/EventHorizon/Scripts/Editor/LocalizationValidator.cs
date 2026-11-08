using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EventHorizon.Core;
using UnityEditor;
using UnityEngine;

namespace EventHorizon.EditorTools
{
    /// <summary>
    /// Checks the localization tables before every build:
    ///  - every language has the same table files and the same keys as English, with the same {0} placeholders;
    ///  - every literal key used in code (Loc.T("KEY") ...) exists;
    ///  - every character in each table exists in the font that draws that language (Fira for Mongolian).
    /// Story keys built from ids are checked by the in-game story test (-eh-storytest).
    /// Writes Logs/LocalizationReport.txt.
    /// </summary>
    public static class LocalizationValidator
    {
        static readonly Regex CodeKey = new(@"Loc\.(?:T|TOr|Sub|In|Has)\(\s*(?:[A-Za-z_.]+\s*,\s*)?""([A-Z][A-Z0-9_]+)""");
        static readonly Regex Placeholder = new(@"\{(\d+)[^}]*\}");

        [MenuItem("Event Horizon/Validate Localization", priority = 120)]
        public static int Validate()
        {
            Loc.Reload();
            var report = new StringBuilder();
            int errors = 0;
            var en = Loc.Table(Loc.English);
            report.AppendLine($"English: {en.Count} strings");

            // Same files in every language folder.
            string root = "Assets/EventHorizon/Resources/Localization";
            var files = new Dictionary<string, HashSet<string>>();
            foreach (var lang in Loc.Languages)
            {
                string dir = Path.Combine(root, lang.folder);
                files[lang.code] = Directory.Exists(dir) ? new HashSet<string>(Directory.GetFiles(dir, "*.csv").Select(Path.GetFileName)) : new HashSet<string>();
            }
            foreach (var lang in Loc.Languages.Where(l => l.code != Loc.English))
            {
                var t = Loc.Table(lang.code);
                var missing = en.Keys.Where(k => !t.ContainsKey(k)).ToList();
                var extra = t.Keys.Where(k => !en.ContainsKey(k)).ToList();
                var args = en.Keys.Where(k => t.ContainsKey(k) && Args(en[k]) != Args(t[k])).ToList();
                var tables = files[Loc.English].Except(files[lang.code]).ToList();
                report.AppendLine($"{lang.englishName}: {t.Count} strings, {missing.Count} missing, {extra.Count} extra, {args.Count} placeholder mismatches, {tables.Count} missing tables");
                foreach (var k in missing) report.AppendLine("  missing: " + k);
                foreach (var k in extra) report.AppendLine("  extra: " + k);
                foreach (var k in args) report.AppendLine($"  placeholders differ: {k}  [{Args(en[k])}] vs [{Args(t[k])}]");
                foreach (var f in tables) report.AppendLine("  missing table: " + f);
                errors += missing.Count + args.Count + tables.Count;
            }

            // Literal keys in code.
            var used = new SortedSet<string>();
            foreach (var path in Directory.GetFiles("Assets/EventHorizon/Scripts/Runtime", "*.cs", SearchOption.AllDirectories))
                foreach (Match m in CodeKey.Matches(File.ReadAllText(path)))
                    used.Add(m.Groups[1].Value);
            // Keys ending in "_" are prefixes completed at runtime ("ACTION_" + name); the story test checks those.
            var undefined = used.Where(k => !k.EndsWith("_") && !en.ContainsKey(k)).ToList();
            report.AppendLine($"Code: {used.Count} literal keys, {undefined.Count} undefined");
            foreach (var k in undefined) report.AppendLine("  undefined in English: " + k);
            errors += undefined.Count;

            // Glyph coverage.
            foreach (var (code, fontPath) in new[] { ("en", "Assets/EventHorizon/Fonts/Rajdhani-Medium.ttf"), ("mn", "Assets/EventHorizon/Fonts/FiraSansCondensed-Medium.ttf") })
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                var fallback = AssetDatabase.LoadAssetAtPath<Font>("Assets/EventHorizon/Fonts/FiraSansCondensed-Medium.ttf");
                if (font == null || Loc.Info(code) == null) continue;
                var chars = new SortedSet<char>(Loc.Table(code).Values.SelectMany(v => v).Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)));
                font.RequestCharactersInTexture(new string(chars.ToArray()));
                fallback.RequestCharactersInTexture(new string(chars.ToArray()));
                var absent = chars.Where(c => !font.HasCharacter(c) && !fallback.HasCharacter(c)).ToList();
                report.AppendLine($"Glyphs ({code}): {chars.Count} distinct characters, {absent.Count} missing from {Path.GetFileName(fontPath)} and the Fira fallback" +
                                  (absent.Count > 0 ? ": " + new string(absent.ToArray()) : ""));
                errors += absent.Count;
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/LocalizationReport.txt", report.ToString());
            string summary = $"[Event Horizon] Localization: {errors} errors. Full report: Logs/LocalizationReport.txt";
            if (errors > 0) Debug.LogError(summary + "\n" + report); else Debug.Log(summary);
            return errors;
        }

        static string Args(string s) => string.Join(",", Placeholder.Matches(s).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));
    }
}
