using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BExIS.Modules.Lui.UI.Models
{
    /// <summary>
    /// Class to store server information to access data via API
    /// 
    /// </summary>
    /// <returns></returns>
    public class ServerInformation
    {
        public string ServerName { get; set; }
        public string Token { get; set; }

    }

    /// <summary>
    /// Class to store dataset information receive via api
    /// 
    /// </summary>
    /// <returns></returns>
    public class DataStructureObject
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string inUse { get; set; }
        public string Structured { get; set; }
        public List<Variables> Variables { get; set; }
    }

    public class Variables
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string isOptional { get; set; }
        public string Unit { get; set; }
        public string DataType { get; set; }
        public string SystemType { get; set; }
        public string AttributeName { get; set; }
        public string AttributeDescription { get; set; }
    }

    /// <summary>
    /// Class to store dataset information receive via api
    /// 
    /// </summary>
    /// <returns></returns>
    public class DatasetObject
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string VersionId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string DataStructureId { get; set; }
        public string MetadataStructureId { get; set; }
        public AdditionalInformations AdditionalInformations { get; set; }
        public DatasetObject()
        {
            AdditionalInformations = new AdditionalInformations();
        }
    }



    /// <summary>
    /// Store AdditionalInformations for Dataset Object
    /// 
    /// </summary>
    /// <returns></returns>
    public class AdditionalInformations
    {
        public string Title { get; set; }

    }

    public enum DecimalCharacter
    {
        point,
        comma
    }

    public class DataApiModel
    {
        public long DatasetId { get; set; }
        public DecimalCharacter DecimalCharacter { get; set; }
        public string[] Columns { get; set; }
        public string[][] Data { get; set; }
    }
}
