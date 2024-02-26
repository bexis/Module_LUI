using BExIS.Utils.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class LUIComponentsCalculation
    {
        public DataTable landuseData;
        public DataTable landuseFullData;
        DataTable plotTypes;
        public List<string> warnings;
        public LUIComponentsCalculation(DataTable data, DataTable fullData, DataTable plotTypesData)
        {
            landuseData = data;
            landuseFullData = fullData;
            plotTypes = plotTypesData;
            warnings = new List<string>();

            // importnant step, where some NAs are replaced by values
            DataCorrections();
        }

        public DataTable CalculateComponents()
        {
            //create result datatable
            DataTable luiComponents = new DataTable();
            DataColumn idCol = new DataColumn("Id");
            idCol.DataType = System.Type.GetType("System.Int32");
            luiComponents.Columns.Add(idCol);

            luiComponents.Columns.Add("Year");
            luiComponents.Columns.Add("Exploratory");
            luiComponents.Columns.Add("EP_PlotID");
            luiComponents.Columns.Add("isVIP");
            luiComponents.Columns.Add("isMIP");
            luiComponents.Columns.Add("TotalGrazing");
            luiComponents.Columns.Add("TotalMowing");
            luiComponents.Columns.Add("TotalFertilization");

            #region check TotalMowing

            //do we need this?
            var totalMowing = landuseData.AsEnumerable().Select(r => r.Field<long>("Cuts")).ToList(); ;
            int sumNa = totalMowing.Where(a => a.Equals(-999999)).Count();
            if (sumNa > 0)
            {
                warnings.Add("There is missing data in TotalMowing");
            }

            #endregion


            int idCounter = 0;
            // needed to calculate the mean manure for the 2006 and 2007er values
            List<double> manureListOnePlotAllYears = new List<double>();
            
            foreach (DataRow row in landuseData.Rows)
            {
                idCounter++;
                DataRow dr = luiComponents.NewRow();
                dr["Id"] = idCounter;
                dr["Year"] = row.Field<DateTime>("Year").Year;
                dr["Exploratory"] = row.Field<string>("Exploratory");
                dr["EP_PlotID"] = row.Field<string>("EP_PlotID");
                
                //get MIP/VIP info
                dr["isVIP"] = plotTypes.AsEnumerable().Where(a=>a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID")).Select(e=>e.Field<string>("VIP")).FirstOrDefault();
                dr["isMIP"] = plotTypes.AsEnumerable().Where(a => a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID")).Select(e => e.Field<string>("MIP")).FirstOrDefault(); ;

                dr["TotalMowing"] = row.Field<long>("Cuts");

                #region TotalGrazing

                //Calculate grazing intensities (grazing1-grazing4)
                var Grazing1 = row.Field<double>("LivestockUnits1") * row.Field<double>("DayGrazing1") / row.Field<double>("GrazingArea1");
                if (double.IsNaN(Grazing1) || Grazing1 == -999999)
                    Grazing1 = 0;
                var Grazing2 = row.Field<double>("LivestockUnits2") * row.Field<double>("DayGrazing2") / row.Field<double>("GrazingArea2");
                if (double.IsNaN(Grazing2) || Grazing2 == -999999)
                    Grazing2 = 0;
                var Grazing3 = row.Field<double>("LivestockUnits3") * row.Field<double>("DayGrazing3") / row.Field<double>("GrazingArea3");
                if (double.IsNaN(Grazing3) || Grazing3 == -999999)
                    Grazing3 = 0;
                var Grazing4 = row.Field<double>("LivestockUnits4") * row.Field<double>("DayGrazing4") / row.Field<double>("GrazingArea4");
                if (double.IsNaN(Grazing4) || Grazing4 == -999999)
                    Grazing4 = 0;

                double TotalGrazing = Grazing1 + Grazing2 + Grazing3 + Grazing4;
                TotalGrazing = System.Math.Round(TotalGrazing, 4);

                dr["TotalGrazing"] = TotalGrazing;

                #endregion

                #region TotalFertilization

                //Calculate organic N from manure, slurry, sludge and mash 

                /*
                 * 
                 * Calculation for 
                 * 
                 * Manure
                 * 
                 */

                // needed to calculate the mean manure for the 2006 and 2007er values
                // list of all manure values for a plot
                if (row.Field<DateTime>("Year").Year == 2006 || row.Field<DateTime>("Year").Year == 2007)
                {
                    
                    // Norg-Exact
                    manureListOnePlotAllYears.AddRange(landuseData.AsEnumerable().Where(a => a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID") &&
                    a.Field<double>("Manure_tha") > 0 && a.Field<string>("ExactValOrg") == "ja" && a.Field<DateTime>("Year").Year < 2019)
                    .Select(b => b.Field<double>("NorgExact")).ToList());

                    // Manure Type: Rind
                    manureListOnePlotAllYears.AddRange(landuseData.AsEnumerable().Where(a => a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID") &&
                    a.Field<double>("Manure_tha") >= 0 && a.Field<string>("ExactValOrg") != "ja" && a.Field<string>("TypeManure") == "Rind" && a.Field<DateTime>("Year").Year < 2019)
                    .Select(b => b.Field<double>("Manure_tha") * 5.6).ToList());

                    // Manure Type: Pferd
                    manureListOnePlotAllYears.AddRange(landuseData.AsEnumerable().Where(a => a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID") &&
                    a.Field<double>("Manure_tha") >= 0 && a.Field<string>("ExactValOrg") != "ja" && a.Field<string>("TypeManure") == "Pferd" && a.Field<DateTime>("Year").Year < 2019)
                    .Select(b => b.Field<double>("Manure_tha") * 4.9).ToList());

                    // Manure Type: Schaf
                    manureListOnePlotAllYears.AddRange(landuseData.AsEnumerable().Where(a => a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID") &&
                    a.Field<double>("Manure_tha") >= 0 && a.Field<string>("ExactValOrg") != "ja" && a.Field<string>("TypeManure") == "Schaf" && a.Field<DateTime>("Year").Year < 2019)
                    .Select(b => b.Field<double>("Manure_tha") * 8.13).ToList());

                    // no manure
                    manureListOnePlotAllYears.AddRange(landuseData.AsEnumerable().Where(a => a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID") &&
                    a.Field<double>("Manure_tha") >= 0 && a.Field<string>("ExactValOrg") != "ja" && a.Field<string>("TypeManure") == "-888888" && a.Field<DateTime>("Year").Year < 2019)
                    .Select(b => b.Field<double>("Manure_tha") * 0).ToList());

                    // no manure
                    manureListOnePlotAllYears.AddRange(landuseData.AsEnumerable().Where(a => a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID") &&
                    a.Field<double>("Manure_tha") == 0 && a.Field<string>("ExactValOrg") == "ja" && a.Field<string>("TypeManure") == "-888888" && a.Field<DateTime>("Year").Year < 2019)
                    .Select(b => b.Field<double>("Manure_tha") * 0).ToList());

                    // check for mean implementation (done in 2023, therefore 17 years = 17 entries per plot)
                    // check for mean implementation (year < 2020 used, therefore 14 years = 14 entries per plot)
                    if (manureListOnePlotAllYears.Count()!=13)
                    {
                        var ta = row.Field<DateTime>("Year").Year;
                        var aa = row.Field<string>("EP_PlotID");
                        var ba = row.Field<string>("TypeManure");
                        var ca = row.Field<double>("Manure_tha");
                    }
                }

                // get NorgManure for the cuurent year
                // need to check if exact value (exactval= yes)
                string ExactValOrgY0 = row.Field<string>("ExactValOrg");
                // exactVal for manure (Manure_tha > 0) exists
                double NorgManure = row.Field<double>("Manure_tha");

                if (ExactValOrgY0 == "ja" && NorgManure > 0)
                {
                    NorgManure = row.Field<double>("NorgExact");
                }
                else
                {
                    switch (row.Field<string>("TypeManure"))
                    {
                        case "Rind":
                            NorgManure = NorgManure * 5.6;
                            break;
                        case "Pferd":
                            NorgManure = NorgManure * 4.9;
                            break;
                        case "Schaf":
                            NorgManure = NorgManure * 8.13;
                            break;
                        default:
                            NorgManure = 0;
                            break;
                    }
                }

                // Get NorgManure for the 2 past years before the current year and plot

                // Correct NorgManure for lagged release (40-30-30 rule)
                int year = int.Parse(row.Field<DateTime>("Year").ToString("yyyy"));
                string year1 = (year - 1).ToString();
                string year2 = (year - 2).ToString();
                List<DataRow> pastYears = GetYearsBeforeData(year);

                // to hold the manure of the year-1
                double NorgManureY1 = 0;
                // to hold the manure of the year-2
                double NorgManureY2 = 0;
                // to hold the effectice manure (after using the 40-30-30 rule)
                double NorgManureEff = 0;


                // our data starts in the year 2006, therefore no data from the earlier times exits
                // we simulate the manure rule by calculating the mean of the existing data (after 2006), this world becuase the rule was introduced later where already data exists
                if (year == 2006)
                {
                    // apply 40-30-30 rule to manure
                    // use the mean instead a value of the years before (2004 + 2005)
                    NorgManureEff = Math.Round(((0.4 * NorgManure) + (0.6 * manureListOnePlotAllYears.Average())),4);
                    manureListOnePlotAllYears.Clear();
                }
                else
                {
                    // we can calculate at least one year before the current year
                    var NorgManureY1data = pastYears.AsEnumerable().Where(a => a.Field<DateTime>("Year").ToString("yyyy") == year1 && a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID")).FirstOrDefault();

                    if (NorgManureY1data != null)
                    {
                        // need to check if exact value (exactval= yes) for manure (Manure_tha > 0) exists
                        string ExactValOrgY1 = NorgManureY1data.Field<string>("ExactValOrg");
                        NorgManureY1 = NorgManureY1data.Field<double>("Manure_tha");

                        if (ExactValOrgY1 == "ja" && NorgManureY1 > 0)
                        {
                            NorgManureY1 = NorgManureY1data.Field<double>("NorgExact");
                        }
                        else
                        {
                            switch (NorgManureY1data.Field<string>("TypeManure"))
                            {
                                case "Rind":
                                    NorgManureY1 = NorgManureY1data.Field<double>("Manure_tha") * 5.6;
                                    break;
                                case "Pferd":
                                    NorgManureY1 = NorgManureY1data.Field<double>("Manure_tha") * 4.9;
                                    break;
                                case "Schaf":
                                    NorgManureY1 = NorgManureY1data.Field<double>("Manure_tha") * 8.13;
                                    break;
                                default:
                                    NorgManureY1 = 0;
                                    break;
                            }
                        }
                    }

                    if (year == 2007)
                    {
                        // apply 40-30-30 rule to manure
                        // use the mean for one part, and the value of 2006 
                        NorgManureEff = Math.Round(((0.4 * NorgManure) + (0.3 * NorgManureY1) + (0.3 * manureListOnePlotAllYears.Average())),4);
                        manureListOnePlotAllYears.Clear();
                    }
                    else
                    {
                        var NorgManureY2data = pastYears.AsEnumerable().Where(a => a.Field<DateTime>("Year").ToString("yyyy") == year2 && a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID")).FirstOrDefault();

                        if (NorgManureY2data != null)
                        {
                            // need to check if exact value (exactval= yes) for manure (Manure_tha > 0) exists
                            string ExactValOrgY2 = NorgManureY2data.Field<string>("ExactValOrg");
                            NorgManureY2 = NorgManureY2data.Field<double>("Manure_tha");

                            if (ExactValOrgY2 == "ja" && NorgManureY2 > 0)
                            {
                                NorgManureY2 = NorgManureY2data.Field<double>("NorgExact");
                            }
                            else
                            {
                                switch (NorgManureY2data.Field<string>("TypeManure"))
                                {
                                    case "Rind":
                                        NorgManureY2 = NorgManureY2data.Field<double>("Manure_tha") * 5.6;
                                        break;
                                    case "Pferd":
                                        NorgManureY2 = NorgManureY2data.Field<double>("Manure_tha") * 4.9;
                                        break;
                                    case "Schaf":
                                        NorgManureY2 = NorgManureY2data.Field<double>("Manure_tha") * 8.13;
                                        break;
                                    default:
                                        NorgManureY2 = 0;
                                        break;
                                }
                            }
                        }
                        // apply 40-30-30 rule to manure
                        NorgManureEff = (0.4 * NorgManure) + (0.3 * NorgManureY1) + (0.3 * NorgManureY2);
                    }
                }
                /*
                 * 
                 * Calculation for 
                 * 
                 * Slurry
                 * 
                 */

                // need to check if exact value (exactval= yes) for slurry (Slurry_tha > 0) exists
                double NorgSlurry = row.Field<double>("Slurry_m3ha");

                if (ExactValOrgY0 == "ja" && NorgSlurry > 0)
                {
                    NorgSlurry = row.Field<double>("NorgExact");
                }
                else
                {
                    switch (row.Field<string>("TypeSlurry"))
                    {
                        case "Rind":
                            NorgSlurry = NorgSlurry * 3.85;
                            break;
                        case "Schwein":
                            NorgSlurry = NorgSlurry * 5.4;
                            break;
                        case "Misch":
                            NorgSlurry = NorgSlurry * 4.45;
                            break;
                        case "Digestate":
                            NorgSlurry = NorgSlurry * 4.4;
                            break;
                        case "Biogas":
                            NorgSlurry = NorgSlurry * 4.4;
                            break;
                        default:
                            NorgSlurry = 0;
                            break;
                    }
                }

                /*
                 * 
                 * Calculation for 
                 * 
                 * Biogas
                 * 
                 */

                // need to check if exact value (exactval= yes) for biogas (Biogas_tha > 0) exists
                double NorgBiogas = row.Field<double>("Biogas_m3ha") * 4.4;

                if (ExactValOrgY0 == "ja" && NorgBiogas > 0)
                {
                    NorgBiogas = row.Field<double>("NorgExact");
                }

                // Workaround for Schlempe
                if (ExactValOrgY0 == "ja" && NorgManure == 0 && NorgSlurry == 0 && NorgBiogas == 0)
                {
                    NorgSlurry = row.Field<double>("NorgExact");
                }

                /*
                 * 
                 * Calculation for 
                 * 
                 * Totals 
                 * 
                 */

                //Calculate total organic N
                double Norg;
                Norg = NorgManureEff + NorgSlurry + NorgBiogas;
                                
                // Calculate TotalFertilization (organic + mineral)
                // Check if minN is a value
                var temp_minN = row.Field<double>("minNitrogen_kgNha") < 0 ? 0 : row.Field<double>("minNitrogen_kgNha");
                // calculate
                var TotalFertilization = temp_minN + Norg;
                dr["TotalFertilization"] = TotalFertilization;

                #endregion

                luiComponents.Rows.Add(dr);
            }

            return luiComponents;
        }


        private List<DataRow> GetYearsBeforeData(int year)
        {
            string year1 = (year-1).ToString();
            string year2 = (year - 2).ToString();
            var data = landuseFullData.AsEnumerable().Where(a => a.Field<DateTime>("Year").ToString("yyyy") == year1 || a.Field<DateTime>("Year").ToString("yyyy") == year2).ToList();

            return data;
        }


        private void DataCorrections()
        {
            //Interpolate missing data of fertilization -> not needed because the case is very rare
            
           var slurryNAs = landuseData.AsEnumerable().Where(r => r.Field<double>("Slurry_m3ha") == -999999 && r.Field<string>("TypeSlurry") == "999999");

            foreach(var row in slurryNAs)
            {
                warnings.Add("Slurry_m3ha and TypeSlurry == NA for Plot: " + row.Field<string>("EP_PlotID") + "and Year: " + row.Field<DateTime>("Year").ToString("yyyy"));
            }

            var NitrogenNAs = landuseData.AsEnumerable().Where(r => r.Field<double>("minNitrogen_kgNha") == -999999);

            foreach (var row in NitrogenNAs)
            {
                warnings.Add("NitrogenNAs == NA for Plot: " + row.Field<string>("EP_PlotID") + "and Year: " + row.Field<DateTime>("Year").ToString("yyyy"));
            }


            //Replace NA in GrazingArea with Zeros
            var GrazingArea1Rows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea1") == -999999);
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea1", "0");
              
            }
            var GrazingArea2Rows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea2") == -999999);
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea2", "0");

            }
            var GrazingArea3Rows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea3") == -999999);
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea3", "0");

            }
            var GrazingArea4Rows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea4") == -999999);
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea4", "0");

            }

            //Replace missing values (zeros) in GrazingArea with SizeManagementUnit
            var GrazingArea1ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea1") == 0 && ((r.Field<double>("LivestockUnits1") > 0) || (r.Field<double>("DayGrazing1")) > 0));
            foreach (var row in GrazingArea1ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<double>("SizeManagementUnit"));

            }
            var GrazingArea2ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea2") == 0 && (r.Field<double>("LivestockUnits2") > 0 || r.Field<double>("DayGrazing2") > 0));
            foreach (var row in GrazingArea2ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<string>("SizeManagementUnit"));

            }
            var GrazingArea3ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea3") == 0 && (r.Field<double>("LivestockUnits3") > 0 || r.Field<double>("DayGrazing3") > 0));
            foreach (var row in GrazingArea3ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<double>("SizeManagementUnit"));

            }
            var GrazingArea4ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea4") == 0 && (r.Field<double>("LivestockUnits4") > 0 || r.Field<double>("DayGrazing4") > 0));
            foreach (var row in GrazingArea4ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<double>("SizeManagementUnit"));

            }
        }
    }
        
}