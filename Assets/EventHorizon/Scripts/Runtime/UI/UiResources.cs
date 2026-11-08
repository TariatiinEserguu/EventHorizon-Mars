using UnityEngine;

namespace EventHorizon.UI
{
    /// <summary>
    /// Textures and sounds the menus use, gathered in one asset (Resources/UI/UiResources) so they are included in
    /// builds. The editor builder fills it from the downloaded asset packs.
    /// </summary>
    [CreateAssetMenu(menuName = "Event Horizon/UI Resources", fileName = "UiResources")]
    public class UiResources : ScriptableObject
    {
        public Texture2D mars, earth, moon, sun, saturn, stars, phobos;
        public AudioClip hover, click, back, confirm, error;
        public AudioClip previewEffects, previewAmbient, previewVoice, previewMusic;

        static UiResources s_instance;
        public static UiResources Instance => s_instance != null ? s_instance : s_instance = Resources.Load<UiResources>("UI/UiResources");

        public Texture2D Art(string key) => key switch
        {
            "mars" => mars,
            "earth" => earth,
            "moon" => moon,
            "sun" => sun,
            "saturn" => saturn,
            "stars" => stars,
            "blackhole" => stars,
            "phobos" => phobos,
            _ => mars,
        };
    }
}
