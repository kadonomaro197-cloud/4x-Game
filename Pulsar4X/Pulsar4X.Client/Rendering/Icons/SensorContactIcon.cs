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
            // A stale contact can lose its position parent, so AbsolutePosition (read by the base) can throw NaN/
            // null. The Draw side is SafeDraw-wrapped, but this update loop is NOT — guard it so one bad contact
            // can't abort the whole frame's icon updates.
            try
            {
                base.OnFrameUpdate(matrix, camera);   // builds the glyph shapes + sets ViewScreenPos
            }
            catch
            {
                return;
            }
            // Park the name just lower-right of the blip (a unit-style label, minus the leader line).
            _nameRect.X = ViewScreenPos.X + 9;
            _nameRect.Y = ViewScreenPos.Y + 5;
        }

        public override void Draw(IntPtr rendererPtr, Camera camera)
        {
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
