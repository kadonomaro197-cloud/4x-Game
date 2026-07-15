using System.Collections.Generic;
using Pulsar4X.Components;

namespace Pulsar4X.Colonies
{
    /// <summary>The computed result of assembling a building from a foundation + modules — the totals plus whether the
    /// assembly fits the foundation's FOOTPRINT budget. The on-world echo of a station's
    /// <see cref="Pulsar4X.Stations.StationAssemblyResult"/>.</summary>
    public class BuildingAssemblyResult
    {
        /// <summary>The foundation's footprint budget — how much module it can host, measured in VOLUME (the room the
        /// building takes). The "chassis gives the budget" half of the universal rule.</summary>
        public double FootprintBudget;
        /// <summary>Σ mounted-module volume — the footprint the modules CONSUME. Over-budget → invalid.</summary>
        public double UsedFootprint;
        public int ModuleCount;
        public double BuildMass;
        public int CrewRequired;
        public bool Valid;
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// THE BUILDING ASSEMBLER — turns a foundation + modules into a building's totals, mirroring
    /// <c>StationAssembly.Compute</c> exactly (foundation supplies the FOOTPRINT budget; each module consumes its
    /// volume). The "chassis gives the budget, everything else consumes it" rule for a planet-side building. Pure +
    /// defensive (a bad/empty assembly comes back Invalid with reasons, never throws). Registration lives on
    /// <see cref="BuildingDesign.RegisterBuildingDesign"/> — this is only the live READOUT + validity.
    /// Design: docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md.
    /// </summary>
    public static class BuildingAssembly
    {
        /// <summary>Compute a building's totals + footprint-budget validity from a <paramref name="foundation"/> (must
        /// carry a <see cref="BuildingChassisAtb"/>) and its mounted <paramref name="modules"/>. Never throws.</summary>
        public static BuildingAssemblyResult Compute(ComponentDesign foundation, IEnumerable<(ComponentDesign design, int count)> modules)
        {
            var r = new BuildingAssemblyResult();
            if (foundation == null || !foundation.HasAttribute<BuildingChassisAtb>())
            {
                r.Problems.Add("no foundation — a building needs exactly one building foundation");
                return r;
            }

            var atb = foundation.GetAttribute<BuildingChassisAtb>();
            r.FootprintBudget = atb.StructuralBudget;
            r.BuildMass = foundation.MassPerUnit;
            r.CrewRequired = foundation.CrewReq;

            double used = 0;
            if (modules != null)
            {
                foreach (var (d, c) in modules)
                {
                    if (d == null || c <= 0 || ReferenceEquals(d, foundation)) continue;
                    used += d.VolumePerUnit * c;
                    r.BuildMass += (double)d.MassPerUnit * c;
                    r.CrewRequired += d.CrewReq * c;
                    r.ModuleCount += c;
                }
            }
            r.UsedFootprint = used;

            r.Valid = used <= r.FootprintBudget;
            if (!r.Valid)
                r.Problems.Add("over footprint budget: " + used.ToString("0") + " / " + r.FootprintBudget.ToString("0")
                    + " — raise the foundation Footprint Budget or drop a module");
            return r;
        }
    }
}
