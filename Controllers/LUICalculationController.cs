﻿using BExIS.Modules.Lui.UI.Models;
using System.Web.Mvc;
using Vaiona.Web.Mvc.Models;
using System.Collections.Generic;
using System;
using System.Data;
using System.Linq;
using System.IO;
using System.Web;
using BExIS.Modules.Lui.UI.Helper;
using BExIS.Security.Services.Utilities;
using BExIS.Security.Services.Subjects;
using System.Net;
using System.Web.Script.Serialization;
using Vaiona.Web.Extensions;
using Vaiona.Utils.Cfg;
using Ionic.Zip;
using System.Xml;
using System.Text;
using BExIS.Utils.Extensions;
using Vaiona.Web.Mvc.Modularity;
using System.Web.Routing;

namespace BExIS.Modules.Lui.UI.Controllers
{
    public class LUICalculationController : Controller
    {
        #region constants
        // page title
        private static string TITLE = "LUI Calculation";

        // session variable names
        private static string SESSION_TABLE = "lui:resultTable";
        private static string SESSION_FILE = "lui:resultFile";

        // namespace for download files
        private static string FILE_NAMESPACE = Models.Settings.get("lui:filename:namespace") as string;
        #endregion

        // GET: Main
        public ActionResult Index()
        {
            if (checkPreconditions())
            {
                // set page title
                ViewBag.Title = PresentationModel.GetViewTitleForTenant(TITLE, this.Session.GetTenant());

                //create model
                LUIQueryModel model = new LUIQueryModel();
                model.MissingComponentData = DataAccess.GetMissingComponentData();
                model.NewComponentsSetDatasetId = Models.Settings.get("lui:datasetNewComponentsSet").ToString();
                model.NewComponentsSetDatasetVersion = DataAccess.GetDatasetInfo(model.NewComponentsSetDatasetId).Version;
                return View("Index", model);

            } 
            else
            {
                // preconditions failed, show error page
                return View("Error");

            }
        }

        public ActionResult ShowPrimaryData(long datasetID)
        {
            LUIQueryModel lUIQueryModel = new LUIQueryModel();
            lUIQueryModel.RawVsCalc.SelectedValue = "unstandardized";
            lUIQueryModel.DownloadDatasetId = datasetID.ToString();
            Session["LUICalModel"] = lUIQueryModel;

            DataModel model = new DataModel();
            model.Data = DataAccess.GetComponentData(datasetID.ToString());

            return PartialView("_data", model);
        }

        /// <summary>
        /// trigger calculation of LUI values
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult CalculateLUI(LUIQueryModel model)
        {
            Session["LUICalModel"] = null;
            // set page title
            ViewBag.Title = PresentationModel.GetViewTitleForTenant(TITLE, this.Session.GetTenant());

            long selectedDataStructureId;

            Session["DataStructureId"] = null;

            if (model.ComponentsSet.SelectedValue == "old components set")
            {
                selectedDataStructureId = (int)Models.Settings.get("lui:datastructureOldComponentsSet");
                model.DownloadDatasetId = Models.Settings.get("lui:datasetOldComponentsSet").ToString();
            }
            else
            {
                selectedDataStructureId = (int)Models.Settings.get("lui:datastructureNewComponentsSet");
                model.DownloadDatasetId = Models.Settings.get("lui:datasetNewComponentsSet").ToString();

            }

            Session["DataStructureId"] = selectedDataStructureId;

            // do the calucaltion
            var results = CalculateLui.DoCalc(model);

            DataModel dataModel = new DataModel();
            dataModel.Data = results;

            // store results in session
            Session[SESSION_TABLE] = results;
            if (null != Session[SESSION_FILE])
            {
                ((Dictionary<string, string>)Session[SESSION_FILE]).Clear();
            }
            Session["LUICalModel"] = model;

            return PartialView("_data", dataModel);
        }


        /// <summary>
        /// prepare the serialized data file for download
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        public ActionResult PrepareDownloadFile(string mimeType)
        {
            // helper class
            DownloadManager downloadManager = new DownloadManager();

            // filename
            // use unix timestamp to make filenames unique
            string filename = Models.Settings.get("lui:filename:download") as string;

            //result datatable
            DataTable downloadData = new DataTable();

            LUIQueryModel model = (LUIQueryModel)Session["LUICalModel"];
            
            if (model.RawVsCalc.SelectedValue == "unstandardized")
              downloadData  = DataAccess.GetComponentData(model.DownloadDatasetId);
            else
                downloadData = Session[SESSION_TABLE] as DataTable;

            string mean = "";
            if (model.TypeOfMean.SelectedValue != "empty")
                mean = "_" + model.TypeOfMean.SelectedValue;
            filename += "_" + model.ComponentsSet.SelectedValue + "_" + model.Scales.SelectedValue + mean + "_" + DateTime.Now.ToString("yyyy-MM-dd");

            // datastructure ID
            //int dsId = (int)Settings.get("lui:datastructure");

            // depends on the requested type
            string path = "";
            switch (mimeType)
            {
                case "text/csv":
                case "text/tsv":
                    path = downloadManager.GenerateAsciiFile(FILE_NAMESPACE, downloadData as DataTable, filename, mimeType);
                    break;

                case "application/vnd.ms-excel.sheet.macroEnabled.12":
                case "application/vnd.ms-excel":
                    //path = outputDataManager.GenerateExcelFile(FILE_NAMESPACE, Session[SESSION_TABLE] as DataTable, filename, selectedDataStructureId);
                    break;

                default:
                    Response.StatusCode = 420;
                    return Json(new { error = true, msg = "Unknown file-type: " + mimeType }, JsonRequestBehavior.AllowGet);
            }

            //get metadata as html
            //XmlDocument xmlDocument = DataAccess.GetMetadata(model.DownloadDatasetId);

            //string htmlPage = PartialView("SimpleMetadata", xmlDocument).RenderToString();
            DatasetObject datasetObject = DataAccess.GetDatasetInfo(model.DownloadDatasetId);
            var view = this.Render("DCM", "Form", "LoadMetadataOfflineVersion", new RouteValueDictionary()
            {
                { "entityId", long.Parse(model.DownloadDatasetId) },
                { "title", "" },
                { "metadatastructureId", long.Parse(datasetObject.MetadataStructureId) },
                { "datastructureId", long.Parse(datasetObject.DataStructureId) },
                { "researchplanId", null },
                { "sessionKeyForMetadata", "ShowDataMetadata" },
                { "resetTaskManager", false }
            });

            string pathHtml = downloadManager.GenerateHtmlFile(view.ToHtmlString(), model.DownloadDatasetId + "_metadata");

            //get missing data when relavent
            List<MissingComponentData> missingComponentData = DataAccess.GetMissingComponentData();
            string pathMissingData = "";
            if (missingComponentData.Count > 0)
            {
                string version = DataAccess.GetDatasetInfo(model.DownloadDatasetId).Version;
                string filenameMissingdata = model.DownloadDatasetId + "_" + "Version_" + version + "_" + "MissingComponentData";
                pathMissingData = downloadManager.GernateMissingDataFile(missingComponentData, filenameMissingdata);
            }
            

            string zipFilePath = Path.Combine(AppConfiguration.DataPath, "LUI", "Temp", "LuiData" + ".zip");

            //create zip
            using (ZipFile zip = new ZipFile())
            {
                zip.AddFile(path, "");
                zip.AddFile(pathHtml, "");
                if(pathMissingData.Length > 0)
                    zip.AddFile(pathMissingData, "");
                zip.Save(zipFilePath);
            }



            // store path in session for further download
            if (null == Session[SESSION_FILE])
            {
                Session[SESSION_FILE] = new Dictionary<string, string>();
            }

            ((Dictionary<string, string>)Session[SESSION_FILE])[mimeType] = zipFilePath;




            
            return Json(new { error = false, mimeType = mimeType }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// return the serialized data file
        /// if the file does not exist yet, it will be created
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        public ActionResult DownloadFile(string mimeType)
        {
            // make sure the file was created
            if ((null == Session[SESSION_FILE]) || !((Dictionary<string, string>)Session[SESSION_FILE]).ContainsKey(mimeType))
            {
                ActionResult res = PrepareDownloadFile(mimeType);

                // check, if everything went ok
                if (200 != Response.StatusCode)
                {
                    return res;
                }
            }

            // get data file path
            string pathData = ((Dictionary<string, string>)Session[SESSION_FILE])[mimeType];

           
            //send mail
            var es = new EmailService();
            string user;
            using (UserManager userManager = new UserManager())
            {
                user = userManager.FindByNameAsync(HttpContext.User.Identity.Name).Result.DisplayName;
            }
            LUIQueryModel model = (LUIQueryModel)Session["LUICalModel"];
            string datasetId;
            if (model.ComponentsSet.SelectedValue.Contains("old"))
                datasetId = Models.Settings.get("lui:datasetOldComponentsSet").ToString();
            else
                datasetId = Models.Settings.get("lui:datasetNewComponentsSet").ToString();

            string version = DataAccess.GetDatasetInfo(datasetId).Version; 

            string text = "LUI Calculation file <b>\"" + Path.GetFileName(pathData) + "\"</b> with id <b>(" + datasetId + ")</b> version <b>(" + version + ")</b> was downloaded by <b>" + user + "</b>";
            es.Send("LUI data was downloaded (Id: " + datasetId + ", Version: " + version + ")", text, "bexis-sys@listserv.uni-jena.de");


            // return file for download
            return File(pathData, mimeType, Path.GetFileName(pathData));
        }

        public ActionResult DownloadPDF(string fileName)
        {
            string path = "HelpFiles\\";

            var filePath = Path.Combine(AppConfiguration.GetModuleWorkspacePath("LUI"), path, fileName);
            Response.AddHeader("Content-Disposition", "inline; filename=" + fileName);
            return File(filePath, MimeMapping.GetMimeMapping(fileName));
        }


        /// <summary>
        /// check for preconditions, so that we can do all computations
        /// * Link to LUI dataset
        /// * Link to result data structure
        /// </summary>
        /// <returns></returns>
        private bool checkPreconditions()
        {
            // check for LUI new dataset
            bool exists = false;
            string luiIdNew = Models.Settings.get("lui:datasetNewComponentsSet").ToString();
            var dataNew = DataAccess.GetComponentData(luiIdNew);
            if (dataNew.Rows.Count == 0)
                return exists == false;

            string dsdId = Models.Settings.get("lui:datastructureNewComponentsSet").ToString();

            // check for LUI old dataset
            string luiIdOld = Models.Settings.get("lui:datasetOldComponentsSet").ToString();
            var dataOld = DataAccess.GetComponentData(luiIdOld);
            if (dataOld.Rows.Count == 0)
                return exists == false;

            int dsdIdOld = (int)Models.Settings.get("lui:datastructureOldComponentsSet");

            // if we came that far, all conditions are met
            return true;
        }
    }

}
