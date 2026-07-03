using System;
using Pulsar4X.DataStructures;
using Pulsar4X.Orbital;
using SDL3;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Input;

namespace Pulsar4X.Client
{
    class SysBodyIcon : Icon, IPointerHandler, IShape, IInteractable
    {
        SystemBodyInfoDB _systemBodyInfoDB;
        BodyType _bodyType;
        MassVolumeDB _massVolDB;
        double _bodyRadiusAU;
        float _viewRadius;
        Random _rng;
        float _iconMinSize = 8;
        int _entityId;
        string _sysId;

        public byte Priority { get { return 100; } }

        public SysBodyIcon(EntityState entity, SystemBodyInfoDB systemBodyInfoDB, PositionDB positionDB, MassVolumeDB massVolumeDB) : base(positionDB)
        {
            _positionDB = positionDB;
            _systemBodyInfoDB = systemBodyInfoDB;
            _bodyType = _systemBodyInfoDB.BodyType;
            _massVolDB = massVolumeDB;
            _bodyRadiusAU = _massVolDB.RadiusInAU;
            _entityId = entity.Id;
            _sysId = entity.StarSystemId;
            _rng = new Random(_entityId); //use entity guid as a seed for psudoRandomness.

            // Per-body-type colours. Note: non-asteroid bodies are drawn as a FILLED circle in Draw(),
            // which BRIGHTENS the base colour by +80 per channel (moons ×0.8). So these base values are
            // deliberately darker/muted — pick them by imagining +80 added. Asteroids draw as an outline
            // (no brighten), so their colour is used as-is.
            switch (_bodyType)
            {
                case BodyType.Asteroid:
                    Asteroid();
                    break;
                case BodyType.Terrestrial:
                    ColouredBody(85, 80, 55);    // rocky tan-green land world (NOT blue)
                    break;
                case BodyType.GasGiant:
                    ColouredBody(150, 110, 60);  // banded orange-tan (Jupiter/Saturn)
                    break;
                case BodyType.IceGiant:
                    ColouredBody(70, 120, 150);  // pale ice-blue (Uranus/Neptune) — blue belongs HERE
                    break;
                case BodyType.GasDwarf:
                    ColouredBody(90, 120, 100);  // muted teal-green
                    break;
                case BodyType.DwarfPlanet:
                    ColouredBody(130, 125, 115); // icy off-white (Pluto/Ceres)
                    break;
                case BodyType.Moon:
                    ColouredBody(95, 95, 100);   // rocky grey (Luna)
                    break;
                case BodyType.Comet:
                    ColouredBody(120, 150, 170); // icy blue-white
                    break;
                default:
                    Unknown();
                    break;
            }

            if (_bodyType == BodyType.Moon)
                _iconMinSize = 4;
        }

        public bool OnPointerUp(SDL.Event sevent)
        {
            if (_state == null)
                return false;
            var state = _state!;

            if (sevent.Button.Button == 1)
                state.EntityClicked(_entityId, _sysId, MouseButtons.Primary);
            else if (sevent.Button.Button == 3)
                state.EntityClicked(_entityId, _sysId, MouseButtons.Alt);
            return true;
        }

        public bool Contains(System.Drawing.PointF point)
        {
            System.Numerics.Vector2 v = new (ViewScreenPos.X, ViewScreenPos.Y);
            return System.Numerics.Vector2.Distance(v, point.ToVector2()) <= Scale * 100;
        }

        /// <summary>
        /// Builds a round body of a given base colour. Used by every non-asteroid body type
        /// (terrestrial, gas giant, ice giant, gas dwarf, dwarf planet, moon, comet). The colour
        /// here is the BASE; Draw() brightens it (+80/channel, ×0.8 for moons) when it fills the circle.
        /// </summary>
        void ColouredBody(byte r, byte g, byte b)
        {
            _iconMinSize = 8;
            short segments = 32;
            var points = CreatePrimitiveShapes.Circle(0, 0, 100, segments);

            SDL.Color colour = new SDL.Color() { R = r, G = g, B = b, A = 255 };
            Shapes.Add(new Shape() { Color = colour, Points = points });
        }

        void Asteroid()
        {
            _iconMinSize = 8;
            double vertDiameter = _rng.Next(50, 100);
            double horDiameter = _rng.Next(50, 100);
            int segments = _rng.Next(8, 32);
            int jagMax = _rng.Next(5, 8);
            int jagMin = _rng.Next(4, jagMax);

            var points = CreatePrimitiveShapes.CreateArc(0, 0, horDiameter, vertDiameter, 0, Math.PI * 2, segments);
            for (int i = 0; i < segments; i = i + 2)
            {
                //this is not right, need to pull the points in towards the center, not just pull them left.
                double x = points[i].X - _rng.Next(jagMin, jagMax);
                double y = points[i].Y - _rng.Next(jagMin, jagMax);
                points[i] = new Vector2() { X = x, Y = y };
            }
            //colors picked out of my ass for a brown look.
            //TODO: use minerals for this? but migth not have that info. going to have to work in with sensor stuff.
            byte r = 150;
            byte g = 100;
            byte b = 50;
            byte a = 255;
            SDL.Color colour = new SDL.Color() { R = r, G = g, B = b, A = a };
            Shapes.Add(new Shape() { Color = colour, Points = points });
        }

        void Unknown()
        {

            short segments = 24;
            var points = CreatePrimitiveShapes.Circle(0, 0, 100, segments);
            //colors picked out of my ass .
            //TODO: use minerals for this? but migth not have that info. going to have to work in with sensor stuff.
            byte r = 100;
            byte g = 100;
            byte b = 100;
            byte a = 255;
            SDL.Color colour = new SDL.Color() { R = r, G = g, B = b, A = a };
            Shapes.Add(new Shape() { Color = colour, Points = points });
        }


        public override void OnFrameUpdate(Matrix matrix, Camera camera)
        {
            _viewRadius = camera.ViewDistance(_bodyRadiusAU);
            if (_viewRadius < _iconMinSize)
                Scale = _iconMinSize * 0.01f;
            else
                Scale = _viewRadius * 0.01f;
            base.OnFrameUpdate(matrix, camera);
        }

        public override void Draw(IntPtr rendererPtr, Camera camera)
        {
            if (DrawShapes == null || DrawShapes.Length == 0)
                return;

            // Draw filled circle for non-asteroid body types
            if (_bodyType != BodyType.Asteroid)
            {
                var shape = DrawShapes[0];
                if (shape.Points != null && shape.Points.Length > 2)
                {
                    int cx = ViewScreenPos.X;
                    int cy = ViewScreenPos.Y;
                    int radius = (int)(Scale * 100);

                    if (radius > 0)
                    {
                        // Fill color derived from the body's base color, dimmed for moons. The additive lift used
                        // to be +80, which washed the discs out to pale tints; cut to +30 (2026-07-03, developer's
                        // "deeper shades of what they are") so a planet reads as a richer, deeper version of its hue
                        // while the disc still reads as filled (not the same as the dim base/outline colour).
                        const int fillLift = 30;
                        float brighten = _bodyType == BodyType.Moon ? 0.8f : 1.0f;
                        byte fillR = (byte)Math.Min(255, (int)((shape.Color.R + fillLift) * brighten));
                        byte fillG = (byte)Math.Min(255, (int)((shape.Color.G + fillLift) * brighten));
                        byte fillB = (byte)Math.Min(255, (int)((shape.Color.B + fillLift) * brighten));
                        SDL.SetRenderDrawColor(rendererPtr, fillR, fillG, fillB, shape.Color.A);
                        for (int y = -radius; y <= radius; y++)
                        {
                            int xSpan = (int)Math.Sqrt(radius * radius - y * y);
                            SDL.RenderLine(rendererPtr, cx - xSpan, cy + y, cx + xSpan, cy + y);
                        }
                    }
                }
                return; // skip outline for filled bodies
            }

            // Draw outline for asteroids
            base.Draw(rendererPtr, camera);
        }
    }
}

