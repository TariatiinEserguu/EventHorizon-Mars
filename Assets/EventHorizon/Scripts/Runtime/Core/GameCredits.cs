using UnityEngine;

namespace EventHorizon.Core
{
    /// <summary>
    /// Third-party attribution for the credits screen. The text is generated from tools/asset_manifest.json by
    /// tools/fetch_assets.py, so it always matches ASSET_CREDITS.md. CC BY assets require it to ship.
    /// </summary>
    public static class GameCredits
    {
        public const string ResourcePath = "ThirdPartyCredits";

        public static string Load()
        {
            var text = Resources.Load<TextAsset>(ResourcePath);
            return text != null ? text.text : "Third-party credits missing: run python3 tools/fetch_assets.py --credits";
        }
    }
}
