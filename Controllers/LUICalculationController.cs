using BExIS.Modules.Lui.UI.Models;
using System.Web.Mvc;
using Vaiona.Web.Mvc.Models;
using Vaiona.Web.Extensions;
using System.Collections.Generic;
using System;
using System.Data;
using System.Linq;
using System.IO;
using BExIS.IO.Transform.Output;
using BExIS.Dlm.Services.Data;
using BExIS.Dlm.Services.DataStructure;
using System.Web.Routing;
using Vaiona.Web.Mvc.Modularity;
using Vaiona.Utils.Cfg;
using System.Web;
using BExIS.Modules.Lui.UI.Helper;
using BExIS.Security.Services.Utilities;
using BExIS.Security.Services.Subjects;

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

        public int selectedDatasetId = 0;
        //public int selectedDataStructureId = 0;

        // GET: Main
        public ActionResult Index()
        {
            if( checkPreconditions() )
            {

                // set page title
                ViewBag.Title = PresentationModel.GetViewTitleForTenant(TITLE, this.Session.GetTenant());

                // show the view
                LUIQueryModel model = new LUIQueryModel();
                return View("Index", model);

            } else
            {

                // preconditions failed, show error page
                return View("Error");

            }
        }

        public ActionResult ShowPrimaryData(long datasetID)
        {
            long versionId = 0;
            using (var datasetManager = new DatasetManager())
            {
                versionId = datasetManager.GetDatasetLatestVersion(datasetID).Id;
            }
                var view = this.Render("DDM", "Data", "ShowPrimaryData", new RouteValueDictionary()
            {
                { "datasetID", datasetID },
                { "versionId", versionId }
            });

            return Content(view.ToHtmlString(), "text/html");
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
                selectedDataStructureId = (int)Models.Settings.get("lui:datastructureOldComponentsSet");
            else
                selectedDataStructureId = (int)Models.Settings.get("lui:datastructureNewComponentsSet");

            Session["DataStructureId"] = selectedDataStructureId;

            // do the calucaltion
            var results = CalculateLui.DoCalc(model);

            // store results in session
            Session[SESSION_TABLE] = results;
            if (null != Session[SESSION_FILE])
            {
                ((Dictionary<string, string>)Session[SESSION_FILE]).Clear();
            }
            Session["LUICalModel"] = model;

            return PartialView("_results", results);
        }

        
        /// <summary>
        /// prepare the serialized data file for download
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        public ActionResult PrepareDownloadFile(string mimeType)
        {

            // if we have already a matching file cached, we can short circuit here
            if ((null != Session[SESSION_FILE]) && ((Dictionary<string, string>)Session[SESSION_FILE]).ContainsKey(mimeType))
            {
                return Json(new { error = false, mimeType = mimeType }, JsonRequestBehavior.AllowGet);
            }
            long selectedDataStructureId = 0;
            if (Session["DataStructureId"] != null)
                selectedDataStructureId = (long)Session["DataStructureId"];

            // helper class
            DownloadManager downloadManager = new DownloadManager();

            // filename
            // use unix timestamp to make filenames unique
            string filename = Models.Settings.get("lui:filename:download") as string;
            LUIQueryModel model  = (LUIQueryModel)Session["LUICalModel"];
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
                    path = downloadManager.GenerateAsciiFile(FILE_NAMESPACE, Session[SESSION_TABLE] as DataTable, filename, mimeType);
                    break;

                case "application/vnd.ms-excel.sheet.macroEnabled.12":
                case "application/vnd.ms-excel":
                    //path = outputDataManager.GenerateExcelFile(FILE_NAMESPACE, Session[SESSION_TABLE] as DataTable, filename, selectedDataStructureId);
                    break;

                default:
                    Response.StatusCode = 420;
                    return Json(new { error = true, msg = "Unknown file-type: " + mimeType }, JsonRequestBehavior.AllowGet);
            }

            // store path in session for further download
            if (null == Session[SESSION_FILE])
            {
                Session[SESSION_FILE] = new Dictionary<string, string>();
            }
            ((Dictionary<string, string>)Session[SESSION_FILE])[mimeType] = path;

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

            // get file path
            string path = ((Dictionary<string, string>)Session[SESSION_FILE])[mimeType];

            //send mail
            
            var es = new EmailService();
            string user;
            using (UserManager userManager = new UserManager())
            {
               user =  userManager.FindByNameAsync(HttpContext.User.Identity.Name).Result.DisplayName;
            }
            LUIQueryModel model = (LUIQueryModel)Session["LUICalModel"];
            long datasetId;
            if(model.ComponentsSet.SelectedValue.Contains("old"))
                datasetId = Convert.ToInt64(Models.Settings.get("lui:datasetOldComponentsSet"));
            else
                datasetId = Convert.ToInt64(Models.Settings.get("lui:datasetNewComponentsSet"));
            int version;
            using (DatasetManager datasetManager = new DatasetManager())
            {
                version = datasetManager.GetDataset(datasetId).VersionNo;        
            }

            string text = "LUI Calculation file <b>\"" + Path.GetFileName(path) + "\"</b> with id <b>("+ datasetId + ")</b> version <b>("+ version + ")</b> was downloaded by <b>" + user + "</b>";
            es.Send("LUI data was downloaded (Id: " + datasetId + ", Version: "+ version +")" , text, "bexis-sys@listserv.uni-jena.de");


            // return file for download
            return File(path, mimeType, Path.GetFileName(path));
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
            using (DatasetManager dm = new DatasetManager())
            using (DataStructureManager dsm = new DataStructureManager())
            {
                int luiIdNew = (int)Models.Settings.get("lui:datasetNewComponentsSet");
                bool exists = dm.DatasetRepo.Query()
                                            .Where(x => x.Id == luiIdNew)
                                            .Any();
                if (!exists)
                {
                    return false;
                }

                // check for export data structure
                int dsdId = (int)Models.Settings.get("lui:datastructureNewComponentsSet");
                exists = dsm.StructuredDataStructureRepo.Query()
                                        .Where(x => x.Id == dsdId)
                                        .Any();
                if (!exists)
                {
                    return false;
                }

                // check for LUI old dataset
                int luiIdOld = (int)Models.Settings.get("lui:datasetOldComponentsSet");
                exists = dm.DatasetRepo.Query()
                                            .Where(x => x.Id == luiIdOld)
                                            .Any();
                if (!exists)
                {
                    return false;
                }

                // check for export data structure

                int dsdIdOld = (int)Models.Settings.get("lui:datastructureOldComponentsSet");
                exists = dsm.StructuredDataStructureRepo.Query()
                                        .Where(x => x.Id == dsdIdOld)
                                        .Any();
                if (!exists)
                {
                    return false;
                }

                // if we came that far, all conditions are met
                return true;
            }
        }
    }
}