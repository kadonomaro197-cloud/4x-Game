using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Pulsar4X.Blueprints;
using Pulsar4X.Orbital;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Damage
{
    [Flags]
    public enum Connections
    {
        Skin = 1,
        Front = 2,
        Back = 4,
        Sides = 8,
        Structural = 15
    }

    /// <summary>
    /// Merge this into materials?
    /// </summary>
    public class DamageResistBlueprint: Blueprint
    {
        public byte IDCode;
        public int HitPoints;
        public int MeltingPoint;
        public float Density;

        // Wavelength absorption per band: UV(0-400nm), Vis(400-700nm), NIR(700-2000nm), MIR(2000-5000nm), FIR(5000+nm)
        // 0.0 = fully transparent (no energy absorbed), 1.0 = fully absorbing (all energy deposited here, beam stops)
        public float[] WavelengthAbsorption = { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };

        // [JsonConstructor] + [JsonProperty("UniqueID")] maps the JSON "UniqueID" field (which is the string key
        // used for the Blueprint dictionary) to the byte iDCode used as the color-channel key in the damage bitmap.
        // Without this, Newtonsoft can't match "UniqueID" to "iDCode" and the lookup table stays empty at runtime.
        [JsonConstructor]
        public DamageResistBlueprint([JsonProperty("UniqueID")] byte iDCode, int hitPoints, float density)
        {
            IDCode = iDCode;
            HitPoints = hitPoints;
            Density = density; //kg/m^3
            if (DamageTools.DamageResistsLookupTable.ContainsKey(iDCode))
                DamageTools.DamageResistsLookupTable[iDCode] = this;
            else
                DamageTools.DamageResistsLookupTable.Add(IDCode, this);
        }
    }

    public struct DamageFragment
    {
        public Vector2 Velocity;
        public (int x,int y) Position;
        public double Energy;
        public float Mass;
        public float Momentum;
        public float Density;//kg/m^3
        public float Length;
        public double Wavelength; // wavelength of beam in nm; 0 = kinetic/non-photon damage
    }

    public static class DamageTools
    {
        public static Dictionary<byte, DamageResistBlueprint> DamageResistsLookupTable = new Dictionary<byte, DamageResistBlueprint>()
        {

        };


        public static RawBmp LoadFromBitMap(string file)
        {


            if(!File.Exists(file))
                throw new FileNotFoundException();

            byte[] bmpBytes = File.ReadAllBytes(file);
            (int offset,int size)[] headerDef = new (int,int)[12];
            headerDef[0] = (0,2); //first two bytes should be BM in ascii
            headerDef[1] = (2, 4); //size of bmp in bytes (whole file size)
            headerDef[3] = (6, 2); //reserved creation application dependant
            headerDef[4] = (6, 8); //reserved creaton applicaton dependant
            headerDef[5] = (10, 4); //startAddressOf imageByteArray
            //bitmapInfo:
            headerDef[6] = (14, 4); //headerSize in bytes: should be 12?
            headerDef[7] = (18, 4); //bmp width in px (ushort)
            headerDef[8] = (22, 4); //bmp height in px (ushort)
            headerDef[9] = (26, 2); //num of colour planes = 1
            headerDef[10] = (28, 2); //bits per px, shoudl be our colour depth;
            headerDef[11] = (34, 4); //image Size (raw bmp data)
            byte[][] headerDat = new byte[12][];

            for (int i = 0; i < headerDef.Length; i++)
            {
                int size = headerDef[i].size;
                int offset = headerDef[i].offset;
                headerDat[i] = new byte[headerDef[i].size];
                for (int j = 0; j < size; j++)
                {
                    headerDat[i][j] = bmpBytes[offset + j];
                }

            }

            int filesize = BitConverter.ToInt32(headerDat[1]);
            int start = BitConverter.ToInt16(headerDat[5]);
            int width = BitConverter.ToInt16(headerDat[7]);
            int height = BitConverter.ToInt16(headerDat[8]);
            int depth = BitConverter.ToInt16(headerDat[10]) / 8;//we want depth in bytes.
            int sizeAry = BitConverter.ToUInt16(headerDat[11]);

            //Note: the bmp is stored in memory above starting at the bottom left.

            // Create an array to store image data
            byte[] imageData = new byte[4 * width * height];
            // Use the Marshal class to copy image data
            Buffer.BlockCopy(bmpBytes, start, imageData, 0, sizeAry);

            //byte[] imageData2 = new byte[4 * width * height];
            RawBmp newBmp = new RawBmp()
            {
                ByteArray = imageData,
                Depth = depth,
                Height = height,
                Width = width,
                Stride = depth * width
            };
            return newBmp;



        }

        public static Color FromByte(byte byteColor)
        {
            byte r = byteColor;
            byte g = byteColor;
            byte b = byteColor;
            Color color = Color.FromArgb(255,r, g, b);
            return color;
        }

        public static DamageResistBlueprint FromColor(Color color)
        {
            byte id = color.R;
            return DamageResistsLookupTable[id];
        }
        // Returns which of the five wavelength bands (UV/Vis/NIR/MIR/FIR) a given wavelength (nm) falls into.
        private static float GetWavelengthAbsorption(DamageResistBlueprint material, double wavelength_nm)
        {
            int band;
            if (wavelength_nm <= 0)   band = 2; // kinetic/unset: treat as near-IR
            else if (wavelength_nm < 400)  band = 0; // UV
            else if (wavelength_nm < 700)  band = 1; // Visible
            else if (wavelength_nm < 2000) band = 2; // Near-IR
            else if (wavelength_nm < 5000) band = 3; // Mid-IR
            else                           band = 4; // Far-IR

            if (material.WavelengthAbsorption == null || band >= material.WavelengthAbsorption.Length)
                return 0.5f;
            return material.WavelengthAbsorption[band];
        }

        public static (List<(byte id, int damageAmount)> damageToComponents, List<RawBmp> damageFrames) DealDamageEnergyBeamSim(EntityDamageProfileDB damageProfile, DamageFragment damage)
        {
            RawBmp shipDamageProfile = damageProfile.DamageProfile;

            List<RawBmp> damageFrames = new List<RawBmp>();
            List<(byte id, int damageAmount)> damageToComponents = new List<(byte, int)>();

            (int x, int y) dpos = (0, 0);
            var dvel = damage.Velocity;
            var pos = new Vector2(dpos.x, dpos.y);
            double energy = damage.Energy; // joules; decrements via Beer-Lambert absorption at each pixel
            double startingEnergy = damage.Energy;

            //We need to figure out where the incoming damage intersects with the ship's damage profile "image"
            var pwidth = damageProfile.DamageProfile.Width;
            var pwIndex = pwidth - 1;
            var hw = pwidth * 0.5;
            var phight = damageProfile.DamageProfile.Height;
            var phIndex = phight - 1;
            var hh = phight * 0.5;

            Vector2 start = new Vector2(damage.Position.x - hw, damage.Position.y - hh);
            var end = new Vector2((pwidth * 0.5)-1, (phight * 0.5)-1);
            var tl = new Vector2(0, 0);
            var tr = new Vector2(pwIndex, 0);
            var bl = new Vector2(0, phIndex);
            var br = new Vector2(pwIndex, phIndex);

            Vector2 intersection;
            if (GeneralMath.LineIntersectsLine(start, end, tl, bl, out intersection)) { }
            else if (GeneralMath.LineIntersectsLine(start, end, tr, br, out intersection)) { }
            else if (GeneralMath.LineIntersectsLine(start, end, tl, tr, out intersection)) { }
            else if (GeneralMath.LineIntersectsLine(start, end, bl, br, out intersection)) { }

            dpos.x = Convert.ToInt32(intersection.X);
            dpos.y = Convert.ToInt32(intersection.Y);

            byte[] byteArray = new byte[shipDamageProfile.ByteArray.Length];
            Buffer.BlockCopy(shipDamageProfile.ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
            RawBmp firstFrame = new RawBmp()
            {
                ByteArray = byteArray,
                Height = shipDamageProfile.Height,
                Width = shipDamageProfile.Width,
                Depth = shipDamageProfile.Depth,
                Stride = shipDamageProfile.Stride
            };
            damageFrames.Add(firstFrame);
            (byte r, byte g, byte b, byte a) savedpx = shipDamageProfile.GetPixel(dpos.x, dpos.y);
            (int x, int y) savedpxloc = dpos;

            // Loop terminates when < 0.1% of original energy remains, or beam exits the bitmap.
            while (
                energy > startingEnergy * 0.001 &&
                dpos.x >= 0 &&
                dpos.x <= shipDamageProfile.Width &&
                dpos.y >= 0 && dpos.y <= shipDamageProfile.Height)
            {
                byteArray = new byte[shipDamageProfile.ByteArray.Length];
                RawBmp lastFrame = damageFrames.Last();
                Buffer.BlockCopy(lastFrame.ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
                var thisFrame = new RawBmp()
                {
                    ByteArray = byteArray,
                    Height = shipDamageProfile.Height,
                    Width = shipDamageProfile.Width,
                    Depth = shipDamageProfile.Depth,
                    Stride = shipDamageProfile.Stride
                };

                (byte r, byte g, byte b, byte a) px = thisFrame.GetPixel(dpos.x, dpos.y);

                // Only absorb energy and deal damage when hitting an occupied pixel with a known material.
                // Unknown materials are transparent (beam passes through without damage or energy loss).
                if (px.a > 0 && DamageResistsLookupTable.TryGetValue(px.r, out var damageresist))
                {
                    // Beer-Lambert absorption: fraction of remaining beam energy deposited in this pixel layer.
                    float absorption = GetWavelengthAbsorption(damageresist, damage.Wavelength);
                    double energyDeposited = energy * absorption;
                    energy -= energyDeposited;

                    // 1 damage point per 100J deposited. DamageProcessor divides by 1000 before applying to HealthPercent.
                    int damageAmount = Math.Max(1, (int)(energyDeposited * 0.01));
                    damageToComponents.Add((px.g, damageAmount));

                    // Reduce material health visualization (alpha channel)
                    px = (px.r, px.g, px.b, (byte)Math.Max(0, px.a - 1));
                }

                // Beam pixel: alpha shows remaining energy fraction for visuals.
                byte beamAlpha = (byte)Math.Min(255, (int)(energy * 255.0 / startingEnergy));
                thisFrame.SetPixel(dpos.x, dpos.y, byte.MaxValue, byte.MaxValue, byte.MaxValue, beamAlpha);
                thisFrame.SetPixel(savedpxloc.x, savedpxloc.y, savedpx.r, savedpx.g, savedpx.b, savedpx.a);
                damageFrames.Add(thisFrame);
                savedpxloc = dpos;
                savedpx = px;

                double dt = 1 / dvel.Length();
                pos.X += dvel.X * dt;
                pos.Y += dvel.Y * dt;
                dpos.x = Convert.ToInt32(pos.X);
                dpos.y = Convert.ToInt32(pos.Y);
            }

            Buffer.BlockCopy(damageFrames.Last().ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
            var finalFrame = new RawBmp()
            {
                ByteArray = byteArray,
                Height = shipDamageProfile.Height,
                Width = shipDamageProfile.Width,
                Depth = shipDamageProfile.Depth,
                Stride = shipDamageProfile.Stride
            };
            finalFrame.SetPixel(savedpxloc.x, savedpxloc.y, savedpx.r, savedpx.g, savedpx.b, savedpx.a);

            damageProfile.DamageEvents.Add(damage);
            damageProfile.DamageProfile = finalFrame;
            return (damageToComponents, damageFrames);
        }
        public static (List<(byte id, int damageAmount)> damageToComponents, List<RawBmp> damageFrames) DealDamageSim(EntityDamageProfileDB damageProfile, DamageFragment damage)
        {
            RawBmp shipDamageProfile = damageProfile.DamageProfile;

            List<RawBmp> damageFrames = new List<RawBmp>();
            List<(byte id, int damageAmount)> damageToComponents = new List<(byte, int)>();

            var fragmentMass = damage.Mass;
            (int x, int y) dpos = (0, 0);
            var dvel = damage.Velocity;
            var dden = damage.Density;
            var dlen = damage.Length;
            var pos = new Vector2(dpos.x, dpos.y);
            var pixelscale = 0.01;
            double startMomentum = damage.Momentum;
            double momentum = startMomentum;

            //We need to figure out where the incoming damage intersects with the ship's damage profile "image"
            var pwidth = damageProfile.DamageProfile.Width;
            var pwIndex = pwidth - 1;//zero based arrays
            var hw = pwidth * 0.5;
            var phight = damageProfile.DamageProfile.Height;
            var phIndex = phight - 1;//zero based arrays
            var hh = phight * 0.5;
            var len = Math.Sqrt((pwidth * pwidth) + (phight * phight));

            //damage.RelativePosition ralitive to our targets center, but we need to translate for calculating 0,0 at top left
            Vector2 start = new Vector2(damage.Position.x - hw, damage.Position.y - hh);
            var end = new Vector2((pwidth * 0.5)-1, (phight * 0.5)-1); //center of our target
            var tl = new Vector2(0, 0);
            var tr = new Vector2(pwIndex, 0);
            var bl = new Vector2(0, phIndex);
            var br = new Vector2(pwIndex, phIndex);

            //pretty sure these can be else ifs.
            Vector2 intersection;

            //left
            if (GeneralMath.LineIntersectsLine(start, end, tl, bl, out intersection))
            {
            }
            //right
            else if (GeneralMath.LineIntersectsLine(start, end, tr, br, out intersection))
            {
            }
            //top
            else if (GeneralMath.LineIntersectsLine(start,end,tl, tr, out intersection))
            {
            }
            //bottom
            else if (GeneralMath.LineIntersectsLine(start,end,bl, br, out intersection))
            {
            }

            dpos.x = Convert.ToInt32(intersection.X);
            dpos.y = Convert.ToInt32(intersection.Y);

            byte[] byteArray = new byte[shipDamageProfile.ByteArray.Length];
            Buffer.BlockCopy(shipDamageProfile.ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
            RawBmp firstFrame = new RawBmp()
            {
                ByteArray = byteArray,
                Height = shipDamageProfile.Height,
                Width = shipDamageProfile.Width,
                Depth = shipDamageProfile.Depth,
                Stride = shipDamageProfile.Stride
            };
            damageFrames.Add(firstFrame);
            (byte r, byte g, byte b, byte a) savedpx = shipDamageProfile.GetPixel(dpos.x, dpos.y);
            (int x, int y) savedpxloc = dpos;

            while (
                momentum > 0 &&
                dpos.x >= 0 &&
                dpos.x <= shipDamageProfile.Width &&
                dpos.y >= 0 && dpos.y <= shipDamageProfile.Height)
            {
                byteArray = new byte[shipDamageProfile.ByteArray.Length];
                RawBmp lastFrame = damageFrames.Last();
                Buffer.BlockCopy(lastFrame.ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
                var thisFrame = new RawBmp()
                {
                    ByteArray = byteArray,
                    Height = shipDamageProfile.Height,
                    Width = shipDamageProfile.Width,
                    Depth = shipDamageProfile.Depth,
                    Stride = shipDamageProfile.Stride
                };

                (byte r, byte g, byte b, byte a) px = thisFrame.GetPixel(dpos.x, dpos.y);
                if (px.a > 0)
                {
                    DamageResistBlueprint damageresist = DamageResistsLookupTable[px.r];

                    double density = damageresist.Density / (px.a / 255f); //density / health
                    double maxImpactDepth = dlen * dden / density;
                    double depthPercent = pixelscale / maxImpactDepth;
                    dlen -= (float)(damage.Length * depthPercent);
                    var momentumLoss = startMomentum * depthPercent;
                    momentum -= momentumLoss;
                    if (momentum > 0)
                    {
                        px = ( px.r, px.g, px.b, 0);
                        damageToComponents.Add((px.g, 1));
                    }
                }

                //this is the damage fragment
                thisFrame.SetPixel(dpos.x, dpos.y, byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)momentum);

                //this is the entity being damaged.
                thisFrame.SetPixel(savedpxloc.x, savedpxloc.y, savedpx.r, savedpx.g, savedpx.b, savedpx.a);
                damageFrames.Add(thisFrame);
                savedpxloc = dpos;
                savedpx = px;

                double dt = 1 / dvel.Length();
                pos.X += dvel.X * dt;
                pos.Y += dvel.Y * dt;
                dpos.x = Convert.ToInt32(pos.X);
                dpos.y = Convert.ToInt32(pos.Y);
            }

            Buffer.BlockCopy(damageFrames.Last().ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
            var finalFrame = new RawBmp()
            {
                ByteArray = byteArray,
                Height = shipDamageProfile.Height,
                Width = shipDamageProfile.Width,
                Depth = shipDamageProfile.Depth,
                Stride = shipDamageProfile.Stride
            };
            finalFrame.SetPixel(savedpxloc.x, savedpxloc.y, savedpx.r, savedpx.g, savedpx.b, savedpx.a);
            //damageProfile.DamageSlides.Add(damageFrames);

            damageProfile.DamageEvents.Add(damage);
            damageProfile.DamageProfile = finalFrame;
            return (damageToComponents, damageFrames);
        }
    }
}