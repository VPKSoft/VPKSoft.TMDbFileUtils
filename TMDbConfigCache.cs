﻿#region License
/*
VPKSoft.TMDbFileUtils

A library to help enumerate both movie files and directories containing TV show seasons and the run them through the TMBb API.
Copyright © 2018 VPKSoft, Petteri Kautonen

Contact: vpksoft@vpksoft.net

This file is part of VPKSoft.TMDbFileUtils.

VPKSoft.TMDbFileUtils is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

VPKSoft.TMDbFileUtils is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with VPKSoft.TMDbFileUtils.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion

using System;
using System.IO;
using System.Xml;
using TMdbEasy;
using TMdbEasy.ApiInterfaces;
using TMdbEasy.TmdbObjects.Configuration;
using VPKSoft.Utils;

namespace VPKSoft.TMDbFileUtils
{
    /// <summary>
    /// A helper class to store a TMDb configuration to a XML file, so it doesn't need to be requested all the time a software starts executing.
    /// </summary>
    public static class TMDbConfigCache
    {
        // the TMdbEasy.ApiInterfaces.IConfigApi interface.. 
        private static IConfigApi configApi;

        /// <summary>
        /// Gets the configuration path for Windows.Forms application.
        /// </summary>
        /// <value>
        /// The configuration path for Windows.Forms application
        /// </value>
        public static string ConfigPathWinforms
        {
            get
            {
                // first ensure that the path exists..
                Paths.MakeAppSettingsFolder(Misc.AppType.Winforms);

                // return the path..
                return Paths.GetAppSettingsFolder(Misc.AppType.Winforms);
            }
        }

        /// <summary>
        /// Gets the configuration path for a WPF application.
        /// </summary>
        /// <value>
        /// The configuration path for a WPF application
        /// </value>
        public static string ConfigPathWPF
        {
            get
            {
                // first ensure that the path exists..
                Paths.MakeAppSettingsFolder(Misc.AppType.WPF);

                // return the path..
                return Paths.GetAppSettingsFolder(Misc.AppType.WPF);
            }
        }

        /// <summary>
        /// Gets the TMDb API configuration from the TMdbEasy client.
        /// </summary>
        /// <param name="easy">An instance to a TMdbEasy client.</param>
        /// <param name="configFileName">A name of the configuration file.</param>
        /// <returns>A Configurations class instance from the TMdbEasy client class instance.</returns>
        private static Configurations GetApiConfig(EasyClient easy, string configFileName)
        {
            configApi = easy.GetApi<IConfigApi>().Value; // get the API..

            // get the Configurations class instance synchronously.. 
            Configurations configurations = configApi.GetConfigurationAsync().Result;

            // serialize the Configurations class instance to a XML document..
            XmlDocument xmlDocument = ObjectSerialization.ToXmlDocument(configurations);

            // save the XML document..
            xmlDocument.Save(configFileName);

            // return the Configurations class instance..
            return configurations;
        }

        /// <summary>
        /// Gets the TMDb API configuration either from the TMdbEasy client or 
        /// deserialized from a XML document if the document is newer than 7 days old and the XML document exists.
        /// </summary>
        /// <param name="easy">An instance to a TMdbEasy client.</param>
        /// <param name="configPath">The configuration path.</param>
        /// <returns>A Configurations class instance.</returns>
        public static Configurations GetConfigurations(EasyClient easy, string configPath)
        {
            // construct a XML file name for serialization / deserialization..
            string configFileName = Path.Combine(configPath, "TMDbConfig.xml");
            if (File.Exists(configFileName)) // if the XML file exists..
            {
                // ..read the XML file's information..
                FileInfo fileInfo = new FileInfo(configFileName);

                // if the file is newer than 7 days..
                if ((DateTime.Now - fileInfo.LastWriteTimeUtc).TotalDays < 7)
                {
                    // create an instance of a XmlDocument class..
                    XmlDocument xmlDocument = new XmlDocument();

                    // load the XML file into the XmlDocument class instance..
                    xmlDocument.Load(configFileName);

                    // deserialize the XML document to a Configurations class instance and return it..
                    return (Configurations)ObjectSerialization.DeserializeObject(typeof(Configurations), xmlDocument);
                }
                else // too old XML file..
                {
                    // ..so re-get the configuration and save it to a XML file and return a Configurations class instance..
                    return GetApiConfig(easy, configFileName);
                }
            }
            else // file doesn't exist..
            {
                // ..so get the configuration and save it to a XML file and return a Configurations class instance..
                return GetApiConfig(easy, configFileName);
            }
        }
    }
}
