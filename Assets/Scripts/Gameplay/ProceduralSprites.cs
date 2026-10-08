using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>Generates small reusable sprites at runtime for effects that don't need real art
    /// (a soft glow used as a tintable buff aura behind the player ship, the player's beam).</summary>
    public static class ProceduralSprites
    {
        private static Sprite _softGlow;

        public static Sprite SoftGlow
        {
            get
            {
                if (_softGlow == null) _softGlow = BuildSoftGlow();
                return _softGlow;
            }
        }

        /// <summary>World-space width of one beam tile at scale 1 - PlayerBeam scales it from here.</summary>
        public const float BeamBodyWidthUnits = 0.8f;
        public const int BeamFrameCount = 8;
        private const int BeamTextureSize = 64;
        private static Sprite[] _beamFrames;

        /// <summary>Looping animation of the player's beam body: each frame is a seamless,
        /// vertically tileable slice - a faint red haze, three thin braided strands whose brightness
        /// ripples along the length, and a white-hot centre line, with soft alpha falloff to the
        /// edges. Successive frames slide the ripples along the strands, so playing them in order
        /// makes energy appear to stream away from the ship. The colour ramps deep red -> orange ->
        /// yellow-white by intensity, so the bright strands read as heat rather than flat paint.</summary>
        public static Sprite[] BeamFrames
        {
            get
            {
                if (_beamFrames == null || _beamFrames.Length == 0 || _beamFrames[0] == null) _beamFrames = BuildBeamFrames();
                return _beamFrames;
            }
        }

        private static Sprite[] BuildBeamFrames()
        {
            Sprite[] frames = new Sprite[BeamFrameCount];
            for (int i = 0; i < BeamFrameCount; i++)
                frames[i] = BuildBeamFrame(i * Mathf.PI * 2f / BeamFrameCount);
            return frames;
        }

        private static Sprite BuildBeamFrame(float phaseShift)
        {
            int size = BeamTextureSize;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                // Repeat so bilinear filtering blends the top row with the bottom one - no seam
                // line where two tiles meet.
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            float[] strandX = { -0.48f, 0f, 0.48f };
            float[] strandPhase = { 0f, 2.1f, 4.2f };
            Color deepRed = new Color(0.85f, 0.04f, 0.05f);
            Color orange = new Color(1f, 0.38f, 0.1f);
            Color hot = new Color(1f, 0.95f, 0.7f);

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float edgeFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, Mathf.Abs(u)));

                    float intensity = 0.3f * Mathf.Exp(-u * u * 2.4f);
                    for (int i = 0; i < strandX.Length; i++)
                    {
                        float d = u - strandX[i];
                        // Two ripples per tile - a whole number so the tile still repeats exactly.
                        float ripple = 0.7f + 0.3f * Mathf.Sin(v * Mathf.PI * 4f + strandPhase[i] - phaseShift);
                        intensity += ripple * Mathf.Exp(-d * d / 0.012f) * 0.75f;
                    }
                    intensity += Mathf.Exp(-u * u / 0.01f) * 0.55f;

                    float t = Mathf.Clamp01(intensity);
                    Color c = t < 0.5f ? Color.Lerp(deepRed, orange, t / 0.5f) : Color.Lerp(orange, hot, (t - 0.5f) / 0.5f);
                    c.a = Mathf.Clamp01(0.12f + intensity * 0.95f) * edgeFade;
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            float pixelsPerUnit = size / BeamBodyWidthUnits;
            // Full Rect: SpriteRenderer's Tiled mode only repeats a sprite that has a plain quad mesh.
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Natural size of the halo sprite: 1 world unit wide, 4 tall - PlayerBeam scales it.</summary>
        public const float BeamHaloWidthUnits = 1f;
        public const float BeamHaloHeightUnits = 4f;
        private static Sprite _beamHalo;

        /// <summary>The soft glow around the beam: a single smooth sprite (no strands) that fades in
        /// from nothing at the muzzle and out again at the far end, so unlike a tiled strip it has no
        /// hard start edge sitting in front of the ship's nose.</summary>
        public static Sprite BeamHalo
        {
            get
            {
                if (_beamHalo == null) _beamHalo = BuildBeamHalo();
                return _beamHalo;
            }
        }

        private static Sprite BuildBeamHalo()
        {
            const int w = 32;
            const int h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color tint = new Color(1f, 0.32f, 0.12f);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float vertical = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.16f, v)) *
                                 (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, v)));
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float horizontal = Mathf.Exp(-u * u * 3.2f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, Mathf.Abs(u))));
                    Color c = tint;
                    c.a = horizontal * vertical;
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w / BeamHaloWidthUnits);
        }

        private static Sprite _white;

        /// <summary>A plain white square sprite - gives Filled-type UI Images something to fill.</summary>
        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
                    Color32[] pixels = new Color32[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                }
                return _white;
            }
        }

        private static Sprite _dot;

        /// <summary>A solid soft-edged circle - the red notification badge on the Missions icon.</summary>
        public static Sprite Dot
        {
            get
            {
                if (_dot == null) _dot = BuildDot();
                return _dot;
            }
        }

        private static Sprite BuildDot(int size = 64)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite _spark;

        /// <summary>A short streak with a hot head on the right and a tail fading out to the left,
        /// thin and soft at the edges - the flying sparks of the beam's impact splash. Drawn
        /// white so each spark can be tinted from yellow-white down to red as it cools.</summary>
        public static Sprite Spark
        {
            get
            {
                if (_spark == null) _spark = BuildSpark();
                return _spark;
            }
        }

        private static Sprite BuildSpark(int width = 64, int height = 16)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height * 2f - 1f;                       // -1..1 across the streak
                float across = Mathf.Exp(-v * v * 5f);
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;                               // 0 tail .. 1 head
                    float along = Mathf.Pow(u, 1.6f);
                    float head = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, u));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(across * along * head * 1.4f)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite _ring;

        /// <summary>A soft white ring (circle outline with a faint inner glow) - the touch-point
        /// marker of the first-run "drag to move" hint. The UI pack has no ring sprite; its
        /// "BlueDot" is a rounded square, which is what that hint used to show.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null) _ring = BuildRing();
                return _ring;
            }
        }

        private static Sprite BuildRing(int size = 128)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
                    float ring = Mathf.Exp(-Mathf.Pow((t - 0.82f) / 0.07f, 2f));
                    float inner = t < 0.82f ? 0.16f * (1f - t / 0.82f) : 0f;
                    float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.94f, 1f, t));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring + inner) * edge));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildSoftGlow(int size = 128)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
                    float alpha = Mathf.Clamp01(1f - t);
                    alpha *= alpha; // sharper falloff toward the edge
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
