// Contributors:
//   James Domingo, Green Code LLC
//   Robert M. Scheller


using Landis.SpatialModeling;
using Landis.Library.BiomassCohorts;
using Landis.Library.Biomass;
using Landis.Library.HarvestManagement;
using Landis.Core;
using System.Collections.Generic;

namespace Landis.Extension.BiomassHarvest
{
    public class SiteVars
    {
        private static ISiteVar<double> biomassRemoved;
        private static ISiteVar<double> capacityReduction;
        private static ISiteVar<Pool> woodyDebris;
        private static ISiteVar<Pool> litter;

        private static ISiteVar<Landis.Library.BiomassCohorts.ISiteCohorts> cohorts;
        private static ISiteVar<IDictionary<ISpecies, int>> biomassBySpecies;

        private static ISiteVar<ManagementArea> managementArea;

        //---------------------------------------------------------------------

        public static void Initialize(IParameters parameters)
        {
            woodyDebris = PlugIn.ModelCore.GetSiteVar<Pool>("Succession.WoodyDebris");
            litter = PlugIn.ModelCore.GetSiteVar<Pool>("Succession.Litter");
            cohorts = PlugIn.ModelCore.GetSiteVar<Landis.Library.BiomassCohorts.ISiteCohorts>("Succession.BiomassCohorts");

            biomassRemoved = PlugIn.ModelCore.Landscape.NewSiteVar<double>();
            Landis.Library.BiomassHarvest.SiteVars.CohortsPartiallyDamaged = PlugIn.ModelCore.Landscape.NewSiteVar<int>();
            capacityReduction = PlugIn.ModelCore.Landscape.NewSiteVar<double>();
            biomassBySpecies = PlugIn.ModelCore.Landscape.NewSiteVar<IDictionary<ISpecies, int>>();

            SiteVars.CapacityReduction.ActiveSiteValues = 0.0;

            PlugIn.ModelCore.RegisterSiteVar(SiteVars.CapacityReduction, "Harvest.CapacityReduction");

            managementArea = PlugIn.ModelCore.Landscape.NewSiteVar<ManagementArea>();
            PlugIn.ModelCore.RegisterSiteVar(SiteVars.ManagementArea, "BiomassHarvest.ManagementArea");

            if (cohorts == null)
            {
                string mesg = string.Format("Cohorts are empty.  Please double-check that this extension is compatible with your chosen succession extension.");
                throw new System.ApplicationException(mesg);
            }
        }
        //---------------------------------------------------------------------
        public static ushort GetMaxAge(ActiveSite site)
        {
            int maxAge = 0;
            foreach (ISpeciesCohorts sppCo in (Landis.Library.BiomassCohorts.ISpeciesCohorts) SiteVars.Cohorts[site])
                foreach (ICohort cohort in sppCo)
                    if (cohort.Age > maxAge)
                        maxAge = cohort.Age;

            return (ushort) maxAge;

        }

        //---------------------------------------------------------------------

        public static ISiteVar<double> BiomassRemoved
        {
            get {
                return biomassRemoved;
            }
        }
        //---------------------------------------------------------------------

        public static ISiteVar<double> CapacityReduction
        {
            get {
                return capacityReduction;
            }
        }

        //---------------------------------------------------------------------
        public static ISiteVar<ISiteCohorts> Cohorts
        {
            get
            {
                return cohorts;
            }
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// The intact dead woody pools for the landscape's sites.
        /// </summary>
        public static ISiteVar<Pool> WoodyDebris
        {
            get
            {
                return woodyDebris;
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// The dead non-woody pools for the landscape's sites.
        /// </summary>
        public static ISiteVar<Pool> Litter
        {
            get
            {
                return litter;
            }
        }

        /// <summary>
        /// The amount of biomass harvested by a site for each species
        /// </summary>
        public static ISiteVar<IDictionary<ISpecies, int>> BiomassBySpecies
        {
            get
            {
                return biomassBySpecies;
            }
        }

        /// <summary>
        /// Management area which a site belongs to.
        /// </summary>
        public static ISiteVar<ManagementArea> ManagementArea
        {
            get
            {
                return managementArea;
            }
        }
    }
}
