using System;
using System.Collections.Generic;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Stations
{
    /// <summary>The computed result of assembling a station from a chassis + modules — the emergent totals plus whether
    /// the assembly is legal (the modules fit the chassis's STRUCTURE BUDGET). The station echo of a ground unit's
    /// <see cref="Pulsar4X.GroundCombat.GroundUnitAssemblyResult"/> / a ship's derived stats.</summary>
    public class StationAssemblyResult
    {
        /// <summary>The chassis's structural budget — how much module the frame can bear, measured in VOLUME
        /// (the internal room a station has). The "chassis gives the budget" half of the universal rule.</summary>
        public double StructuralBudget;
        /// <summary>Σ mounted-module volume — the structure the modules CONSUME. Over-budget → invalid.</summary>
        public double UsedStructure;
        /// <summary>How many modules are mounted (Σ counts, excluding the chassis).</summary>
        public int ModuleCount;
        /// <summary>Total build mass (chassis + modules) — feeds the build cost + transport size.</summary>
        public double BuildMass;
        /// <summary>Σ crew the assembled station needs to run (chassis + modules) — informational.</summary>
        public int CrewRequired;
        public bool Valid;
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// THE STATION ASSEMBLER — turns a chassis + a list of modules into a station's totals, the same way
    /// <c>GroundUnitAssembly.Compute</c> turns a frame + parts into a ground unit and <c>ShipDesign.Recalculate</c>
    /// turns components into a ship. It computes what the Entity Assembler shows live for a station design and gates
    /// the ONE station-specific rule: **the modules must fit the chassis's STRUCTURE BUDGET** — the "chassis gives the
    /// budget, everything else consumes it" rule (the developer's call) made mechanical.
    ///
    ///   • capacity = the chassis's <see cref="StationChassisAtb.StructuralBudget"/> (a design-time dial on the frame).
    ///   • used     = Σ each module's <see cref="ComponentDesign.VolumePerUnit"/> × count (how much internal room it
    ///     takes). Volume is the natural unit — a station has finite internal room; a reactor / research lab / factory
    ///     takes up space. Raise the chassis's Structural Budget dial to fit more or bigger modules.
    ///
    /// Pure + defensive (a bad/empty assembly comes back Invalid with reasons, never throws). Registration (turning the
    /// assembly into a buildable faction design) lives on <see cref="StationDesign.RegisterStationDesign"/> — this class
    /// is only the live READOUT + validity, exactly the compute half of <c>GroundUnitAssembly</c>.
    /// Design: docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md.
    /// </summary>
    public static class StationAssembly
    {
        /// <summary>Compute a station's totals + structure-budget validity from a <paramref name="chassis"/> (must carry a
        /// <see cref="StationChassisAtb"/>) and its mounted <paramref name="modules"/> (reactor / research / factory /
        /// refinery / mine, with a count each — NOT the chassis). Never throws.</summary>
        public static StationAssemblyResult Compute(ComponentDesign chassis, IEnumerable<(ComponentDesign design, int count)> modules)
        {
            var r = new StationAssemblyResult();
            if (chassis == null || !chassis.HasAttribute<StationChassisAtb>())
            {
                r.Problems.Add("no chassis — a station needs exactly one station chassis (frame)");
                return r;
            }

            var atb = chassis.GetAttribute<StationChassisAtb>();
            r.StructuralBudget = atb.StructuralBudget;
            r.BuildMass = chassis.MassPerUnit;
            r.CrewRequired = chassis.CrewReq;

            double used = 0;
            if (modules != null)
            {
                foreach (var (d, c) in modules)
                {
                    if (d == null || c <= 0 || ReferenceEquals(d, chassis)) continue;
                    used += d.VolumePerUnit * c;
                    r.BuildMass += (double)d.MassPerUnit * c;
                    r.CrewRequired += d.CrewReq * c;
                    r.ModuleCount += c;
                }
            }
            r.UsedStructure = used;

            r.Valid = used <= r.StructuralBudget;
            if (!r.Valid)
                r.Problems.Add("over structure budget: " + used.ToString("0") + " / " + r.StructuralBudget.ToString("0")
                    + " — raise the chassis Structural Budget or drop a module");
            return r;
        }
    }
}
