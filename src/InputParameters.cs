// Contributors:
//   James Domingo, Green Code LLC
//   Robert M. Scheller

 
using Landis.Library.HarvestManagement;

namespace Landis.Extension.BiomassHarvest
{
    /// <summary>
    /// The parameters for biomass harvest.
    /// </summary>
    public class Parameters
        : InputParameters, IParameters
    {
        private string biomassMapNamesTemplate;
 
        //---------------------------------------------------------------------
        /// <summary>
        /// Template for pathnames for biomass-removed maps.
        /// </summary>
        public string BiomassMapNames
        {
            get {
                return biomassMapNamesTemplate;
            }
            set {
                if (value != null) {
                    // Since this template for biomass-reduced map names
                    // recognized just one template variable ("{timestep}")
                    // just like the template for prescription map names,
                    // we can use the MapNames class for validation.
                    // TO DO: update documentation for MapNames class.
                    MapNames.CheckTemplateVars(value);
                }
                biomassMapNamesTemplate = value;
            }
        }

        public Parameters()
        {
        }

    }
}
