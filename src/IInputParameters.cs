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

        /// <summary>
        /// If set to true, BHE should expose site variable "ManagementArea" for all sites which are included
        /// into a management area.
        /// </summary>
        bool ExposeManagementAreasForSites
        {
            get;
        }
    }
}
