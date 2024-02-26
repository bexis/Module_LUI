﻿using BExIS.Modules.Lui.UI.Helper;
using BExIS.Modules.Lui.UI.Models;
using BExIS.Security.Services.Subjects;
using BExIS.Utils.Config;
using Microsoft.AspNet.Identity;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Vaiona.Utils.Cfg;
using Vaiona.Web.Extensions;
using Vaiona.Web.Mvc.Models;
using Vaiona.Web.Mvc.Modularity;

namespace BExIS.Modules.Lui.UI.Controllers
{
    public class LUIComponentCalculationController : Controller
    {
        // GET: LUIComponentCalculation
        public ActionResult Index()
        {
            ViewBag.Title = PresentationModel.GetViewTitleForTenant("LUI Component Calculation", this.Session.GetTenant());

            ComponentDataModel model = new ComponentDataModel();
            return View("ComponentCalculation", model);
        }

        public ActionResult CalculateCompontents()
        {
            Session["ComponentData"] = null;

            var settings = ModuleManager.GetModuleSettings("lui");

            string datasetId = settings.GetValueByKey("lui:lanuDataset").ToString();
            //get data structureId
            long structureId = long.Parse(DataAccess.GetDatasetInfo(datasetId, GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);

            DataTable lanuFullData = DataAccess.GetData(datasetId, structureId, GetServerInformation());

            //get only last year
            var lastYear = lanuFullData.AsEnumerable().Select(a => a.Field<DateTime>("Year")).Distinct().ToList().Max();
            //DataTable lanuData = lanuFullData.AsEnumerable()
            //                    .Where(r => r.Field<DateTime>("Year") == lastYear).CopyToDataTable();
            DataTable lanuData = lanuFullData.Copy();


            //get plottype infos
            string datasetIdPlots = settings.GetValueByKey("lui:epPlotsDataset").ToString();
            //get data structureId
            long structureIdPlots = long.Parse(DataAccess.GetDatasetInfo(datasetIdPlots, GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);
            DataTable plotTypes = DataAccess.GetData(datasetIdPlots, structureIdPlots, GetServerInformation());

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
            string filename = "Lanu_ComponentData_"+ DateTime.Now.ToString("yyyy-MM-dd");

            string dataPath = AppConfiguration.DataPath;
            string storePath = Path.Combine(dataPath, "LUI", "Temp", filename + ".csv");

            System.IO.File.WriteAllLines(storePath, lines);

            return File(storePath, MimeMapping.GetMimeMapping("ComponentData" + ".csv"), Path.GetFileName(storePath));
        }

        public ActionResult UploadSelectedRows(int[] rowIds)
        {
            string result = "";
            if (rowIds != null)
            {
                ComponentDataModel compData = Session["ComponentData"] as ComponentDataModel;

                //check for dublicates
                int[] duplicateIds = CheckDuplicates(compData.Data, rowIds);

                if (duplicateIds.Length == rowIds.Length)
                {
                    result = "No Upload: all selected rows are already uploaded!";
                }
                else
                {
                    //get duplicated ids for result text
                    //var dups = rowIds.Except(noDuplicateIds);
                    if (duplicateIds.Length > 0)
                    {
                        result += "Duplicated ids not uploaded: ";
                        foreach (int d in duplicateIds)
                        {
                            if(duplicateIds.Last() == d)
                                result += d.ToString() + " ";
                            else
                                result += d.ToString() + ", ";
                        }

                        result += "<br/>";
                    }


                    DataApiModel model = new DataApiModel();

                    var settings = ModuleManager.GetModuleSettings("lui");

                    model.DatasetId = Convert.ToInt64(settings.GetValueByKey("lui:datasetNewComponentsSet"));
                    model.DecimalCharacter = DecimalCharacter.point;

                    //get col names
                    List<string> cols = new List<string>();
                    foreach (DataColumn colum in compData.Data.Columns)
                    {
                        if (colum.ColumnName != "Id")
                            cols.Add(colum.ColumnName);
                    }

                    model.Columns = cols.ToArray();

                    string[,] dataArray = new string[rowIds.Count(), cols.Count];

                        List<string[]> dataArrays = new List<string[]>();

                        //get all not duplicated id
                        var idsToUpload = rowIds.Except(duplicateIds);
                        if(idsToUpload.Count() > 0)
                            result += "Uploaded Ids: ";

                        foreach (int id in idsToUpload)
                        {
                            DataTable copy = new DataTable();
                            copy = compData.Data.Copy();

                            DataRow row = copy.AsEnumerable().Where(a => a.Field<int>("Id") == id).FirstOrDefault();
                            row.Table.Columns.Remove("Id");

                            string[] stringArray = row.ItemArray.Cast<string>().ToArray();
                            dataArrays.Add(stringArray);

                            if (idsToUpload.Last() == id)
                                result += id.ToString() + " ";
                            else
                                result += id.ToString() + ", ";
                        }
                    result += "<br/>";

                        model.Data = dataArrays.ToArray();

                    //add primary key for update
                    string[] primaryKeys = new string[] { "Year", "EP_PlotID" };
                    model.PrimaryKeys = primaryKeys;
                    

                    //upload 
                    result += "API response: " + DataAccess.Upload(model, GetServerInformation()) + "<br/>";
                    
                }
            }
            else
            {
                result += "No Upload: no rows selected.";
            }

            return Content(result);
        }

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
        /// Get server information form json file in workspace
        /// </summary>
        /// <returns></returns>
        public  ServerInformation GetServerInformation()
        {
            //string filePath = Path.Combine(AppConfiguration.GetModuleWorkspacePath("LUI"), "Credentials.json");
            //string text = System.IO.File.ReadAllText(filePath);
            ServerInformation serverInformation = new ServerInformation();
            var uri = System.Web.HttpContext.Current.Request.Url;
            serverInformation.ServerName = uri.GetLeftPart(UriPartial.Authority) + "/";
            serverInformation.Token = GetUserToken();


            return serverInformation;
        }


        private int[] CheckDuplicates(DataTable newCompData, int[] rowIds)
        {
            var settings = ModuleManager.GetModuleSettings("lui");

            string luiIdNew = settings.GetValueByKey("lui:datasetNewComponentsSet").ToString();
            long structureId = long.Parse(DataAccess.GetDatasetInfo(luiIdNew, GetServerInformation()).DataStructureId, CultureInfo.InvariantCulture);

            DataTable allCompData = DataAccess.GetData(luiIdNew, structureId, GetServerInformation());

            List<int> duplicates = new List<int>();
            foreach (int id in rowIds)
            {
                var row = newCompData.AsEnumerable().Where(a => a.Field<int>("Id") == id).FirstOrDefault();
                var duplicate = allCompData.AsEnumerable().Where(c=>c.Field<string>("EP_PlotID") == row.Field<string>("EP_PlotID") && c.Field<DateTime>("Year").ToString("yyyy") == row.Field<string>(1)).FirstOrDefault();
                if (duplicate != null)
                 duplicates.Add(id);

                    

            }

            return duplicates.ToArray();
        }
    }
}