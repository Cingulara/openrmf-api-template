using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using openrmf_templates_api.Models;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Xml;

namespace openrmf_templates_api.Classes
{
    public static class ChecklistLoader
    {
        /// <summary>
        /// Reads in the raw checklist file CKL and from that XML string, creates a C# class
        /// of all the data in the file by parsing it.
        /// </summary>
        /// <param name="rawChecklist">The long XML string of the checklist</param>
        /// <returns>
        ///  A CHECKLITS record which is a C# representation of the CKL XML file in class form.
        /// </returns>
        public static CHECKLIST LoadChecklist(string rawChecklist) {
            CHECKLIST myChecklist = new CHECKLIST();
            XmlSerializer serializer = new XmlSerializer(typeof(CHECKLIST));
            // sanitize it for JS
            rawChecklist = rawChecklist.Replace("\t","");
            XmlDocument xmlDoc = new XmlDocument();
            // load the doc into the XML structure
            xmlDoc.LoadXml(rawChecklist);
            // get the three main nodes we care about
            XmlNodeList assetList = xmlDoc.GetElementsByTagName("ASSET");
            XmlNodeList vulnList = xmlDoc.GetElementsByTagName("VULN");
            XmlNodeList stiginfoList = xmlDoc.GetElementsByTagName("STIG_INFO");
            // ensure all three are valid otherwise this XML is junk
            if (assetList != null && stiginfoList != null && vulnList != null) {
                // fill in the ASSET listing
                if (assetList.Count >= 1)
                    myChecklist.ASSET = getAssetListing(assetList.Item(0));
                // now get the STIG_INFO Listing
                if (stiginfoList.Count >= 1)
                    myChecklist.STIGS.iSTIG.STIG_INFO = getStigInfoListing(stiginfoList.Item(0));
                // now get the VULN listings until the end!
                if (vulnList.Count > 0) {
                    myChecklist.STIGS.iSTIG.VULN = getVulnerabilityListing(vulnList);
                }
            }            
            return myChecklist;
        }

        /// <summary>
        /// Take the ASSET XML node in here and parse to fill in the checklist.
        /// </summary>
        /// <param name="node">The XML node for the ASSET XML structure</param>
        /// <returns>
        /// The ASSET record matching the XML to the C# class structure for including
        /// into the larger CHECKLIST structure to use.
        /// </returns>
        private static ASSET getAssetListing(XmlNode node) {
            ASSET asset = new ASSET();
            foreach (XmlElement child in node.ChildNodes)
            {
                switch (child.Name) {
                    case "ROLE":
                        asset.ROLE = child.InnerText;
                        break;
                    case "ASSET_TYPE":
                        asset.ASSET_TYPE = child.InnerText;
                        break;
                    case "MARKING": 
                        asset.MARKING = child.InnerText;
                        break;
                    case "HOST_NAME":
                        asset.HOST_NAME = child.InnerText;
                        break;
                    case "HOST_IP":
                        asset.HOST_IP = child.InnerText;
                        break;
                    case "HOST_MAC":
                        asset.HOST_MAC = child.InnerText;
                        break;
                    case "HOST_FQDN":
                        asset.HOST_FQDN = child.InnerText;
                        break;
                    case "TECH_AREA":
                        asset.TECH_AREA = child.InnerText;
                        break;
                    case "TARGET_KEY":
                        asset.TARGET_KEY = child.InnerText;
                        break;
                    case "WEB_OR_DATABASE":
                        asset.WEB_OR_DATABASE = child.InnerText;
                        break;
                    case "WEB_DB_SITE":
                        asset.WEB_DB_SITE = child.InnerText;
                        break;
                    case "WEB_DB_INSTANCE":
                        asset.WEB_DB_INSTANCE = child.InnerText;
                        break;
                }
            }
            return asset;
        }

        /// <summary>
        /// Take the STIG_INFO XML node in here and parse to fill in the checklist.
        /// </summary>
        /// <param name="node">The XML node for the STIG_INFO XML structure</param>
        /// <returns>
        /// The STIG_INFO record matching the XML to the C# class structure for including
        /// into the larger CHECKLIST structure to use.
        /// </returns>
        private static STIG_INFO getStigInfoListing(XmlNode node) {
            STIG_INFO info = new STIG_INFO();
            SI_DATA data; // used for the name/value pairs

            // cycle through the children in STIG_INFO and get the SI_DATA
            foreach (XmlElement child in node.ChildNodes) {
                // get the SI_DATA record for SID_DATA and SID_NAME and then return them
                // each SI_DATA has 2
                data = new SI_DATA();                
                foreach (XmlElement siddata in child.ChildNodes) {
                    if (siddata.Name == "SID_NAME")
                        data.SID_NAME = siddata.InnerText;
                    else if (siddata.Name == "SID_DATA")
                        data.SID_DATA = siddata.InnerText;
                }
                info.SI_DATA.Add(data);
            }            
            return info;
        }
 
        /// <summary>
        /// Take the VULN XML nodes in here and parse to fill in the checklist. This 
        /// is the main meat of the checklist file as it has all the status and pieces
        /// of the checklist we care about.
        /// </summary>
        /// <param name="nodes">The XML nodes for the VULN XML structure</param>
        /// <returns>
        /// The VULN list of records matching the XML to the C# class structure for including
        /// into the larger CHECKLIST structure to use.
        /// </returns>
        private static List<VULN> getVulnerabilityListing(XmlNodeList nodes) {
            List<VULN> vulns = new List<VULN>();
            VULN vuln;
            STIG_DATA data;
            foreach (XmlNode node in nodes) {
                vuln = new VULN();
                if (node.ChildNodes.Count > 0) {
                    foreach (XmlElement child in node.ChildNodes) {
                        data = new STIG_DATA();
                        if (child.Name == "STIG_DATA") {
                            foreach (XmlElement stigdata in child.ChildNodes) {
                                if (stigdata.Name == "VULN_ATTRIBUTE")
                                    data.VULN_ATTRIBUTE = stigdata.InnerText;
                                else if (stigdata.Name == "ATTRIBUTE_DATA")
                                    data.ATTRIBUTE_DATA = stigdata.InnerText;
                            }
                            vuln.STIG_DATA.Add(data);
                        }
                        else {
                            // switch on the fields left over to fill them in the VULN class 
                            switch (child.Name) {
                                case "STATUS":
                                    vuln.STATUS = child.InnerText;
                                    break;
                                case "FINDING_DETAILS":
                                    vuln.FINDING_DETAILS = child.InnerText;
                                    break;
                                case "COMMENTS":
                                    vuln.COMMENTS = child.InnerText;
                                    break;
                                case "SEVERITY_OVERRIDE":
                                    vuln.SEVERITY_OVERRIDE = child.InnerText;
                                    break;
                                case "SEVERITY_JUSTIFICATION":
                                    vuln.SEVERITY_JUSTIFICATION = child.InnerText;
                                    break;
                            }
                        }
                    }
                }
                vulns.Add(vuln);
            }
            return vulns;
        }
    }
}