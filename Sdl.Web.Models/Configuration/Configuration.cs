﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Script.Serialization;

namespace Sdl.Web.Mvc
{
    /// <summary>
    /// Needs refactoring to separate out the loading of config
    /// </summary>
    public static class Configuration
    {
        public static IStaticFileManager StaticFileManager { get; set; }
        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> _configuration;
        public static Dictionary<string, Localization> Localizations { get; set; }
        public const string DEFAULT_LOCALIZATION = "";
        private static object configLock = new object();
        public static string GetConfig(string key, string localization = null)
        {
            if (_configuration == null)
            {
                Load(HttpContext.Current.Server.MapPath("~"));
            }
            if (localization == null)
            {
                localization = WebRequestContext.Localization.Path;
            }
            if (_configuration.ContainsKey(localization))
            {
                var config = _configuration[localization];
                var bits = key.Split('.');
                if (bits.Length == 2)
                {
                    if (config.ContainsKey(bits[0]))
                    {
                        if (config[bits[0]].ContainsKey(bits[1]))
                        {
                            return config[bits[0]][bits[1]];
                        }
                        throw new Exception(String.Format("Configuration key {0} does not exist in section {1}", bits[1], bits[0]));
                    }
                    throw new Exception(String.Format("Configuration section {0} does not exist", bits[0]));
                }
                throw new Exception(String.Format("Configuration key {0} is in the wrong format. It should be in the format [section].[key], for example \"environment.cmsurl\"", key));
            }
            else
            {
                throw new Exception(String.Format("Configuration localization '{0}' does not exist.",localization));
            }
        }

        public static void Load(string applicationRoot)
        {
            var version = ConfigurationManager.AppSettings["Sdl.Web.SiteVersion"];
            if (version == null)
            {
                throw new Exception("Cannot find Sdl.Web.SiteVersion in application config appSettings.");
            }
            CheckOrCreateVersion(applicationRoot, version);
            lock (configLock)
            {
                _configuration = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
                foreach (var loc in Localizations.Values)
                {
                    if (!_configuration.ContainsKey(loc.Path))
                    {
                        var config = new Dictionary<string, Dictionary<string, string>>();
                        var path = String.Format("{0}{1}/system/config/_all.json", applicationRoot, loc.Path);
                        if (File.Exists(path))
                        {
                            var bootstrapJson = Json.Decode(File.ReadAllText(path));
                            foreach (string file in bootstrapJson.files)
                            {
                                var type = file.Substring(file.LastIndexOf("/") + 1);
                                type = type.Substring(0, type.LastIndexOf(".")).ToLower();
                                var configPath = applicationRoot + file;
                                if (File.Exists(configPath))
                                {
                                    config.Add(type, GetConfigFromFile(configPath));
                                }
                            }
                            _configuration.Add(loc.Path, config);
                        }
                    }
                }
                //Filter out localizations that were not found on disk, and add culture from config
                Dictionary<string, Localization> relevantLocalizations = new Dictionary<string, Localization>();
                foreach (var loc in Localizations)
                {
                    if (_configuration.ContainsKey(loc.Value.Path))
                    {
                        var config = _configuration[loc.Value.Path];
                        if (config.ContainsKey("site") && config["site"].ContainsKey("culture"))
                        {
                            loc.Value.Culture = config["site"]["culture"];
                        }
                        relevantLocalizations.Add(loc.Key, loc.Value);
                    }
                }
                Localizations = relevantLocalizations;
                //Update the localizations to contain the appropriate culture

            }            
        }

        private static void CheckOrCreateVersion(string applicationRoot, string version)
        {
            var tempDirSuffix = "_temp";
            List<string> processedLocations = new List<string>();
            foreach (var loc in Localizations.Values)
            {
                if (!processedLocations.Contains(loc.Path))
                {
                    var versionRoot = String.Format("{0}{1}/system/{2}", applicationRoot, loc.Path, version);
                    if (!Directory.Exists(versionRoot))
                    {
                        var tempVersionRoot = versionRoot + tempDirSuffix;
                        //Create a temp dir - when everything succeeds we copy files to main folder above, and rename this
                        var di = Directory.CreateDirectory(tempVersionRoot);
                        //Find bootstrap file(s) in broker DB and take it from there.
                        var url = loc.Path + "/system/_all.json";
                        SerializeFile(url, applicationRoot, version + tempDirSuffix, 2);
                        processedLocations.Add(loc.Path);
                        CleanAndCopyToParentDirectory(tempVersionRoot, version + tempDirSuffix);
                        Directory.Move(tempVersionRoot, versionRoot);
                    } 
                }
            }
        }

        private static void CleanAndCopyToParentDirectory(string tempVersionRoot, string tempDirName)
        {
            //Now clean up old and Create new directories
            foreach (string dirPath in Directory.GetDirectories(tempVersionRoot, "*",SearchOption.AllDirectories))
            {
                var newPath = dirPath.Replace("/" + tempDirName,"");
                if (Directory.Exists(newPath))
                {
                    try
                    {
                        Directory.Delete(newPath,true);
                    }
                    catch(Exception ex)
                    {
                        //TODO Log a warning, but don't stop processing
                    }
                }
                Directory.CreateDirectory(newPath);
            }
            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(tempVersionRoot, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace("/" + tempDirName, ""), true);
            } 
        }

        private static void SerializeFile(string url, string applicationRoot, string version, int bootstrapLevel = 0)
        {
            string fileContents = StaticFileManager.SerializeForVersion(url, applicationRoot, version, bootstrapLevel!=0);
            if (bootstrapLevel!=0)
            {
                var bootstrapJson = Json.Decode(fileContents);
                foreach (string file in bootstrapJson.files)
                {
                    SerializeFile(file, applicationRoot, version, bootstrapLevel - 1);
                }
            }
        }

        private static Dictionary<string, string> GetConfigFromFile(string file)
        {
            return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
        }
        public static string GetDefaultPageName()
        {
            return ConfigurationManager.AppSettings["Sdl.Web.DefaultPage"] ?? "index.html";
        }
        public static string GetDefaultExtension()
        {
            return ".html";
        }
        public static string GetRegionController()
        {
            return ConfigurationManager.AppSettings["Sdl.Web.RegionController"] ?? "Region";
        }
        public static string GetRegionAction()
        {
            return ConfigurationManager.AppSettings["Sdl.Web.RegionAction"] ?? "Region";
        }
        public static string GetCmsUrl()
        {
            return GetConfig("environment.cmsurl");
        }
        public static string GetVersion()
        {
            return ConfigurationManager.AppSettings["Sdl.Web.SiteVersion"];
        }

        public static void SetLocalizations(List<Dictionary<string, string>> localizations)
        {
            Localizations = new Dictionary<string, Localization>();
            foreach (var loc in localizations)
            {
                var localization = new Localization();
                localization.Protocol = !loc.ContainsKey("Protocol") ? "http" : loc["Protocol"];
                localization.Domain = !loc.ContainsKey("Domain") ? "no-domain-in-cd_link_conf" : loc["Domain"];
                localization.Port = !loc.ContainsKey("Port") ? "" : loc["Port"];
                localization.Path = (!loc.ContainsKey("Path") || loc["Path"] == "/") ? "" : loc["Path"];
                localization.LocalizationId = !loc.ContainsKey("LocalizationId") ? 0 : Int32.Parse(loc["LocalizationId"]);
                Localizations.Add(localization.GetBaseUrl(), localization);
            }
        }
    }
}
