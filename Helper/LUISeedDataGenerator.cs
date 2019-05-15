using BExIS.Security.Entities.Objects;
using BExIS.Security.Services.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Vaiona.Web.Mvc.Modularity;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class LUISeedDataGenerator : IModuleSeedDataGenerator
    {

        public void GenerateSeedData()
        {
            FeatureManager featureManager = new FeatureManager();
            OperationManager operationManager = new OperationManager();

            try
            {
                Feature rootDataToolsFeature = featureManager.FeatureRepository.Get().FirstOrDefault(f => f.Name.Equals("Data Tools"));
                if (rootDataToolsFeature == null) rootDataToolsFeature = featureManager.Create("Data Tools", "Data Tools");

                Feature luiFeature = featureManager.FeatureRepository.Get().FirstOrDefault(f => f.Name.Equals("Data Tools"));
                if (luiFeature == null) luiFeature = featureManager.Create("LUI Tool", "LUI Tool", rootDataToolsFeature);


            }
            catch
            {
            }


        }


        public void Dispose()
        {
            // nothing to do for now...
        }





        #region operation definition struct
        private class OperationStruct
        {
            public string module;
            public string controller;
            public string action;
        }
        #endregion

        #region settings
        private static string featureRoot = "Data Tools";
        private static string luiFeature = "LUI";
        private static OperationStruct luiOperation = new OperationStruct { module = "LUI", controller = "Main", action = "*" };
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public static void CreateFeatures()
        {

            // get managers
            FeatureManager featureManager = new FeatureManager();
            OperationManager operationManager = new OperationManager();

            // make sure we have a DataTools section
            Feature root = featureManager.FindByName(featureRoot) ?? featureManager.Create(featureRoot, featureRoot);

            // make sure there is a LUI entry there
            Feature luiEntry = featureManager.FindByName(luiFeature) ?? featureManager.Create(luiFeature, luiFeature, root);
            
            // add actual permissions
            Operation luiOp = operationManager.Find(luiOperation.module, luiOperation.controller, luiOperation.action);
            if( luiOp != null ) {
                // there is already something there, so make sure it points to the correct feature
                luiOp.Feature = luiEntry;
                operationManager.Update(luiOp);
            } else
            {
                // nothing there, so create it
                luiOp = operationManager.Create(luiOperation.module, luiOperation.controller, luiOperation.action, luiEntry);
            }

        }

    }

}