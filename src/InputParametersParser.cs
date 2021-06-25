// Contributors:
//   James Domingo, Green Code LLC
//   Robert M. Scheller
 

using Landis.Utilities;
using Landis.Core;
using Landis.Library.BiomassHarvest;
using Landis.Library.HarvestManagement;
using Landis.Library.SiteHarvest;
using System.Collections.Generic;

namespace Landis.Extension.BiomassHarvest
{
    /// <summary>
    /// A parser that reads the extension's parameters from text input.
    /// </summary>
    public class ParametersParser
        : InputParametersParser
    {
        public override string LandisDataValue
        {
            get
            {
                return PlugIn.ExtensionName;
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="speciesDataset">
        /// The dataset of species to look up species' names in.
        /// </param>
        public ParametersParser(ISpeciesDataset speciesDataset)
            : base(PlugIn.ExtensionName, speciesDataset)
        {
        }

        //---------------------------------------------------------------------

        protected override Landis.Library.HarvestManagement.InputParameters CreateEmptyParameters()
        {
            return new Parameters();
        }

        //---------------------------------------------------------------------

        protected override ICohortCutter CreateCohortCutter(ICohortSelector cohortSelector)
        {
            return CohortCutterFactory.CreateCutter(cohortSelector, HarvestExtensionMain.ExtType);
        }

        protected override ICohortCutter CreateAdditionalCohortCutter(ICohortSelector cohortSelector)
        {
            return CohortCutterFactory.CreateAdditionalCutter(cohortSelector, HarvestExtensionMain.ExtType);
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Creates a cohort selection method for a specific set of ages and
        /// age ranges.
        /// </summary>
        /// <remarks>
        /// This overrides the base method so it can use the PartialThinning
        /// class to handle cohort selections with percentages.
        /// </remarks>
        protected override void CreateCohortSelectionMethodFor(ISpecies species,
                                                               IList<ushort> ages,
                                                               IList<AgeRange> ranges)
        {
            if (!PartialThinning.CreateCohortSelectorFor(species, ages, ranges))
            {
                // There were no percentages specified for this species' ages
                // and ranges.  So just create and store a whole cohort
                // selector using the base method.
                base.CreateCohortSelectionMethodFor(species, ages, ranges);
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Creates a cohort selection method for a specific set of ages and
        /// age ranges.
        /// </summary>
        /// <remarks>
        /// This overrides the base method so it can use the PartialThinning
        /// class to handle cohort selections with percentages.
        /// </remarks>
        protected override void CreateAdditionalCohortSelectionMethodFor(ISpecies species,
                                                               IList<ushort> ages,
                                                               IList<AgeRange> ranges)
        {
            if (!PartialThinning.CreateAdditionalCohortSelectorFor(species, ages, ranges))
            {
                // There were no percentages specified for this species' ages
                // and ranges.  So just create and store a whole cohort
                // selector using the base method.
                base.CreateAdditionalCohortSelectionMethodFor(species, ages, ranges);
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Puts filename for BiomassMaps into the parameters
        /// </summary>
        /// <remarks>
        /// This overrides the base method in harvest-mgmt-lib
        /// </remarks>
        protected override void ReadBiomassMaps(InputParameters baseParameters)
        {
            // Get the biomass harvest implementation of the InputParameters so we can set the biomassMaps property 
            Parameters parameters = baseParameters as Parameters;

            InputVar<string> biomassMapNames = new InputVar<string>("BiomassMaps");
            if (ReadOptionalVar(biomassMapNames))
            {
                parameters.BiomassMapNames = biomassMapNames.Value;
            }

            InputVar<bool> exposeManagementAreasForSites = new InputVar<bool>("ExposeManagementAreasForSites");
            if (ReadOptionalVar(exposeManagementAreasForSites))
            {
                parameters.ExposeManagementAreasForSites = exposeManagementAreasForSites.Value;
            }
        }
    }
}
