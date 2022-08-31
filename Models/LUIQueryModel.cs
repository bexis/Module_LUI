using System.Collections.Generic;
using BExIS.Modules.Lui.UI.Models;

namespace BExIS.Modules.Lui.UI.Models
{
    //public enum Scale { regional, global }
    //public enum Explo { ALB, HAI, SCH }

    public class LUIQueryModel
    {
        public RadioButtonControlHelper RawVsCalc { get; set; }
        public RadioButtonControlHelper Scales { get; set; }
        public List<CheckboxControlHelper> Explos { get; set; }
        //public List<CheckboxControlHelper> Years { get; set; }
        public RadioButtonControlHelper TypeOfMean { get; set; }
        public RadioButtonControlHelper Plotlevel { get; set; }

        //public CalculateLui.ComponentsSet ComponentsSet { get; set; }

        public RadioButtonControlHelper ComponentsSet { get; set; }

        public List<MissingComponentData> MissingComponentData { get; set; }

        public List<CheckboxControlHelper> AvailableYearsNewComp { get; set; }

        public List<CheckboxControlHelper> AvailableYearsOldComp { get; set; }


        public string NewComponentsSetDatasetId { get; set; }

        public string NewComponentsSetDatasetVersion { get; set; }

        public string DownloadDatasetId { get; set; }

        public string NewComponentsSetLastUpdate { get; set; }

        public bool IsPublicAccess { get; set; }


        public LUIQueryModel()
        {
            // initiate model
            RawVsCalc = new RadioButtonControlHelper();
            Scales = new RadioButtonControlHelper();
            Explos = new List<CheckboxControlHelper>();
            TypeOfMean = new RadioButtonControlHelper();
            Plotlevel = new RadioButtonControlHelper();
            ComponentsSet = new RadioButtonControlHelper();
            AvailableYearsNewComp = new List<CheckboxControlHelper>();
            AvailableYearsOldComp = new List<CheckboxControlHelper>();


            IsPublicAccess = false;

            MissingComponentData = new List<MissingComponentData>();

            //fill ComponentsSet
            ComponentsSet.SelectedValue = "default components set";
            ComponentsSet.Values = new List<string>() { "historic components set", "default components set" };


            // fill RawVsCalc
            RawVsCalc.SelectedValue = null;
            RawVsCalc.Values = new List<string>() { "unstandardized", "standardized" };

            // fill Scales
            Scales.SelectedValue = null;
            Scales.Values = new List<string>() { "regional", "global" };

            // fill explos
            Explos.Add(new CheckboxControlHelper { Name = "ALB", Checked = false });
            Explos.Add(new CheckboxControlHelper { Name = "HAI", Checked = false });
            Explos.Add(new CheckboxControlHelper { Name = "SCH", Checked = false });

            // fill Years          
            //int fromYear = (int)Settings.get("lui:years:min");
            //int toYear = (int)Settings.get("lui:years:max");
            //for (int i = fromYear; i <= toYear; i++)
            //{
            //    Years.Add(new CheckboxControlHelper { Name = i.ToString(), Checked = false });

            //}

            // fill TypeOfMeans
            TypeOfMean.SelectedValue = "empty";
            TypeOfMean.Values = new List<string>() { "separately", "overall" };

            // fill Plotlevel
            Plotlevel.SelectedValue = null;
            Plotlevel.Values = new List<string>() { "VIPs", "MIPs", "EPs" };
        }
    }

    public class CheckboxControlHelper
    {
        public string Name { get; set; }
        public bool Checked { get; set; }
    }

    public class RadioButtonControlHelper
    {
        public string SelectedValue { get; set; }
        public List<string> Values { get; set; }
    }

    public class MissingComponentData
    {
        public string Year { get; set; }

        public List<string> PlotIds { get; set; }

        public MissingComponentData()
        {
            PlotIds = new List<string>();
        }

    }







}