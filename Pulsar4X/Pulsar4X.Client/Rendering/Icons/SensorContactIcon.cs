using System;
using Pulsar4X.Orbital;
using Pulsar4X.Sensors;
using SDL3;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The map blip for a SENSOR CONTACT — what a faction has DETECTED of another entity, drawn like a unit but
    /// carrying ONLY the information the player actually has, which varies: a diamond marker at the last-known
    /// position, the contact's name, and — once the real entity is gone and we're coasting on its last-known
    /// ("memory") position — a faded "(last known)" ghost. This is the visible half of fog of war: the star and
    /// the planets are always on the map, but an enemy fleet only shows up here once your sensors find it; lose
    /// the track (it leaves range, or is destroyed) and the blip fades to where you last saw it.
    ///
    /// Built on <see cref="Icon"/> so it reuses the world->screen transform and the NaN-guarded line draw. The
    /// NAME is rendered with the same SDL TTF path <see cref="EntityLabel"/> uses (cached texture, freed in the
    /// finalizer). Everything is drawn through SystemMapRendering's per-item SafeDraw, so a glitch on one blip
    /// logs once and skips instead of blanking the map.
    /// </summary>
    public class SensorContactIcon : Icon
    {
        readonly SensorContact _contact;
        readonly bool _hostile;

        IntPtr _nameTexture = IntPtr.Zero;
        SDL.FRect _nameRect = new();
        SDL.Color _color;
        string _drawnText;   // the string currently baked into _nameTexture (recreate only when it changes)

        // On-screen / finite-coordinate CULL — mirrors OrbitEllipseIcon + SimpleCircle (root CLAUDE.md gotcha #15,
        // findings/A1-freeze.md H1, the freeze the developer hit). A contact's last-known position can go DEGENERATE:
        // a NaN/infinite AbsolutePosition when a track's anchor is lost, or a fogLag drift (the A1 on-ramp — the log
        // showed fogLag jump to ~25,000 km right before the stop) that parks the blip astronomically off-screen. The
        // base Icon.Draw CLAMPS such a coordinate to int.Min/MaxValue instead of THROWING, so SystemMapRendering's
        // SafeDraw try/catch never fires — and SDL.RenderLine then chokes rasterising a line whose endpoint is
        // astronomically off-screen, frame time climbs, and the client FREEZES (a native hang, no exception, and
        // CI-invisible by construction). So the fix is to stop the bad coordinate BEFORE it reaches SDL: if the blip's
        // world or on-screen position is non-finite, or its screen coordinate is far past a sane pixel bound, set
        // _offScreenSkip and skip both the transform and the draw. Recomputed every frame, so a contact whose position
        // becomes valid again (or scrolls back on-screen) redraws next frame — a per-frame "worth drawing right now?"
        // decision, not a permanent removal. A NORMAL, on-screen, finite blip takes the unchanged base path.
        bool _offScreenSkip;
        // A blip's diamond is ~9 px; any screen coordinate more than this far off-screen is pure clutter AND the SDL
        // rasteriser choke. Comfortably past any real viewport, well within int range (matches SimpleCircle.MaxSafeCoordPx).
        const double _maxBlipScreenCoordPx = 1_000_000.0;

        public SensorContactIcon(SensorContact contact, bool hostile) : base(contact.Position)
        {
            _contact = contact;
            _hostile = hostile;
            BuildGlyph();
        }

        /// <summary>A diamond unit-marker. Red if hostile, amber if unknown/neutral; dimmer when it's a stale
        /// (memory) ghost. A touch larger for a louder — closer / bigger-signature — contact, clamped to a band so
        /// it always reads as a marker.</summary>
        void BuildGlyph()
        {
            byte alpha = (byte)(_contact.PositionIsMemory ? 90 : 205);
            _color = _hostile
                ? new SDL.Color { R = 225, G = 70, B = 70, A = alpha }
                : new SDL.Color { R = 215, G = 185, B = 70, A = alpha };

            double strength = _contact.SignalStrength_kW;
            float size = (float)Math.Max(5.0, Math.Min(9.0, 5.0 + Math.Log10(Math.Max(1.0, strength))));

            Vector2[] points =
            {
                new Vector2 { X = 0,     Y = size },
                new Vector2 { X = size,  Y = 0 },
                new Vector2 { X = 0,     Y = -size },
                new Vector2 { X = -size, Y = 0 },
                new Vector2 { X = 0,     Y = size },
            };
            Shapes.Clear();
            Shapes.Add(new Shape { Points = points, Color = _color });
        }

        public override void OnFrameUpdate(Matrix matrix, Camera camera)
        {
            _offScreenSkip = false;
            // A stale contact can lose its position parent, so AbsolutePosition (read below and by the base) can throw
            // OR return NaN/infinite. The Draw side is SafeDraw-wrapped (catches THROWS), but a THROW isn't the freeze
            // — the freeze is SDL choking on a clamped-but-extreme coordinate that never throws (see the field comment).
            // So cull the degenerate/off-screen case HERE, before the base transform hands it to the draw. Wrap the
            // whole read: a bad contact just skips this frame's draw instead of aborting every icon's update.
            try
            {
                var world = WorldPosition_m;
                var screen = camera.ViewCoordinateV2_m(world);
                if (!double.IsFinite(world.X) || !double.IsFinite(world.Y) || !double.IsFinite(world.Z)
                    || !double.IsFinite(screen.X) || !double.IsFinite(screen.Y)
                    || Math.Abs(screen.X) > _maxBlipScreenCoordPx || Math.Abs(screen.Y) > _maxBlipScreenCoordPx)
                {
                    _offScreenSkip = true;
                    return;
                }
                base.OnFrameUpdate(matrix, camera);   // builds the glyph shapes + sets ViewScreenPos
            }
            catch
            {
                _offScreenSkip = true;
                return;
            }
            // Park the name just lower-right of the blip (a unit-style label, minus the leader line).
            _nameRect.X = ViewScreenPos.X + 9;
            _nameRect.Y = ViewScreenPos.Y + 5;
        }

        public override void Draw(IntPtr rendererPtr, Camera camera)
        {
            if (_offScreenSkip) return;   // degenerate/off-screen this frame — culled in OnFrameUpdate so no extreme
                                          // coordinate reaches SDL.RenderLine (the native-hang guard, gotcha #15).
            base.Draw(rendererPtr, camera);           // the diamond

            // The name is the "what you know": shown plainly when tracked, "(last known)" when it's a memory
            // ghost. Rebuild the texture only when that displayed string actually changes.
            string text = _contact.PositionIsMemory ? _contact.Name + " (last known)" : _contact.Name;
            if (string.IsNullOrEmpty(text)) return;

            if (!string.Equals(text, _drawnText, StringComparison.Ordinal))
            {
                DestroyName();
                _drawnText = text;
            }
            if (_nameTexture == IntPtr.Zero && !RenderName(rendererPtr, text)) return;
            if (!camera.IsOnScreen(_nameRect.X, _nameRect.Y, _nameRect.W, _nameRect.H)) return;

            SDL.RenderTexture(rendererPtr, _nameTexture, IntPtr.Zero, in _nameRect);
        }

        bool RenderName(IntPtr rendererPtr, string text)
        {
            if (Styles.SDLDefaultFont == IntPtr.Zero) return false;

            IntPtr surface = SDL3.TTF.RenderTextSolid(Styles.SDLDefaultFont, text, 0, _color);
            if (surface == IntPtr.Zero) return false;

            _nameTexture = SDL.CreateTextureFromSurface(rendererPtr, surface);
            if (_nameTexture == IntPtr.Zero) { SDL.DestroySurface(surface); return false; }
            SDL.DestroySurface(surface);

            SDL3.TTF.GetStringSize(Styles.SDLDefaultFont, text, 0, out int w, out _);
            _nameRect.W = w;
            _nameRect.H = SDL3.TTF.GetFontHeight(Styles.SDLDefaultFont);
            return true;
        }

        void DestroyName()
        {
            if (_nameTexture == IntPtr.Zero) return;
            var p = _nameTexture;
            _nameTexture = IntPtr.Zero;
            SDL.DestroyTexture(p);
        }

        ~SensorContactIcon() => DestroyName();
    }
}
