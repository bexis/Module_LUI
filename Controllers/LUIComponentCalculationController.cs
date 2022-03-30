using BExIS.Modules.Lui.UI.Helper;
using BExIS.Modules.Lui.UI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Vaiona.Utils.Cfg;

namespace BExIS.Modules.Lui.UI.Controllers
{
    public class LUIComponentCalculationController : Controller
    {
        // GET: LUIComponentCalculation
        public ActionResult Index()
        {
            ComponentDataModel model = new ComponentDataModel();
            return View("ComponentCalculation", model);
        }

        public ActionResult CalculateCompontents()
        {
            Session["ComponentData"] = null;
            string datasetId = Models.Settings.get("lui:lanuDataset").ToString();
            //get data structureId
            long structureId = long.Parse(DataAccess.GetDatasetInfo(datasetId).DataStructureId, CultureInfo.InvariantCulture);

            DataTable lanuFullData = DataAccess.GetData(datasetId, structureId);

            //get only last year
            var lastYear = lanuFullData.AsEnumerable().Select(a => a.Field<DateTime>("Year")).Distinct().ToList().Max();
            DataTable lanuData = lanuFullData.AsEnumerable()
                                .Where(r => r.Field<DateTime>("Year") == lastYear).CopyToDataTable();

            //get plottype infos
            string datasetIdPlots = Models.Settings.get("lui:epPlotsDataset").ToString();
            //get data structureId
            long structureIdPlots = long.Parse(DataAccess.GetDatasetInfo(datasetIdPlots).DataStructureId, CultureInfo.InvariantCulture);
            DataTable plotTypes = DataAccess.GetData(datasetIdPlots, structureIdPlots);

            LUIComponentsCalculation lUIComponentsCalculation = new LUIComponentsCalculation(lanuData, lanuFullData, plotTypes);
            DataTable compData = lUIComponentsCalculation.CalculateComponents();
            ComponentDataModel model = new ComponentDataModel(compData);
            Session["ComponentData"] = model;

            return View("ComponentCalculation", model);
        }

        public ActionResult Download()
        {
            ComponentDataModel data = Session["ComponentData"] as ComponentDataModel;

            var lines = new List<string>();
            string[] columnNames = data.Data.Columns
                    .Cast<DataColumn>()
                    .Select(column => column.ColumnName)
                    .ToArray();

            var header = string.Join(",", columnNames.Select(name => $"\"{name}\""));
            lines.Add(header);

            var valueLines = data.Data.AsEnumerable()
                .Select(row => string.Join(",", row.ItemArray.Select(val => $"\"{val}\"")));

            lines.AddRange(valueLines);

            string eventName;

            //remove invaid chars in eventname for filename
            string filename = "ComponentData_";

            string dataPath = AppConfiguration.DataPath;
            string storePath = Path.Combine(dataPath, "LUI", "Temp", filename + ".csv");

            System.IO.File.WriteAllLines(storePath, lines);

            return File(storePath, MimeMapping.GetMimeMapping("ComponentData" + ".csv"), Path.GetFileName(storePath));
        }
    }
}