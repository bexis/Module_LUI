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
using BExIS.Utils.Config;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

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
        private static string FILE_NAMESPACE = ModuleManager.GetModuleSettings("lui").GetValueByKey("lui:filename:namespace") as string;
        #endregion

        // GET: Main
        public ActionResult Index()
        {
            if (checkPreconditions())
            {
                // set page title
                ViewBag.Title = PresentationModel.GetViewTitleForTenant(TITLE, this.Session.GetTenant());

                var settings = ModuleManager.GetModuleSettings("lui");

                //create model
                LUIQueryModel model = new LUIQueryModel();
                bool dataMissing = false;
                string datasetId = settings.GetValueByKey("lui:datasetDefaultComponentsSet").ToString();
                List<ApiDataStatisticModel> statisticModels = DataAccess.GetStatistic(datasetId, GetServerInformation());
                DataTable years = statisticModels.Where(a => a.VariableName == "Year").Select(c => c.uniqueValues).FirstOrDefault();
                foreach(DataRow dataRow in years.Rows)
                {
                    if (dataRow["count"].ToString() != "150")
                        dataMissing = true;
                }

                if (dataMissing)
                    model.MissingComponentData = DataAccess.GetMissingComponentData(GetServerInformation());
                else
                    model.MissingComponentData = new List<MissingComponentData>();

                //check if public access
                long.TryParse(this.User.Identity.GetUserId(), out long userId);

                //no bexis user == public access and id uncomplete data exsits
                if(userId == 0)
                {
                    model.IsPublicAccess = true;
                }

                model.DefaultComponentsSetDatasetId = settings.GetValueByKey("lui:datasetDefaultComponentsSet").ToString();
                var datasetInfo = DataAccess.GetDatasetInfo(model.DefaultComponentsSetDatasetId, GetServerInformation());
                model.DefaultComponentsSetDatasetVersion = DataAccess.GetDatasetInfo(model.DefaultComponentsSetDatasetId, GetServerInformation()).Version;
                XmlDocument doc =  DataAccess.GetMetadata(model.DefaultComponentsSetDatasetId, GetServerInformation());
                model.DefaultComponentsSetLastUpdate = DateTime.Parse(doc.GetElementsByTagName("metadataLastModificationDateType")[0].InnerText).ToString("yyyy-MM-dd");

                model.AvailableYearsDataDefault = GetAvailableYears(model.DefaultComponentsSetDatasetId, model.IsPublicAccess);
                model.AvailableYearsDataTill2019 = GetAvailableYears(settings.GetValueByKey("lui:datasetTill2019ComponentsSet").ToString(), model.IsPublicAccess);
                model.AvailableYearsDataTill2023 = GetAvailableYears(settings.GetValueByKey("lui:datasetTill2023ComponentsSet").ToString(), model.IsPublicAccess);


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

            var settings = ModuleManager.GetModuleSettings("lui");

            if (model.ComponentsSet.SelectedValue == "historic set till 2019")
            {
                selectedDataStructureId = int.Parse(settings.GetValueByKey("lui:datastructureTill2019ComponentsSet").ToString());
                model.DownloadDatasetId = settings.GetValueByKey("lui:datasetTill2019ComponentsSet").ToString();
            }
            else if(model.ComponentsSet.SelectedValue == "historic set till 2023")
            {
                selectedDataStructureId = int.Parse(settings.GetValueByKey("lui:datastructureTill2023ComponentsSet").ToString());
                model.DownloadDatasetId = settings.GetValueByKey("lui:datasetTill2023ComponentsSet").ToString();
            }
            else
            {
                selectedDataStructureId = int.Parse(settings.GetValueByKey("lui:datastructureDefaultComponentsSet").ToString());
                model.DownloadDatasetId = settings.GetValueByKey("lui:datasetDefaultComponentsSet").ToString();

            }

            Session["DataStructureId"] = selectedDataStructureId;

            // do the calucaltion
            // source data
            string dsId = "";
            switch (model.ComponentsSet.SelectedValue)
            {
                case "historic set till 2019":
                    dsId = settings.GetValueByKey("lui:datasetTill2019ComponentsSet").ToString();
                    break;
                case "historic set till 2023":
                    dsId = settings.GetValueByKey("lui:datasetTill2023ComponentsSet").ToString();
                    break;
                case "default components set":
                    dsId = settings.GetValueByKey("lui:datasetDefaultComponentsSet").ToString();
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
            var settings = ModuleManager.GetModuleSettings("lui");
            string filename = settings.GetValueByKey("lui:filename:download") as string;

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
            Session["ShowDataMetadata"] = null;
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

            //messsage for log
            string logMessage = "";

           
            //send mail
            var es = new EmailService();
            string user;

            LUIQueryModel model = (LUIQueryModel)Session["LUICalModel"];

            var settings = ModuleManager.GetModuleSettings("lui");

            string datasetId;
            if (model.ComponentsSet.SelectedValue == "historic set till 2019")
                datasetId = settings.GetValueByKey("lui:datasetTill2019ComponentsSet").ToString();

            else if (model.ComponentsSet.SelectedValue == "historic set till 2023")
                datasetId = settings.GetValueByKey("lui:datasetTill2023ComponentsSet").ToString();
            else
                datasetId = settings.GetValueByKey("lui:datasetDefaultComponentsSet").ToString();

            string version = DataAccess.GetDatasetInfo(datasetId, GetServerInformation()).Version;

            if (model.IsPublicAccess)
            {
                user = "public downloaded";
                logMessage = "LUI Calculation public download. Id: " + datasetId + ", Version: " + version + "";
            }
            else
            {
                logMessage = "LUI Calculation download. Id: " + datasetId + ", Version: " + version + "";
                using (UserManager userManager = new UserManager())
                {
                    user = "downloaded by " + userManager.FindByNameAsync(HttpContext.User.Identity.Name).Result.DisplayName;
                }
            }

            string text = "LUI Calculation file <b>\"" + Path.GetFileName(pathData) + "\"</b> with id <b>(" + datasetId + ")</b> version <b>(" + version + ")</b> was  <b>" + user + "</b>";
            es.Send("LUI data was downloaded (Id: " + datasetId + ", Version: " + version + ")", text, "bexis-sys@listserv.uni-jena.de");

            Vaiona.Logging.LoggerFactory.LogCustom(logMessage);


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
            var settings = ModuleManager.GetModuleSettings("lui");

            try
            {
                // check for LUI default dataset 
                string luiIdDefault = settings.GetValueByKey("lui:datasetDefaultComponentsSet").ToString();
                List<ApiDataStatisticModel> statisticsDefault = DataAccess.GetStatistic(luiIdDefault, GetServerInformation());
                var countDefault = statisticsDefault.Select(a => a.count).FirstOrDefault();
                if(countDefault == "0")
                    return exists == false;

                // check for LUI dataset till 2019
                string luiId2019 = settings.GetValueByKey("lui:datasetTill2019ComponentsSet").ToString();
                List<ApiDataStatisticModel> statistics2019 = DataAccess.GetStatistic(luiId2019, GetServerInformation());
                var count2019 = statistics2019.Select(a => a.count).FirstOrDefault();
                if (count2019 == "0")
                    return exists == false;

                // check for LUI dataset till 2023
                string luiId2023 = settings.GetValueByKey("lui:datasetTill2023ComponentsSet").ToString();
                List<ApiDataStatisticModel> statistics2023 = DataAccess.GetStatistic(luiId2023, GetServerInformation());
                var count2023 = statistics2023.Select(a => a.count).FirstOrDefault();
                if (count2023 == "0")
                    return exists == false;


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
            string jwt_token = "";
            try
            {
                using (var identityUserService = new IdentityUserService())
                using (var userManager = new UserManager())
                {
                    var jwtConfiguration = GeneralSettings.JwtConfiguration;

                    long userId = 0;
                    long.TryParse(this.User.Identity.GetUserId(), out userId);
                    var user = userManager.FindByIdAsync(userId).Result;
                    //var user = identityUserService.FindById(userId);
                  
                    if (user != null)
                    {
                        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfiguration.IssuerSigningKey));
                        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                        //Create a List of Claims, Keep claims name short
                        var permClaims = new List<Claim>
                        {
                            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                            new Claim(ClaimTypes.Name, user.UserName)
                        };

                        //Create Security Token object by giving required parameters
                        var token = new JwtSecurityToken(jwtConfiguration.ValidIssuer,
                        jwtConfiguration.ValidAudience,
                        permClaims,
                        notBefore: DateTime.Now,
                            expires: DateTime.Now.AddHours(100000000),
                            signingCredentials: credentials); ;

                         jwt_token = new JwtSecurityTokenHandler().WriteToken(token);  
                    }
                }
            }
            catch
            {

            }
            return jwt_token;

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

            List<ApiDataStatisticModel> statisticModels = DataAccess.GetStatistic(datasetId, GetServerInformation());
            DataTable yearsTable = statisticModels.Where(a => a.VariableName == "Year").Select(c => c.uniqueValues).FirstOrDefault();

            var inComYears = yearsTable.AsEnumerable().Where(a => a.Field<long>("count").ToString() != "150").Select(c=>c.Field<DateTime>("var").ToString("yyyy")).ToList();
            var years = yearsTable.AsEnumerable().Select(r => r.Field<DateTime>("var").ToString("yyyy")).ToList();

            if (isPublicAccess)
            {
                if (inComYears.Count > 0)
                {
                  years = years.Except(inComYears).ToList();
                }
            }

            List<CheckboxControlHelper> yearList = new List<CheckboxControlHelper>();
            foreach(string year in years)
            {
                yearList.Add(new CheckboxControlHelper { Name = year, Checked = false });
            }
            return yearList;
        }
    }

}
