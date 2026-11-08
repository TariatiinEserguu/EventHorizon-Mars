namespace EventHorizon.Space
{
    /// <summary>Physical data for Solar System bodies (NASA planetary fact sheets).</summary>
    public static class PlanetCatalog
    {
        public readonly struct Body
        {
            public readonly string name;
            public readonly float meanRadiusKm;
            /// <summary>Sidereal rotation period. Retrograde spin is encoded by an axial tilt above 90 degrees.</summary>
            public readonly float rotationHours;
            public readonly float axialTiltDeg;
            public readonly string texture;          // file in Assets/ThirdParty/SolarSystemScope (or NASA)
            public readonly float ringInnerKm, ringOuterKm;

            public Body(string name, float r, float rot, float tilt, string tex, float ringIn = 0, float ringOut = 0)
            {
                this.name = name; meanRadiusKm = r; rotationHours = rot; axialTiltDeg = tilt; texture = tex;
                ringInnerKm = ringIn; ringOuterKm = ringOut;
            }
        }

        public static readonly Body Sun = new("Sun", 695700f, 609.12f, 7.25f, "2k_sun.jpg");
        public static readonly Body Mercury = new("Mercury", 2439.7f, 1407.6f, 0.03f, "2k_mercury.jpg");
        public static readonly Body Venus = new("Venus", 6051.8f, 5832.5f, 177.4f, "2k_venus_atmosphere.jpg");
        public static readonly Body Earth = new("Earth", 6371.0f, 23.9345f, 23.44f, "2k_earth_daymap.jpg");
        public static readonly Body Moon = new("Moon", 1737.4f, 655.72f, 6.68f, "2k_moon.jpg");
        public static readonly Body Mars = new("Mars", 3389.5f, 24.6229f, 25.19f, "2k_mars.jpg");
        public static readonly Body Jupiter = new("Jupiter", 69911f, 9.925f, 3.13f, "2k_jupiter.jpg");
        public static readonly Body Saturn = new("Saturn", 58232f, 10.656f, 26.73f, "2k_saturn.jpg", 74500f, 140220f);
        public static readonly Body Uranus = new("Uranus", 25362f, 17.24f, 97.77f, "2k_uranus.jpg");
        public static readonly Body Neptune = new("Neptune", 24622f, 16.11f, 28.32f, "2k_neptune.jpg");

        // Irregular moons: mean radius plus the triaxial semi-axes used by IrregularMoonMesh.
        public static readonly Body Phobos = new("Phobos", 11.27f, 7.654f, 0f, "Phobos.jpg");
        public static readonly Body Deimos = new("Deimos", 6.2f, 30.312f, 0f, "Deimos.jpg");
        public static readonly UnityEngine.Vector3 PhobosAxesKm = new(13.4f, 11.2f, 9.2f); // 26.8 x 22.4 x 18.4 km
        public static readonly UnityEngine.Vector3 DeimosAxesKm = new(7.5f, 6.1f, 5.2f);   // 15.0 x 12.2 x 10.4 km

        public static readonly Body[] All = { Sun, Mercury, Venus, Earth, Moon, Mars, Jupiter, Saturn, Uranus, Neptune, Phobos, Deimos };
    }
}
