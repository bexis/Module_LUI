using BExIS.Modules.Lui.UI.Helper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace BExIS.Modules.Lui.UI.Models
{
    public class CalculateLui
    {
        public enum ComponentsSet
        {
            Old,
            New
        };

        public static DataTable DoCalc(LUIQueryModel model, DataTable dt_sourceData)
        {
            // -----------------------------------------------------------------------------------------
            // initiate some neede variables
            //
            //

            // result data
            DataTable dt_rslts = MakeResultsDT();
            // clear result data before filling it again
            dt_rslts.Clear();

            // -------------------------------------------
            // Plotlevel
            bool EpLevel = false;
            bool MipLevel = false;
            bool VipLevel = false;

            if (model.Plotlevel.SelectedValue == "EPs") { EpLevel = true; }
            else if (model.Plotlevel.SelectedValue == "MIPs") { MipLevel = true; }
            else if (model.Plotlevel.SelectedValue == "VIPs") { VipLevel = true; }

            // -------------------------------------------
            // Explos and Years
            // gets selected Years
            List<string> selectedYearList = new List<string>();
            if(model.AvailableYearsNewComp.Where(li => li.Checked).Select(li => li.Name).ToList().Count() >0)
                selectedYearList = model.AvailableYearsNewComp.Where(li => li.Checked).Select(li => li.Name).ToList();
            else
                selectedYearList = model.AvailableYearsOldComp.Where(li => li.Checked).Select(li => li.Name).ToList();

            // gets selected Exploratories
            List<string> selectedExploList = model.Explos.Where(li => li.Checked).Select(li => li.Name).ToList();

            // concatenation of the year to be added to the result later
            string exploConcat = "";

            // select expression 4 retrieving the means later
            string slctdYrExpression = "";
            // based on exploratory
            string slctdExploExpression = "";
            // to hold the calutaed means later
            object meanGrazingYears;
            object meanMowingYears;
            object meanFertilizationYears;
            //
            //
            // initiation done
            // -----------------------------------------------------------------------------------------


            // -----------------------------------------------------------------------------------------
            // calculate the lui and fill the results datatable
            //
            //

            // -------------------------------------------------------------------------
            // calculate lui seperately for each year (also if just one year was choosen)
            // means e.g. if 2006 + 2007 were choosen: result 2006 = value2006/mean(values2006), result2007 = value2007/mean(values2007)

            if (model.TypeOfMean.SelectedValue == "separately" || model.TypeOfMean.SelectedValue == "empty")
            {
                // yearwise
                foreach (string slctdYr in selectedYearList)
                {
                    // fill the select expression to retrieve the means
                    // based on year selection
                    slctdYrExpression = "(Year =' " + DateTime.ParseExact(slctdYr, "yyyy", CultureInfo.InvariantCulture) + "')";

                    // -------------------------------------------
                    // regional way
                    if (model.Scales.SelectedValue == "regional")
                    {
                        // exploratorywise
                        foreach (string explo in selectedExploList)
                        {
                            // adapt the select expression to retrieve the means
                            // based on exploratory
                            slctdExploExpression = " AND Exploratory = '" + explo + "'";

                            // calculate the regional means for the current (meaning the current loop) year
                            meanGrazingYears = dt_sourceData.Compute("Avg(TotalGrazing)", slctdYrExpression + slctdExploExpression);
                            meanMowingYears = dt_sourceData.Compute("Avg(TotalMowing)", slctdYrExpression + slctdExploExpression);
                            meanFertilizationYears = dt_sourceData.Compute("Avg(TotalFertilization)", slctdYrExpression + slctdExploExpression);

                            // run through source data rows
                            foreach (DataRow row in dt_sourceData.Rows)
                            {
                                // calculate only for selected plotlevels
                                if (EpLevel || (MipLevel & row["isMIP"].ToString() == "yes") || (VipLevel & row["isVIP"].ToString() == "yes"))
                                {
                                    // calculate only for selected years AND selected exploratories
                                    // yearwise + exploratorywise
                                    if (DateTime.Parse(row["Year"].ToString()).Year.ToString() == slctdYr && (row["Exploratory"].ToString() == explo))
                                    {
                                        string plotid = row["EP_PlotID"].ToString();

                                        double calcG = Convert.ToDouble(row["TotalGrazing"]) / Convert.ToDouble(meanGrazingYears);
                                        double calcM = Convert.ToDouble(row["TotalMowing"]) / Convert.ToDouble(meanMowingYears);
                                        double calcF = Convert.ToDouble(row["TotalFertilization"]) / Convert.ToDouble(meanFertilizationYears);

                                        double lui = Math.Sqrt(calcG + calcM + calcF);

                                        // round
                                        calcG = Math.Round(calcG, 2, MidpointRounding.AwayFromZero);
                                        calcM = Math.Round(calcM, 2, MidpointRounding.AwayFromZero);
                                        calcF = Math.Round(calcF, 2, MidpointRounding.AwayFromZero);
                                        lui = Math.Round(lui, 2, MidpointRounding.AwayFromZero);

                                        dt_rslts.Rows.Add(plotid, "separately(" + slctdYr + ")", "regional(" + explo + ")", calcG, calcM, calcF, lui);
                                    }
                                }
                            }
                        }
                    }

                    // -------------------------------------------
                    // global "way"
                    else if (model.Scales.SelectedValue == "global")
                    {
                        // adapt the select expression to retrieve the means
                        // based on exploratory
                        slctdExploExpression = "AND (Exploratory = '" + selectedExploList[0].ToString() + "'";
                        exploConcat = selectedExploList[0].ToString();
                        for (int i = 1; i < selectedExploList.Count; i++)
                        {
                            slctdExploExpression = slctdExploExpression + " OR Exploratory = '" + selectedExploList[i].ToString() + "'";
                            exploConcat = exploConcat + ", " + selectedExploList[i].ToString();
                        }
                        slctdExploExpression = slctdExploExpression + ")";


                        // calculate the global means for the selected years 
                        // and the selcted exploratories (at least 2 to form a global)
                        meanGrazingYears = dt_sourceData.Compute("Avg(TotalGrazing)", slctdYrExpression + slctdExploExpression);
                        meanMowingYears = dt_sourceData.Compute("Avg(TotalMowing)", slctdYrExpression + slctdExploExpression);
                        meanFertilizationYears = dt_sourceData.Compute("Avg(TotalFertilization)", slctdYrExpression + slctdExploExpression);

                        foreach (DataRow row in dt_sourceData.Rows)
                        {
                            // calculate only for selected plots
                            if (EpLevel || (MipLevel & row["isMIP"].ToString() == "yes") || (VipLevel & row["isVIP"].ToString() == "yes"))
                            {
                                // calculate only for selected years
                                if (DateTime.Parse(row["Year"].ToString()).Year.ToString() == slctdYr)
                                {
                                    string plotid = row["EP_PlotID"].ToString();

                                    double calcG = Convert.ToDouble(row["TotalGrazing"]) / Convert.ToDouble(meanGrazingYears);
                                    double calcM = Convert.ToDouble(row["TotalMowing"]) / Convert.ToDouble(meanMowingYears);
                                    double calcF = Convert.ToDouble(row["TotalFertilization"]) / Convert.ToDouble(meanFertilizationYears);

                                    double lui = Math.Sqrt(calcG + calcM + calcF);

                                    // round
                                    calcG = Math.Round(calcG, 2, MidpointRounding.AwayFromZero);
                                    calcM = Math.Round(calcM, 2, MidpointRounding.AwayFromZero);
                                    calcF = Math.Round(calcF, 2, MidpointRounding.AwayFromZero);
                                    lui = Math.Round(lui, 2, MidpointRounding.AwayFromZero);

                                    dt_rslts.Rows.Add(plotid, "separately(" + slctdYr + ")", "global(" + exploConcat + ")", calcG, calcM, calcF, lui);
                                }
                            }
                        }
                    }
                }
            }

            // -------------------------------------------------------------------------
            // calculate lui overall years
            // means e.g. if 2006 + 2007 were choosen: result = mean(value2006+value2007) / mean(values2006+values2007)

            else if (model.TypeOfMean.SelectedValue == "overall")
            {
                // concatenation of the year to be added to the result later
                string yearsConcat = "";
                // to get affected plots later
                List<string> plotList = new List<string>();

                // fill the select expression 4 retrieving the means based on year selection
                // fill the yearsConcat string
                slctdYrExpression = "(Year = '" + DateTime.ParseExact(selectedYearList[0], "yyyy", CultureInfo.InvariantCulture) + "'";
                yearsConcat = selectedYearList[0];
                for (int i = 1; i < selectedYearList.Count; i++)
                {
                    slctdYrExpression = slctdYrExpression + " OR Year = '" + DateTime.ParseExact(selectedYearList[i], "yyyy", CultureInfo.InvariantCulture) + "'";
                    yearsConcat = yearsConcat + ", " + selectedYearList[i];
                }
                slctdYrExpression = slctdYrExpression + ")";

                // -------------------------------------------
                // regional "way"
                if (model.Scales.SelectedValue == "regional")
                {
                    // exploratorywise
                    foreach (string explo in selectedExploList)
                    {
                        // clear plotlist
                        plotList.Clear();

                        // adapt the select expression to retrieve the means
                        // based on exploratory
                        slctdExploExpression = " AND Exploratory = '" + explo + "'";

                        // calculate the regional means for all selected years and 
                        // for the current (meaning the current loop) exploratory
                        meanGrazingYears = dt_sourceData.Compute("Avg(TotalGrazing)", slctdYrExpression + slctdExploExpression);
                        meanMowingYears = dt_sourceData.Compute("Avg(TotalMowing)", slctdYrExpression + slctdExploExpression);
                        meanFertilizationYears = dt_sourceData.Compute("Avg(TotalFertilization)", slctdYrExpression + slctdExploExpression);

                        // fill plotlist
                        foreach (DataRow row in dt_sourceData.Rows)
                        {
                            // only for selected plotlevels
                            if (EpLevel || (MipLevel & row["isMIP"].ToString() == "yes") || (VipLevel & row["isVIP"].ToString() == "yes"))
                            {
                                if (row["Exploratory"].ToString() == explo)
                                {
                                    plotList.Add(row["EP_PlotID"].ToString());
                                }
                            }
                        }
                        plotList = plotList.Distinct().ToList();

                        foreach (string plot in plotList)
                        {
                            string slctdPlotExpression = " AND (EP_PlotID = '" + plot + "')";

                            object meanGrazingYearsPlot;
                            object meanMowingYearsPlot;
                            object meanFertilizationYearsPlot;

                            meanGrazingYearsPlot = dt_sourceData.Compute("Avg(TotalGrazing)", slctdYrExpression + slctdPlotExpression);
                            meanMowingYearsPlot = dt_sourceData.Compute("Avg(TotalMowing)", slctdYrExpression + slctdPlotExpression);
                            meanFertilizationYearsPlot = dt_sourceData.Compute("Avg(TotalFertilization)", slctdYrExpression + slctdPlotExpression);

                            double calcG = Convert.ToDouble(meanGrazingYearsPlot) / Convert.ToDouble(meanGrazingYears);
                            double calcM = Convert.ToDouble(meanMowingYearsPlot) / Convert.ToDouble(meanMowingYears);
                            double calcF = Convert.ToDouble(meanFertilizationYearsPlot) / Convert.ToDouble(meanFertilizationYears);

                            double lui = Math.Sqrt(calcG + calcM + calcF);

                            // round
                            calcG = Math.Round(calcG, 2, MidpointRounding.AwayFromZero);
                            calcM = Math.Round(calcM, 2, MidpointRounding.AwayFromZero);
                            calcF = Math.Round(calcF, 2, MidpointRounding.AwayFromZero);
                            lui = Math.Round(lui, 2, MidpointRounding.AwayFromZero);

                            dt_rslts.Rows.Add(plot, "overall(" + yearsConcat + ")", "regional(" + explo + ")", calcG, calcM, calcF, lui);
                        }
                    }
                }

                // -------------------------------------------
                // global "way"
                if (model.Scales.SelectedValue == "global")
                {
                    // clear plotlist
                    plotList.Clear();

                    // adapt the select expression to retrieve the means
                    // based on exploratory
                    // based on exploratory
                    slctdExploExpression = "AND (Exploratory = '" + selectedExploList[0].ToString() + "'";
                    exploConcat = selectedExploList[0].ToString();
                    for (int i = 1; i < selectedExploList.Count; i++)
                    {
                        slctdExploExpression = slctdExploExpression + " OR Exploratory = '" + selectedExploList[i].ToString() + "'";
                        exploConcat = exploConcat + ", " + selectedExploList[i].ToString();
                    }
                    slctdExploExpression = slctdExploExpression + ")";

                    // calculate the global means for the selected years
                    // and exploratories (at least 2 to form a global)
                    meanGrazingYears = dt_sourceData.Compute("Avg(TotalGrazing)", slctdYrExpression + slctdExploExpression);
                    meanMowingYears = dt_sourceData.Compute("Avg(TotalMowing)", slctdYrExpression + slctdExploExpression);
                    meanFertilizationYears = dt_sourceData.Compute("Avg(TotalFertilization)", slctdYrExpression + slctdExploExpression);

                    // fill plotlist
                    foreach (DataRow row in dt_sourceData.Rows)
                    {
                        if (EpLevel || (MipLevel & row["isMIP"].ToString() == "yes") || (VipLevel & row["isVIP"].ToString() == "yes"))
                        {
                            plotList.Add(row["EP_PlotID"].ToString());
                        }
                    }
                    plotList = plotList.Distinct().ToList();

                    foreach (string plot in plotList)
                    {
                        string slctdPlotExpression = " AND (EP_PlotID = '" + plot + "')";

                        object meanGrazingYearsPlot;
                        object meanMowingYearsPlot;
                        object meanFertilizationYearsPlot;

                        meanGrazingYearsPlot = dt_sourceData.Compute("Avg(TotalGrazing)", slctdYrExpression + slctdPlotExpression);
                        meanMowingYearsPlot = dt_sourceData.Compute("Avg(TotalMowing)", slctdYrExpression + slctdPlotExpression);
                        meanFertilizationYearsPlot = dt_sourceData.Compute("Avg(TotalFertilization)", slctdYrExpression + slctdPlotExpression);

                        double calcG = Convert.ToDouble(meanGrazingYearsPlot) / Convert.ToDouble(meanGrazingYears);
                        double calcM = Convert.ToDouble(meanMowingYearsPlot) / Convert.ToDouble(meanMowingYears);
                        double calcF = Convert.ToDouble(meanFertilizationYearsPlot) / Convert.ToDouble(meanFertilizationYears);

                        double lui = Math.Sqrt(calcG + calcM + calcF);

                        // round
                        calcG = Math.Round(calcG, 2, MidpointRounding.AwayFromZero);
                        calcM = Math.Round(calcM, 2, MidpointRounding.AwayFromZero);
                        calcF = Math.Round(calcF, 2, MidpointRounding.AwayFromZero);
                        lui = Math.Round(lui, 2, MidpointRounding.AwayFromZero);

                        dt_rslts.Rows.Add(plot, "overall(" + yearsConcat + ")", "global(" + exploConcat + ")", calcG, calcM, calcF, lui);
                    }
                }
            }
            //
            //
            // calculation done
            // -----------------------------------------------------------------------------------------

            // -----------------------------------------------------------------------------------------
            // bind results datatable to gridview and show it
            //
            //

            return dt_rslts;
        }

        /// <summary>
        /// initialize an empty LUI results table
        /// </summary>
        /// <returns></returns>
        private static DataTable MakeResultsDT()
        {
            DataTable dt = new DataTable();
            // add needed columns
            dt.Columns.Add("PLOTID", typeof(string)).SetOrdinal(0);
            dt.Columns.Add("YEAR", typeof(string)).SetOrdinal(1);
            dt.Columns.Add("EXPLO", typeof(string)).SetOrdinal(2);
            dt.Columns.Add("G_STD", typeof(double)).SetOrdinal(3);
            dt.Columns.Add("M_STD", typeof(double)).SetOrdinal(4);
            dt.Columns.Add("F_STD", typeof(double)).SetOrdinal(5);
            dt.Columns.Add("LUI", typeof(double)).SetOrdinal(6);
            return dt;
        }
    }
}