﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class LUIComponentsCalculation
    {
        public DataTable landuseData;
        public DataTable landuseFullData;
        public List<string> warnings;
        public LUIComponentsCalculation(DataTable data, DataTable fullData)
        {
            landuseData = data;
            landuseFullData = fullData;
            //DataCorrections();
        }

        public DataTable CalculateComponents()
        {
            //create result datatable
            DataTable luiComponents = new DataTable();
            luiComponents.Columns.Add("Year");
            luiComponents.Columns.Add("Exploratory");
            luiComponents.Columns.Add("EP_PlotID");
            luiComponents.Columns.Add("isVIP");
            luiComponents.Columns.Add("isMIP");
            luiComponents.Columns.Add("TotalGrazing");
            luiComponents.Columns.Add("TotalMowing");
            luiComponents.Columns.Add("TotalFertilization");

            #region check TotalMowing

            var totalMowing = landuseData.AsEnumerable().Select(r => r.Field<long>("Cuts")).ToList(); ;
            int sumNa = totalMowing.Where(a => a.Equals(-999999)).Count();
            if (sumNa > 0)
            {
                warnings.Add("There is missing data in TotalMowing");
            }

            #endregion

            foreach (DataRow row in landuseData.Rows)
            {
                DataRow dr = luiComponents.NewRow();
                dr["Year"] = row.Field<DateTime>("Year");
                dr["Exploratory"] = row.Field<string>("Exploratory");
                dr["EP_PlotID"] = row.Field<string>("EP_PlotID");
                //get MIP/VIP info
                dr["isVIP"] = "";
                dr["isMIP"] = "";

                dr["TotalMowing"] = row.Field<long>("Cuts");

                #region TotalGrazing
                //Calculate grazing intensities (grazing1-grazing4)
                var Grazing1 = row.Field<double>("LivestockUnits1") * row.Field<double>("DayGrazing1") / row.Field<double>("GrazingArea1");
                var Grazing2 = row.Field<double>("LivestockUnits2") * row.Field<double>("DayGrazing2") / row.Field<double>("GrazingArea2");
                var Grazing3 = row.Field<double>("LivestockUnits3") * row.Field<double>("DayGrazing3") / row.Field<double>("GrazingArea3");
                var Grazing4 = row.Field<double>("LivestockUnits4") * row.Field<double>("DayGrazing4") / row.Field<double>("GrazingArea4");

                double TotalGrazing = Grazing1 + Grazing2 + Grazing3 + Grazing4;
                TotalGrazing = System.Math.Round(TotalGrazing, 4);

                dr["TotalGrazing"] = TotalGrazing;

                #endregion

                #region TotalFertilization

                //Calculate organic N from manure, slurry, sludge and mash 

                double NorgManure;
                if (row.Field<string>("TypeManure") == "Rind")
                    NorgManure = row.Field<double>("Manure_tha") * 5.6;
                if (row.Field<string>("TypeManure") == "Pferd")
                    NorgManure = row.Field<double>("Manure_tha") * 4.9;
                if (row.Field<string>("TypeManure") == "Schaf")
                    NorgManure = row.Field<double>("Manure_tha") * 8.13;
               if (row.Field<string>("TypeManure") == "-1")
                    NorgManure = 0;
               else
                NorgManure = 0;

                double NorgSlurry;
                if (row.Field<string>("TypeSlurry") == "Rind")
                    NorgSlurry = row.Field<double>("Slurry_m3ha") * 3.85;
                if (row.Field<string>("TypeSlurry") == "Schwein")
                    NorgSlurry = row.Field<double>("Slurry_m3ha") * 5.4;
                if (row.Field<string>("TypeSlurry") == "Misch")
                    NorgSlurry = row.Field<double>("Slurry_m3ha") * 4.45;
                if (row.Field<string>("TypeSlurry") == "Digestate")
                    NorgSlurry = row.Field<double>("Slurry_m3ha") * 4.4;
                if (row.Field<string>("TypeSlurry") == "Biogas")
                    NorgSlurry = row.Field<double>("Slurry_m3ha") * 4.4;
                if (row.Field<string>("TypeSlurry") == "-1")
                    NorgSlurry = 0;
                else
                    NorgSlurry = 0;

                double NorgBiogas = row.Field<double>("Biogas_m3ha") * 4.4;

                //Correct manure N for lagged release

                int year = int.Parse(row.Field<DateTime>("Year").ToString("yyyy"));
                string year1 = (year - 1).ToString();
                string year2 = (year - 2).ToString();
                List<DataRow> pastYears = GetYearsBeforeData(year);

                //get NorgManure for past years for current (in the loop row) year and plot

                var NorgManureY1data = pastYears.AsEnumerable().Where(a => a.Field<string>("Year") == year1 && a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID")).FirstOrDefault();
                double NorgManureY1 = 0;
                if (NorgManureY1data != null)
                {
                    if (NorgManureY1data.Field<string>("TypeManure") == "Rind")
                        NorgManureY1 = NorgManureY1data.Field<double>("Manure_tha") * 5.6;
                    if (NorgManureY1data.Field<string>("TypeManure") == "Pferd")
                        NorgManureY1 = NorgManureY1data.Field<double>("Manure_tha") * 4.9;
                    if (NorgManureY1data.Field<string>("TypeManure") == "Schaf")
                        NorgManureY1 = NorgManureY1data.Field<double>("Manure_tha") * 8.13;
                    if (NorgManureY1data.Field<string>("TypeManure") == "-1")
                        NorgManureY1 = 0;
                    else
                        NorgManureY1 = 0;
                }

                var NorgManureY2data = pastYears.AsEnumerable().Where(a => a.Field<string>("Year") == year2 && a.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID")).FirstOrDefault();
                double NorgManureY2 = 0;
                if (NorgManureY2data != null)
                {
                    if (NorgManureY2data.Field<string>("TypeManure") == "Rind")
                        NorgManureY2 = NorgManureY2data.Field<double>("Manure_tha") * 5.6;
                    if (NorgManureY2data.Field<string>("TypeManure") == "Pferd")
                        NorgManureY2 = NorgManureY2data.Field<double>("Manure_tha") * 4.9;
                    if (NorgManureY2data.Field<string>("TypeManure") == "Schaf")
                        NorgManureY2 = NorgManureY2data.Field<double>("Manure_tha") * 8.13;
                    if (NorgManureY2data.Field<string>("TypeManure") == "-1")
                        NorgManureY2 = 0;
                    else
                        NorgManureY2 = 0;
                }

                double NorgManureEff = (0.4 * NorgManure) + (0.3*NorgManureY1) + (0.3* NorgManureY2);


                //Calculate total organic N or take direct measurements from the data table

                double Norg;
                if (row.Field<string>("ExactValOrg") == "ja")
                    Norg = row.Field<double>("NorgExact");
                else
                    Norg = NorgManureEff + NorgSlurry + NorgBiogas;

                //Calculate TotalFertilization

                var TotalFertilization = row.Field<double>("minNitrogen_kgNha") + Norg;

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
           var slurryNAs = landuseData.AsEnumerable().Where(r => r.Field<string>("Slurry_m3ha") == "-999999" && r.Field<string>("TypeSlurry") == "999999");

            foreach(var row in slurryNAs)
            {
                warnings.Add("Slurry_m3ha and TypeSlurry == NA for Plot: " + row.Field<string>("EP_PlotID") + "and Year: " + row.Field<string>("Year"));
            }

            var NitrogenNAs = landuseData.AsEnumerable().Where(r => r.Field<string>("minNitrogen_kgNha") == "-999999");

            foreach (var row in NitrogenNAs)
            {
                warnings.Add("NitrogenNAs == NA for Plot: " + row.Field<string>("EP_PlotID") + "and Year: " + row.Field<string>("Year"));
            }


            //Replace NA in GrazingArea with Zeros
            var GrazingArea1Rows = landuseData.AsEnumerable().Where(r => r.Field<string>("GrazingArea1") == "NA");
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea1", "0");
              
            }
            var GrazingArea2Rows = landuseData.AsEnumerable().Where(r => r.Field<string>("GrazingArea2") == "NA");
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea2", "0");

            }
            var GrazingArea3Rows = landuseData.AsEnumerable().Where(r => r.Field<string>("GrazingArea3") == "NA");
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea3", "0");

            }
            var GrazingArea4Rows = landuseData.AsEnumerable().Where(r => r.Field<string>("GrazingArea4") == "NA");
            foreach (var row in GrazingArea1Rows)
            {
                row.SetField("GrazingArea4", "0");

            }

            //Replace missing values (zeros) in GrazingArea with SizeManagementUnit
            var GrazingArea1ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea1") == 0 && ((r.Field<double>("LivestockUnits1") > 0) || (r.Field<double>("DayGrazing1")) > 0));
            foreach (var row in GrazingArea1ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<string>("SizeManagementUnit"));

            }
            var GrazingArea2ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea2") == 0 && (r.Field<double>("LivestockUnits2") > 0 || r.Field<double>("DayGrazing2") > 0));
            foreach (var row in GrazingArea2ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<string>("SizeManagementUnit"));

            }
            var GrazingArea3ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea3") == 0 && (r.Field<double>("LivestockUnits3") > 0 || r.Field<double>("DayGrazing3") > 0));
            foreach (var row in GrazingArea3ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<string>("SizeManagementUnit"));

            }
            var GrazingArea4ReplaceRows = landuseData.AsEnumerable().Where(r => r.Field<double>("GrazingArea4") == 0 && (r.Field<double>("LivestockUnits4") > 0 || r.Field<double>("DayGrazing4") > 0));
            foreach (var row in GrazingArea4ReplaceRows)
            {
                row.SetField("GrazingArea1", row.Field<string>("SizeManagementUnit"));

            }
        }


    }
        
}