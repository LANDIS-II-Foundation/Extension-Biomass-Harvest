// Contributors:
//   James Domingo, Green Code LLC
//   Robert M. Scheller

using Landis.Utilities;
using Landis.SpatialModeling;
using Landis.Library.BiomassCohorts;
using Landis.Library.BiomassHarvest;
using Landis.Library.HarvestManagement;
using Landis.Library.Metadata;
using Landis.Core;

using System.Collections.Generic;
using System.IO;
using System;

using HarvestMgmtLib = Landis.Library.HarvestManagement;

namespace Landis.Extension.BiomassHarvest
{
    public class PlugIn
        : HarvestExtensionMain 
    {
        public static readonly string ExtensionName = "Biomass Harvest";
        
        private IManagementAreaDataset managementAreas;
        private PrescriptionMaps prescriptionMaps;
        private BiomassMaps biomassMaps;
        private string nameTemplate;
        public static MetadataTable<EventsLog> eventLog;
        public static MetadataTable<SummaryLog> summaryLog;
        private static bool running;

        static int[] totalSites;
        static int[] totalDamagedSites;
        static int[,] totalSpeciesCohorts;
        static int[] totalCohortsKilled;
        static int[] totalCohortsDamaged;
        // 2015-09-14 LCB Track prescriptions as they are reported in summary log so we don't duplicate
        static bool[] prescriptionReported;
        static double[,] totalSpeciesBiomass;
        static double[] totalBiomassRemoved;

        private static IParameters parameters;

        private static ICore modelCore;



        //---------------------------------------------------------------------

        public PlugIn()
            : base(ExtensionName)
        {
        }

        //---------------------------------------------------------------------

        public static ICore ModelCore
        {
            get
            {
                return modelCore;
            }
        }

        //---------------------------------------------------------------------
        
        public override void LoadParameters(string dataFile,
                                            ICore mCore)
        {
            modelCore = mCore;

            // Add local event handler for cohorts death due to age-only
            // disturbances.
            // 2015-07-30 LCB: Disconnecting this event handler; Its tasks are performed by the SiteHarvestedEvent
            Cohort.AgeOnlyDeathEvent += CohortKilledByAgeOnlyDisturbance;
            //Cohort.PartialMortality += CohortKilledByAgeOnlyDisturbance;

            HarvestMgmtLib.Main.InitializeLib(modelCore);
            HarvestExtensionMain.SiteHarvestedEvent += SiteHarvested;
            HarvestExtensionMain.RepeatStandHarvestedEvent += RepeatStandHarvested;
            HarvestExtensionMain.RepeatPrescriptionFinishedEvent += RepeatPrescriptionHarvested;
            Landis.Library.BiomassHarvest.Main.InitializeLib(modelCore);

            ParametersParser parser = new ParametersParser(modelCore.Species);

            HarvestMgmtLib.IInputParameters baseParameters = Landis.Data.Load<IInputParameters>(dataFile, parser);
            parameters = baseParameters as IParameters;
            if (parser.RoundedRepeatIntervals.Count > 0)
            {
                ModelCore.UI.WriteLine("NOTE: The following repeat intervals were rounded up to");
                ModelCore.UI.WriteLine("      ensure they were multiples of the harvest timestep:");
                ModelCore.UI.WriteLine("      File: {0}", dataFile);
                foreach (RoundedInterval interval in parser.RoundedRepeatIntervals)
                    ModelCore.UI.WriteLine("      At line {0}, the interval {1} rounded up to {2}",
                                 interval.LineNumber,
                                 interval.Original,
                                 interval.Adjusted);
            }
            if (parser.ParserNotes.Count > 0)
            {
                foreach (List<string> nList in parser.ParserNotes)
                {
                    foreach (string nLine in nList)
                    {
                        PlugIn.ModelCore.UI.WriteLine(nLine);
                    }
                }
            }
        }

        //---------------------------------------------------------------------

        public override void Initialize()
        {
            //event_id = 1;
            HarvestMgmtLib.SiteVars.GetExternalVars();
            MetadataHandler.InitializeMetadata(parameters.Timestep, parameters.PrescriptionMapNames, parameters.EventLog, parameters.SummaryLog);
            SiteVars.Initialize();
            Timestep = parameters.Timestep;
            managementAreas = parameters.ManagementAreas;
            ModelCore.UI.WriteLine("   Reading management-area map {0} ...", parameters.ManagementAreaMap);
            ManagementAreas.ReadMap(parameters.ManagementAreaMap, managementAreas);

            ModelCore.UI.WriteLine("   Reading stand map {0} ...", parameters.StandMap);
            Stands.ReadMap(parameters.StandMap);

            //finish initializing SiteVars
            HarvestMgmtLib.SiteVars.GetExternalVars();

            foreach (ManagementArea mgmtArea in managementAreas)
                mgmtArea.FinishInitialization();

            prescriptionMaps = new PrescriptionMaps(parameters.PrescriptionMapNames);
            nameTemplate = parameters.PrescriptionMapNames;

            if (parameters.BiomassMapNames != null)
                biomassMaps = new BiomassMaps(parameters.BiomassMapNames);


        }

        //---------------------------------------------------------------------

        public override void Run()
        {
            running = true;

            HarvestMgmtLib.SiteVars.Prescription.ActiveSiteValues = null;
            SiteVars.BiomassRemoved.ActiveSiteValues = 0;
            Landis.Library.BiomassHarvest.SiteVars.CohortsPartiallyDamaged.ActiveSiteValues = 0;
            HarvestMgmtLib.SiteVars.CohortsDamaged.ActiveSiteValues = 0;
            SiteVars.BiomassBySpecies.ActiveSiteValues = null; 

            SiteBiomass.EnableRecordingForHarvest();

            //harvest each management area in the list
            foreach (ManagementArea mgmtArea in managementAreas) {

                totalSites          = new int[Prescription.Count];
                totalDamagedSites   = new int[Prescription.Count];
                totalSpeciesCohorts = new int[Prescription.Count, modelCore.Species.Count];
                totalCohortsDamaged = new int[Prescription.Count];
                totalCohortsKilled  = new int[Prescription.Count];
                // 2015-09-14 LCB Track prescriptions as they are reported in summary log so we don't duplicate
                prescriptionReported = new bool[Prescription.Count];
                totalSpeciesBiomass = new double[Prescription.Count, modelCore.Species.Count];
                totalBiomassRemoved = new double[Prescription.Count];

                mgmtArea.HarvestStands();
                //and record each stand that's been harvested in a non-repeat step

                foreach (Stand stand in mgmtArea) {
                    //ModelCore.UI.WriteLine("   List of stands {0} ...", stand.MapCode);
                    if (stand.Harvested && !stand.RepeatHarvested)
                        WriteLogEntry(mgmtArea, stand);

                    // Do not double up on recording repeat harvested stands. This is now done from the harvest code
                    else if (stand.RepeatHarvested)
                        stand.SetRepeatHarvested();
                }

                // Prevent establishment:
                foreach (Stand stand in mgmtArea) {

                    if (stand.Harvested && stand.LastPrescription.PreventEstablishment) {

                        List<ActiveSite> sitesToDelete = new List<ActiveSite>();

                        foreach (ActiveSite site in stand)
                        {
                            if (Landis.Library.BiomassHarvest.SiteVars.CohortsPartiallyDamaged[site] > 0 || HarvestMgmtLib.SiteVars.CohortsDamaged[site] > 0)
                            {
                                Landis.Library.Succession.Reproduction.PreventEstablishment(site);
                                sitesToDelete.Add(site);
                            }

                        }

                        foreach (ActiveSite site in sitesToDelete) {
                            stand.DelistActiveSite(site);
                        }
                    }

                }

                foreach (AppliedPrescription aprescription in mgmtArea.Prescriptions)
                {
                    if (modelCore.CurrentTime <= aprescription.EndTime)
                        WriteSummaryLogEntry(mgmtArea, aprescription);
                }
            }

            WritePrescriptionMap(modelCore.CurrentTime);
            if (biomassMaps != null)
                biomassMaps.WriteMap(modelCore.CurrentTime);

            running = false;
            
            SiteBiomass.DisableRecordingForHarvest();
        }

        //---------------------------------------------------------------------

        // Event handler when a site has been harvested.
        public static void SiteHarvested(object                  sender,
                                         SiteHarvestedEvent.Args eventArgs)
        {
            ActiveSite site = eventArgs.Site;
            IDictionary<ISpecies, int> biomassBySpecies = new Dictionary<ISpecies, int>();
            foreach (ISpecies species in ModelCore.Species)
            {
                int speciesBiomassHarvested = SiteBiomass.Harvested[species];
                SiteVars.BiomassRemoved[site] += speciesBiomassHarvested;
                biomassBySpecies.Add(species, speciesBiomassHarvested);
            }
            SiteVars.BiomassBySpecies[site] = biomassBySpecies;
            SiteBiomass.ResetHarvestTotals();
        }

        // Event handler when a stand has been harvested in a repeat step
        public static void RepeatStandHarvested(object sender,
                                         RepeatHarvestStandHarvestedEvent.Args eventArgs)
        {
            WriteLogEntry(eventArgs.MgmtArea, eventArgs.Stand, eventArgs.RepeatNumber);
        }

        // Event handler when a prescription has finished a repeat event
        public static void RepeatPrescriptionHarvested(object sender,
                                         RepeatHarvestPrescriptionFinishedEvent.Args eventArgs)
        {
            WriteSummaryLogEntry(eventArgs.MgmtArea, eventArgs.Prescription, eventArgs.RepeatNumber,
                eventArgs.LastHarvest);
        }

        //---------------------------------------------------------------------

        // Event handler when a cohort is killed by an age-only disturbance.
        public static void CohortKilledByAgeOnlyDisturbance(object sender, DeathEventArgs eventArgs)
        {

        //    // If this plug-in is not running, then some base disturbance
        //    // plug-in killed the cohort.
            if (!running)
                return;

        //    // If this plug-in is running, then the age-only disturbance must
        //    // be a cohort-selector from Base Harvest.

            //ModelCore.UI.WriteLine("Cohort Biomass removed={0:0.0}; Total Killed={1:0.0}.", reduction, SiteVars.BiomassRemoved[eventArgs.Site]);
            //Landis.Library.BiomassHarvest.SiteVars.CohortsPartiallyDamaged[eventArgs.Site]++;
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Writes an output map of prescriptions that harvested each active site.
        /// </summary>
        private void WritePrescriptionMap(int timestep)
        {
            string path = MapNames.ReplaceTemplateVars(nameTemplate, timestep);
            ModelCore.UI.WriteLine("   Writing prescription map to {0} ...", path);
            using (IOutputRaster<ShortPixel> outputRaster = modelCore.CreateRaster<ShortPixel>(path, modelCore.Landscape.Dimensions))
            {
                ShortPixel pixel = outputRaster.BufferPixel;
                foreach (Site site in modelCore.Landscape.AllSites)
                {
                    if (site.IsActive) {
                        Prescription prescription = HarvestMgmtLib.SiteVars.Prescription[site];
                        if (prescription == null)
                            pixel.MapCode.Value = 1;
                        else
                            pixel.MapCode.Value = (short) (prescription.Number + 1);
                    }
                    else {
                        //  Inactive site
                        pixel.MapCode.Value = 0;
                    }
                    outputRaster.WriteBufferPixel();
                }
            }
        }

        //---------------------------------------------------------------------

        public static void WriteLogEntry(ManagementArea mgmtArea, Stand stand, uint repeatNumber = 0)
        {
            int damagedSites = 0;
            int cohortsDamaged = 0;
            int cohortsKilled = 0;
            int standPrescriptionNumber = 0;
            double biomassRemoved = 0.0;
            double biomassRemovedPerHa = 0.0;
            IDictionary<ISpecies, double> totalBiomassBySpecies = new Dictionary<ISpecies, double>();

            //ModelCore.UI.WriteLine("BiomassHarvest:  PlugIn.cs: WriteLogEntry: mgmtArea {0}, Stand {1} ", mgmtArea.Prescriptions.Count, stand.MapCode);

            foreach (ActiveSite site in stand) {
                //set the prescription name for this site
                if (HarvestMgmtLib.SiteVars.Prescription[site] != null)
                {
                    standPrescriptionNumber = HarvestMgmtLib.SiteVars.Prescription[site].Number;
                    HarvestMgmtLib.SiteVars.PrescriptionName[site] = HarvestMgmtLib.SiteVars.Prescription[site].Name;
                    HarvestMgmtLib.SiteVars.TimeOfLastEvent[site] = modelCore.CurrentTime;
                }

                cohortsDamaged += Landis.Library.BiomassHarvest.SiteVars.CohortsPartiallyDamaged[site];
                cohortsKilled += (HarvestMgmtLib.SiteVars.CohortsDamaged[site] - Landis.Library.BiomassHarvest.SiteVars.CohortsPartiallyDamaged[site]);


                if (Landis.Library.BiomassHarvest.SiteVars.CohortsPartiallyDamaged[site] > 0 || HarvestMgmtLib.SiteVars.CohortsDamaged[site] > 0)
                {
                    damagedSites++;

                    //Conversion from [g m-2] to [Mg ha-1] to [Mg]
                    biomassRemoved += SiteVars.BiomassRemoved[site] / 100.0 * modelCore.CellArea;
                    IDictionary<ISpecies, int> siteBiomassBySpecies = SiteVars.BiomassBySpecies[site];
                    if (siteBiomassBySpecies != null)
                    {
                        // Sum up total biomass for each species
                        foreach (ISpecies species in modelCore.Species)
                        {
                            int addValue = 0;
                            siteBiomassBySpecies.TryGetValue(species, out addValue);
                            double oldValue;
                            if (totalBiomassBySpecies.TryGetValue(species, out oldValue))
                            {
                                totalBiomassBySpecies[species] += addValue / 100.0 * modelCore.CellArea;
                            }
                            else
                            {
                                totalBiomassBySpecies.Add(species, addValue / 100.0 * modelCore.CellArea);
                            }
                        }
                    }
                }
            }

            totalSites[standPrescriptionNumber] += stand.SiteCount;
            totalDamagedSites[standPrescriptionNumber] += damagedSites;
            totalCohortsDamaged[standPrescriptionNumber] += cohortsDamaged;
            totalCohortsKilled[standPrescriptionNumber] += cohortsKilled;
            totalBiomassRemoved[standPrescriptionNumber] += biomassRemoved;

            double[] species_cohorts = new double[modelCore.Species.Count];
            double[] species_biomass = new double[modelCore.Species.Count];

            double biomass_value;
            foreach (ISpecies species in modelCore.Species) {
                int cohortCount = stand.DamageTable[species];
                species_cohorts[species.Index] = cohortCount;
                totalSpeciesCohorts[standPrescriptionNumber, species.Index] += cohortCount;
                totalBiomassBySpecies.TryGetValue(species, out biomass_value);
                species_biomass[species.Index] = biomass_value;
                totalSpeciesBiomass[standPrescriptionNumber, species.Index] += biomass_value;
            }


            //now that the damage table for this stand has been recorded, clear it!!
            stand.ClearDamageTable();

            //write to log file:
            if (biomassRemoved > 0.0)
                biomassRemovedPerHa = biomassRemoved / (double) damagedSites / modelCore.CellArea;

            string name = stand.PrescriptionName;

            if (repeatNumber != 0)
            {
                name = name + "(" + repeatNumber + ")";
            }
            eventLog.Clear();
            EventsLog el = new EventsLog();
            el.Time = modelCore.CurrentTime;
            el.ManagementArea = mgmtArea.MapCode;
            el.Prescription = name;
            el.Stand = stand.MapCode;
            el.EventID = stand.EventId;
            el.StandAge = stand.Age;
            el.StandRank = stand.HarvestedRank;
            el.NumberOfSites = stand.SiteCount;
            el.HarvestedSites = damagedSites;
            el.MgBiomassRemoved = biomassRemoved;
            el.MgBioRemovedPerDamagedHa = biomassRemovedPerHa;
            el.TotalCohortsPartialHarvest = cohortsDamaged;
            el.TotalCohortsCompleteHarvest = cohortsKilled;
            el.CohortsHarvested_ = species_cohorts;
            el.BiomassHarvestedMg_ = species_biomass;

            eventLog.AddObject(el);
            eventLog.WriteToFile();
        }

        public static void WriteSummaryLogEntry(ManagementArea mgmtArea, AppliedPrescription prescription, uint repeatNumber = 0, bool lastHarvest = false)
        {
            double[] species_cohorts = new double[modelCore.Species.Count];
            double[] species_biomass = new double[modelCore.Species.Count];
            foreach (ISpecies species in modelCore.Species)
            {
                species_cohorts[species.Index] = totalSpeciesCohorts[prescription.Prescription.Number, species.Index];
                species_biomass[species.Index] = totalSpeciesBiomass[prescription.Prescription.Number, species.Index];
            }

            if (totalSites[prescription.Prescription.Number] > 0 && prescriptionReported[prescription.Prescription.Number] != true)
            {
                string name = prescription.Prescription.Name;

                if (repeatNumber > 0)
                {
                    name = name + "(" + repeatNumber + ")";
                }
                summaryLog.Clear();
                SummaryLog sl = new SummaryLog();
                sl.Time = modelCore.CurrentTime;
                sl.ManagementArea = mgmtArea.MapCode;
                sl.Prescription = name;
                sl.HarvestedSites = totalDamagedSites[prescription.Prescription.Number];
                sl.TotalBiomassHarvested = totalBiomassRemoved[prescription.Prescription.Number];
                sl.TotalCohortsPartialHarvest = totalCohortsDamaged[prescription.Prescription.Number];
                sl.TotalCohortsCompleteHarvest = totalCohortsKilled[prescription.Prescription.Number];
                sl.CohortsHarvested_ = species_cohorts;
                sl.BiomassHarvestedMg_ = species_biomass;
                summaryLog.AddObject(sl);
                summaryLog.WriteToFile();

                // Do not mark this as recorded until the final summary is logged. Because repeat steps will be
                // recorded first and then new initiations, mark this as reported once the initiation step is complete
                if (repeatNumber == 0 || (ModelCore.CurrentTime > prescription.EndTime && lastHarvest))
                {
                    prescriptionReported[prescription.Prescription.Number] = true;
                }

                // Clear the log for the initial harvests
                if (lastHarvest)
                {
                    totalDamagedSites[prescription.Prescription.Number] = 0;
                    totalBiomassRemoved[prescription.Prescription.Number] = 0;
                    totalCohortsDamaged[prescription.Prescription.Number] = 0;
                    totalCohortsKilled[prescription.Prescription.Number] = 0;

                    foreach (ISpecies species in modelCore.Species)
                    {
                        totalSpeciesCohorts[prescription.Prescription.Number, species.Index] = 0;
                        totalSpeciesBiomass[prescription.Prescription.Number, species.Index] = 0;
                    }
                }
            }
        }
    }
}
