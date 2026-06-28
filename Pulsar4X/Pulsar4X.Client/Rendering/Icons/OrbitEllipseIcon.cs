using System;
using Pulsar4X.Orbital;
using SDL3;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar4X.Client
{




    /// <summary>
    /// Orbit draw data.
    /// How this works:
    /// First, we set up all the static non changing variables from the entites datablobs.
    /// On Setup we create a list of points for the full ellipse, as if it was orbiting around 0,0 world coordinates. (focalPoint).
    /// this list is stored in world coordinates since view coordinates will change freqently with zoom, pan etc.
    /// we also store the orbitAngle (Longitude of the periapsis, which should be the Argument of Periapsis + Longdidtude of the Accending Node, in 2d orbits we just add these together and use the LoP)
    /// On Update we calculate the angle from the center of the ellipse to the orbiting entity. TODO: (this *should* only be called when the game updates, but is currently called each frame)
    /// On Draw we translate the points to correct for the position in world view, and for the viewscreen and camera positions as well as zoom.
    /// We then find the index in the Point Array (created in Setup) that will be where the orbiting entity is, using the angle from the center of the ellipse to the orbiting entity.
    /// Using this index we create a tempory array of only the points which will be in the drawn portion of the ellipse (UserOrbitSettings.EllipseSweepRadians) which start from where the entity should be.
    /// We start drawing segments from where the planet will be, and decrease the alpha channel for each segment.
    /// On ajustments to settings from the user, we re-calculate needed info for that. (if the number of segments change, we have to recreate the point indiex so we run setup in that case)
    /// </summary>
    public class OrbitEllipseIcon : OrbitIconBase
    {
        internal OrbitEllipseIcon(EntityState entityState, List<List<UserOrbitSettings>> settings): base(entityState, settings)
        {

            TrajectoryType = UserOrbitSettings.OrbitTrajectoryType.Elliptical;


            UpdateUserSettings();
            CreatePointArray();
            OnPhysicsUpdate();

        }

        // PERF / freeze guard (2026-06-26): at extreme zoom an orbit ring can be MILLIONS of pixels across — pure
        // off-screen clutter — yet the fixed 181-point transform + 180-line draw still run for it EVERY frame, and
        // SDL chokes rasterizing lines with astronomically off-screen endpoints. The cost stays full no matter how
        // little is on screen, so zooming in piles on more huge rings until a frame never finishes (the live freeze:
        // a ship orbiting a Jupiter moon, zoomed way in). When an orbit's on-screen radius is absurd we skip it
        // entirely (it isn't a usable ring at that scale); the orbit you zoomed in to SEE is screen-sized and still draws.
        const double _auInMetresInverse = 6.6859e-12;      // 1 / (metres per AU) — matches the OnFrameUpdate transform scale
        const double _maxOrbitScreenRadiusPx = 50000.0;    // ~25+ screens; beyond this an orbit ring is off-screen clutter
        bool _offScreenSkip;

        protected override void CreatePointArray()
        {
            _points = new Vector2[_numberOfArcSegments + 1];

            // Use the same position formula as OrbitMath.GetPosition() so the rendered
            // orbit matches actual body positions. The old code used a simplified 2D
            // rotation by LoP which ignored inclination, causing orbit arcs to be offset
            // from body positions in generated systems with significant inclinations.
            double loAN = _orbitDB.LongitudeOfAscendingNode;
            double aoP = _orbitDB.ArgumentOfPeriapsis;
            double incl = _orbitDB.Inclination;
            double e = _eccentricity;
            double a = SemiMaj;
            double semiLatusRectum = a * (1.0 - (double)e * e);

            double cosLoAN = Math.Cos(loAN);
            double sinLoAN = Math.Sin(loAN);
            double cosIncl = Math.Cos(incl);

            for (int i = 0; i < _numberOfArcSegments + 1; i++)
            {
                double trueAnomaly = i * _segmentArcSweepRadians;
                double r = semiLatusRectum / (1.0 + e * Math.Cos(trueAnomaly));

                double angleFromLoAN = trueAnomaly + aoP;
                double cosAngle = Math.Cos(angleFromLoAN);
                double sinAngle = Math.Sin(angleFromLoAN);

                // Full 3D rotation matching OrbitMath.GetPosition(), projected to 2D
                double x = (cosLoAN * cosAngle - sinLoAN * sinAngle * cosIncl) * r;
                double y = (sinLoAN * cosAngle + cosLoAN * sinAngle * cosIncl) * r;

                _points[i] = new Vector2() { X = x, Y = y };
            }
        }





        /*
         * this gets the index by attempting to find the angle between the body and the center of the ellipse. possibly faster, but math is hard.
         * TODO: try doing this using EccentricAnomaly.
        public override void OnPhysicsUpdate()
        {

            //adjust so moons get the right positions
            Vector4 pos = _bodyPositionDB.AbsolutePosition;// - ParentPositionDB.AbsolutePosition;
            PointD pointD = new PointD() { x = pos.X, y = pos.Y };


            //adjust for focal point
            pos.X += _focalDistance;

            //rotate to the LonditudeOfPeriapsis.
            double x2 = (pos.X * Math.Cos(-_orbitAngleRadians)) - (pos.Y * Math.Sin(-_orbitAngleRadians));
            double y2 = (pos.X * Math.Sin(-_orbitAngleRadians)) + (pos.Y * Math.Cos(-_orbitAngleRadians));

            _ellipseStartArcAngleRadians = (float)(Math.Atan2(y2, x2));  //Atan2 returns a value between -180 and 180;

            //PointD pnt = Points.OrderBy(p => CalcDistance(p, new PointD() {x = pos.X, y = pos.Y })).First();

            //get the indexPosition in the point array we want to start drawing from: this should be the segment where the planet is.
            double unAdjustedIndex = (_ellipseStartArcAngleRadians / _segmentArcSweepRadians);
            while (unAdjustedIndex < 0)
            {
                unAdjustedIndex += (2 * Math.PI);
            }
            _index = (int)unAdjustedIndex;

        }
*/

        public override void OnPhysicsUpdate()
        {
            Vector3 pos = BodyPositionDB.RelativePosition;
            _bodyrelativePos = new Vector2() { X = pos.X, Y = pos.Y };
            var apos = BodyPositionDB.AbsolutePosition;
            _bodyAbsolutePos = new Vector2(apos.X, apos.Y);

            //we find the point in the ellipse which is closest to the body so we can start drawing from the body.
            double minDist = (_bodyrelativePos - _points[_index]).Length();

            for (int i =0; i < _points.Count(); i++)
            {
                double dist = (_bodyrelativePos - _points[i]).Length();
                if (dist < minDist)
                {
                    minDist = dist;
                    _index = i;
                }
            }
        }

        public override void OnFrameUpdate(Matrix matrix, Camera camera)
        {
            //resize for zoom
            //translate to position

            var foo = camera.ViewCoordinateV2_m(WorldPosition_m); //camera position and zoom

            var trns = Matrix.IDTranslate(foo.X, foo.Y);
            var scAU = Matrix.IDScale(6.6859E-12, 6.6859E-12);
            var mtrx =  scAU * matrix * trns; //scale to au, scale for camera zoom, and move to camera position and zoom

            // PERF guard: skip an orbit whose on-screen radius is absurd (off-screen clutter at extreme zoom) — skips
            // BOTH the transform below and the draw. Same scale the transform uses: radius_m * (1/AU) * zoom = pixels.
            double orbitScreenRadiusPx = SemiMaj * (1.0 + _eccentricity) * _auInMetresInverse * camera.ZoomLevel;
            _offScreenSkip = orbitScreenRadiusPx > _maxOrbitScreenRadiusPx;
            if (_offScreenSkip)
                return;

            // Transform full orbit points for ghost rendering
            for (int i = 0; i < _numberOfArcSegments + 1 && i < _fullOrbitDrawPoints.Length; i++)
            {
                _fullOrbitDrawPoints[i] = mtrx.TransformToSDL_Point(_points[i].X, _points[i].Y);
            }

            int index = _index;
            var spos = camera.ViewCoordinateV2_m(_bodyAbsolutePos);

            // Pin the ghost orbit point at the body's index to the actual body screen position
            // so the ghost and tail meet at the same spot on large orbits
            if (_index < _fullOrbitDrawPoints.Length)
                _fullOrbitDrawPoints[_index] = new SDL.Point(){ X = (int)spos.X, Y = (int)spos.Y };

            //_drawPoints[0] = mtrx.TransformToSDL_Point(_bodyrelativePos.X, _bodyrelativePos.Y);
            _drawPoints[0] = new SDL.Point(){ X = (int)spos.X, Y = (int)spos.Y};
            for (int i = 1; i < _numberOfDrawSegments; i++)
            {
                if (index > 0)
                    index--;
                else
                    index = _numberOfArcSegments - 1;

                _drawPoints[i] = mtrx.TransformToSDL_Point(_points[index].X, _points[index].Y);
            }
        }



        // Per-segment off-screen cull (the zoom-stutter fix, 2026-06-27). At extreme zoom an on-screen orbit's far
        // segments land astronomically off-screen, and SDL.RenderLine chokes rasterising lines whose endpoints are
        // huge — measured at ~1400 ms/frame, all in map-draw (the [STATE] stage breakdown). This is a cheap trivial-
        // reject (Cohen–Sutherland): if BOTH endpoints sit beyond the same screen edge (+ margin) the segment can't
        // cross the view, so skip it. The visible arc still draws; only the expensive off-screen wrap is dropped.
        const int OffScreenCullMargin = 2000;
        static bool SegOffScreen(int x0, int y0, int x1, int y1, int w, int h)
        {
            if (x0 < -OffScreenCullMargin && x1 < -OffScreenCullMargin) return true;
            if (x0 > w + OffScreenCullMargin && x1 > w + OffScreenCullMargin) return true;
            if (y0 < -OffScreenCullMargin && y1 < -OffScreenCullMargin) return true;
            if (y0 > h + OffScreenCullMargin && y1 > h + OffScreenCullMargin) return true;
            return false;
        }

        public override void Draw(IntPtr rendererPtr, Camera camera)
        {
            if (_offScreenSkip) return;   // absurdly large on-screen orbit — skipped in OnFrameUpdate (see the PERF guard there)
            //now we draw a line between each of the points in the translatedPoints[] array.
            if (_drawPoints.Count() < _numberOfDrawSegments - 1)
                return;

            int vw = (int)camera.ViewPortSize.X, vh = (int)camera.ViewPortSize.Y;

            // Draw faded full orbit ghost underneath
            byte ghostAlpha = _userSettings.GhostOrbitAlpha;
            if (ghostAlpha > 0 && _fullOrbitDrawPoints.Length > 1)
            {
                SDL.SetRenderDrawColor(rendererPtr, _userSettings.Red, _userSettings.Grn, _userSettings.Blu, ghostAlpha);
                for (int i = 0; i < _numberOfArcSegments; i++)
                {
                    var a = _fullOrbitDrawPoints[i]; var b = _fullOrbitDrawPoints[i + 1];
                    if (SegOffScreen(a.X, a.Y, b.X, b.Y, vw, vh)) continue;
                    SDL.RenderLine(rendererPtr, a.X, a.Y, b.X, b.Y);
                }
            }

            // Draw the bright tail on top
            float alpha = _userSettings.MaxAlpha;
            for (int i = 0; i < _numberOfDrawSegments - 1; i++)
            {
                SDL.SetRenderDrawColor(rendererPtr, _userSettings.Red, _userSettings.Grn, _userSettings.Blu, (byte)alpha);//we cast the alpha here to stop rounding errors creaping up.
                var a = _drawPoints[i]; var b = _drawPoints[i + 1];
                if (!SegOffScreen(a.X, a.Y, b.X, b.Y, vw, vh))
                    SDL.RenderLine(rendererPtr, a.X, a.Y, b.X, b.Y);
                alpha -= _alphaChangeAmount;
            }
        }
    }


}
