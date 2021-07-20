// Contributors:
//   James Domingo, Green Code LLC
//   Robert M. Scheller
 

namespace Landis.Extension.BiomassHarvest
{
    /// <summary>
    /// The parameters for biomass harvest.
    /// </summary>
    public interface IParameters
        : Landis.Library.HarvestManagement.IInputParameters
    {
        /// <summary>
        /// Template for pathnames for biomass-removed maps.
        /// </summary>
        string BiomassMapNames
        {
            get;
        }
    }
}
