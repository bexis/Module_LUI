using BExIS.Modules.Lui.UI.Models;
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
using Microsoft.AspNet.Identity;
using System.Globalization;

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
                model.MissingComponentData = DataAccess.GetMissingComponentData(GetServerInformation());

                //check if public access
                long.TryParse(this.User.Identity.GetUserId(), out long userId);

                //no bexis user == public access and id uncomplete data exsits
                if(userId == 0)
                {
                    model.IsPublicAccess = true;
                }

                model.NewComponentsSetDatasetId = Models.Settings.get("lui:datasetNewComponentsSet").ToString();
                var datasetInfo = DataAccess.GetDatasetInfo(model.NewComponentsSetDatasetId, GetServerInformation());
                model.NewComponentsSetDatasetVersion = DataAccess.GetDatasetInfo(model.NewComponentsSetDatasetId, GetServerInformation()).Version;
                XmlDocument doc =  DataAccess.GetMetadata(model.NewComponentsSetDatasetId, GetServerInformation());
                model.NewComponentsSetLastUpdate = DateTime.Parse(doc.GetElementsByTagName("metadataLastModificationDateType")[0].InnerText).ToString("yyyy-MM-dd");

                model.AvailableYearsNewComp = GetAvailableYears(model.NewComponentsSetDatasetId, model.IsPublicAccess);
                model.AvailableYearsOldComp = GetAvailableYears(Models.Settings.get("lui:datasetOldComponentsSet").ToString(), model.IsPublicAccess);

                return View("Index", model);

            } 
            else
            {
                // preconditions failed, show error page
                return View("Error");

            }
        }

        public ActionResult ShowPrimaryData(long datasetID, bool isPublicAccess)
        {
            LUIQueryModel lUIQueryModel = new LUIQueryModel();
            lUIQueryModel.RawVsCalc.SelectedValue = "unstandardized";
            lUIQueryModel.DownloadDatasetId = datasetID.ToString();
            lUIQueryModel.IsPublicAccess = isPublicAccess; 
            Session["LUICalModel"] = lUIQueryModel;

            DataModel model = new DataModel();
            //get data structureId
            long structureId = long.Parse(DataAccess.GetDatasetInfo(datasetID.ToString(), GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);
            DataTable data = DataAccess.GetData(datasetID.ToString(), structureId, GetServerInformation());
            if (isPublicAccess)
            {
                //remove not complete years data for non public access
                var missingData = DataAccess.GetMissingComponentData(GetServerInformation());
                if (missingData.Count > 0)
                {
                    List<string> incompleteYears = missingData.Select(x => x.Year).Distinct().ToList();
                    RemoveNotComplateYears(data, incompleteYears);
                    model.Data = data;

                }
                else
                    model.Data = data;
            }
            else
                model.Data = data;

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

            if (model.ComponentsSet.SelectedValue == "historic components set")
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
            // source data
            string dsId = "";
            switch (model.ComponentsSet.SelectedValue)
            {
                case "historic components set":
                    dsId = Models.Settings.get("lui:datasetOldComponentsSet").ToString();
                    break;
                case "default components set":
                    dsId = Models.Settings.get("lui:datasetNewComponentsSet").ToString();
                    break;
            }

            long structureId = long.Parse(DataAccess.GetDatasetInfo(dsId, GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);
            DataTable dt_sourceData = DataAccess.GetData(dsId, structureId, GetServerInformation());
            
            var results = CalculateLui.DoCalc(model, dt_sourceData);

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
            {
                long structureId = long.Parse(DataAccess.GetDatasetInfo(model.DownloadDatasetId, GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);
                downloadData = DataAccess.GetData(model.DownloadDatasetId, structureId, GetServerInformation());
            }
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
            DatasetObject datasetObject = DataAccess.GetDatasetInfo(model.DownloadDatasetId, GetServerInformation());
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
            List<MissingComponentData> missingComponentData = DataAccess.GetMissingComponentData(GetServerInformation());
            string pathMissingData = "";
            if (missingComponentData.Count > 0)
            {
                string version = DataAccess.GetDatasetInfo(model.DownloadDatasetId, GetServerInformation()).Version;
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

            LUIQueryModel model = (LUIQueryModel)Session["LUICalModel"];

            if (model.IsPublicAccess)
            {
                user = "public downloaded";
            }
            else
            {
                using (UserManager userManager = new UserManager())
                {
                    user = "downloaded by " + userManager.FindByNameAsync(HttpContext.User.Identity.Name).Result.DisplayName;
                }
            }

            string datasetId;
            if (model.ComponentsSet.SelectedValue.Contains("historic"))
                datasetId = Models.Settings.get("lui:datasetOldComponentsSet").ToString();
            else
                datasetId = Models.Settings.get("lui:datasetNewComponentsSet").ToString();

            string version = DataAccess.GetDatasetInfo(datasetId, GetServerInformation()).Version; 

            string text = "LUI Calculation file <b>\"" + Path.GetFileName(pathData) + "\"</b> with id <b>(" + datasetId + ")</b> version <b>(" + version + ")</b> was  <b>" + user + "</b>";
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
            try
            {
                string luiIdNew = Models.Settings.get("lui:datasetNewComponentsSet").ToString();

                long structureIdNew = long.Parse(DataAccess.GetDatasetInfo(luiIdNew, GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);
                var dataNew = DataAccess.GetData(luiIdNew, structureIdNew, GetServerInformation());
                if (dataNew.Rows.Count == 0)
                    return exists == false;

                string dsdId = Models.Settings.get("lui:datastructureNewComponentsSet").ToString();

                // check for LUI old dataset
                string luiIdOld = Models.Settings.get("lui:datasetOldComponentsSet").ToString();
                long structureIdOld = long.Parse(DataAccess.GetDatasetInfo(luiIdNew, GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);

                var dataOld = DataAccess.GetData(luiIdOld, structureIdOld, GetServerInformation());
                if (dataOld.Rows.Count == 0)
                    return exists == false;

                int dsdIdOld = (int)Models.Settings.get("lui:datastructureOldComponentsSet");
            }
            catch(Exception e)
            {
                throw new Exception("Precondion failed: ",e);
            }

            // if we came that far, all conditions are met
            return true;
        }

        /// <summary>
        /// Get current server und user information
        /// </summary>
        /// <returns></returns>
        public ServerInformation GetServerInformation()
        {
            //string filePath = Path.Combine(AppConfiguration.GetModuleWorkspacePath("LUI"), "Credentials.json");
            //string text = System.IO.File.ReadAllText(filePath);
            ServerInformation serverInformation = new ServerInformation();
            var uri = System.Web.HttpContext.Current.Request.Url;
            serverInformation.ServerName = uri.GetLeftPart(UriPartial.Authority);
            serverInformation.Token = GetUserToken();


            return serverInformation;
        }
        /// <summary>
        /// Get bexis token from logged-in user
        /// </summary>
        /// <returns></returns>
        private string GetUserToken()
        {
            var identityUserService = new IdentityUserService();
            var userManager = new UserManager();

            try
            {
                long userId = 0;
                long.TryParse(this.User.Identity.GetUserId(), out userId);

                if(userId==0)
                    return "";

                var user = identityUserService.FindById(userId);

                user = identityUserService.FindById(userId);
                var token = userManager.GetTokenAsync(user).Result;
                return token;
            }
            finally
            {
                identityUserService.Dispose();
                userManager.Dispose();
            }
        }

        /// <summary>
        /// Remove data for not complete uploaded years
        /// </summary>
        /// <returns></returns>
        private void RemoveNotComplateYears(DataTable data, List<string> years)
        {
            foreach (var year in years)
            {
                data.AsEnumerable().Where(r => r.Field<DateTime>("Year").ToString("yyyy") == year).ToList().ForEach(row => row.Delete());
                data.AcceptChanges();
            }
        }

        /// <summary>
        /// Get distinct available years from comp data
        /// </summary>
        /// <returns>List of years</returns>
        private List<CheckboxControlHelper> GetAvailableYears(string datasetId, bool isPublicAccess)
        {
            //get data structureId
            long structureId = long.Parse(DataAccess.GetDatasetInfo(datasetId.ToString(), GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);
            DataTable data = DataAccess.GetData(datasetId, structureId, GetServerInformation());
            if (isPublicAccess)
            {
                var missingData = DataAccess.GetMissingComponentData(GetServerInformation());
                if (missingData.Count > 0)
                {
                    List<string> incompleteYears = missingData.Select(x => x.Year).Distinct().ToList();
                    RemoveNotComplateYears(data, incompleteYears);
                }
            }

            var years = data.AsEnumerable().Select(r => r.Field<DateTime>("Year").ToString("yyyy")).ToList();
            List<CheckboxControlHelper> yearList = new List<CheckboxControlHelper>();
            years = years.Distinct().ToList();
            foreach(string year in years)
            {
                yearList.Add(new CheckboxControlHelper { Name = year, Checked = false });
            }
            return yearList;
        }
    }

}
