// Copyright (c) Cingulara LLC 2019 and Tutela LLC 2019. All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007 license. See LICENSE file in the project root for full license information.

using System;
using openrmf_templates_api.Models;
using openrmf_templates_api.Data;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Xml;

namespace openrmf_templates_api.Classes
{
    public static class DefaultTemplateLoader
    {
        /// <summary>
        /// At startup, reads the directory included below and then one by one, loads the template CKL files
        /// and puts them into the database via a Template record that is a SYSTEM template.
        /// </summary>
        /// <returns>
        ///  No return.
        /// </returns>
        public static bool LoadTemplates() {
            // load the templates
            var path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/templatefiles/";
            // make sure there are files in here
            if (!Directory.Exists(path)) return false;
            // get the list of ******Manual-xccdf.xml files in here
            string[] filenames = Directory.GetFiles(path,"*xccdf.xml");
            string rawChecklist = "";
            Template t;
            Settings s = new Settings();
            s.ConnectionString = Environment.GetEnvironmentVariable("MONGODBCONNECTION");
            s.Database = Environment.GetEnvironmentVariable("MONGODB");
            TemplateRepository _templateRepo = new TemplateRepository(s);
            // remove old ones first
            if (filenames.Length > 0) _templateRepo.RemoveSystemTemplates().Wait();
            // cycle through the files
            try {
                foreach (string file in filenames) {
                    // read in the file
                    rawChecklist = File.ReadAllText(file);
                    rawChecklist = rawChecklist.Replace("\t","").Replace(">\n<","><");
                    try {
                        t = MakeTemplateSystemRecord(rawChecklist, file.Substring(file.LastIndexOf("/")+1));
                        // save them to the database
                        _templateRepo.AddTemplate(t).Wait();
                    }
                    catch (Exception tempEx) {
                        Console.WriteLine("Error parsing template file: {0}. {1}", file, tempEx.Message);
                    }
                }
            }
            catch (Exception ex) {
                // log the error with the ex.Message
                Console.WriteLine("Error parsing template files: {0}", ex.Message);
            }

            // return success
            return true;
        }

        /// <summary>
        /// Take the raw checklist and generate a System Template record to save into
        /// the database. This is called from the above routine when this API initially
        /// starts up.
        /// </summary>
        /// <param name="rawChecklist">The long XML string of the checklist</param>
        /// <param name="filename">The filename of the Manual xccdf this was generated from</param>
        /// <returns>
        ///  A template record with a GUID that is all 1's, type of SYSTEM template, and the 
        ///  rest of the Template data filled in to save as a database record.
        /// </returns>
        private static Template MakeTemplateSystemRecord(string rawChecklist, string filename) {
            Template newArtifact = new Template();
            newArtifact.created = DateTime.Now;
            // create the GUID of all 1's for the SYSTEM GUID
            newArtifact.createdBy = Guid.Parse("11111111-1111-1111-1111-111111111111");
            newArtifact.updatedOn = DateTime.Now;
            // make these SYSTEM types versus the default USER type
            newArtifact.templateType = "SYSTEM";
            newArtifact.filename = filename;
            newArtifact.description = "SYSTEM Template loaded by default by OpenRMF for SCAP Scans and Creation Wizard.";

            // parse the checklist and get the data needed
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawChecklist);
            
            string stigDescription = "";
            string stigGUID = Guid.NewGuid().ToString();

            // get the stigId from the Benchmark id tag
            XmlNodeList nodeListing = xmlDoc.GetElementsByTagName("Benchmark");
            foreach (XmlNode node in nodeListing) {
                if (node != null) {
                    XmlAttributeCollection attrs = node.Attributes;
                    foreach (XmlAttribute xml in attrs) {
                        if (!string.IsNullOrEmpty(xml.Name) && xml.Name == "id") {
                            newArtifact.stigId = xml.Value; // get the stigid for the checklist fields
                        }
                    }
                }
            }
            // get the title and STIG type
            nodeListing = xmlDoc.GetElementsByTagName("title");
            foreach (XmlNode node in nodeListing) {
                if (node.ParentNode != null && node.ParentNode.Name == "Benchmark") {
                    if (!string.IsNullOrEmpty(node.InnerText)) {
                        newArtifact.title = node.InnerText;
                        newArtifact.stigType = newArtifact.title.Replace("STIG", "Security Technical Implementation Guide").Replace("MS Windows","Windows")
                         .Replace("Microsoft Windows","Windows").Replace("Dot Net","DotNet");
                        break;
                    }
                }
            }

            // get the STIG description
            nodeListing = xmlDoc.GetElementsByTagName("description");
            foreach (XmlNode node in nodeListing) {
                if (node.ParentNode != null && node.ParentNode.Name == "Benchmark") {
                    if (!string.IsNullOrEmpty(node.InnerText)) {
                        stigDescription = node.InnerText;
                    }
                }
            }

            // get the stigDate for the status date
            nodeListing = xmlDoc.GetElementsByTagName("status");
            foreach (XmlNode node in nodeListing) {
                if (node != null) {
                    XmlAttributeCollection attrs = node.Attributes;
                    foreach (XmlAttribute xml in attrs) {
                        if (!string.IsNullOrEmpty(xml.Name) && xml.Name == "date") {
                            newArtifact.stigDate = xml.Value; // get the stigid for the checklist fields
                        }
                    }
                }
            }
            // get the release information
            nodeListing = xmlDoc.GetElementsByTagName("version");
            foreach (XmlNode node in nodeListing) {
                if (node.ParentNode != null && node.ParentNode.Name == "Benchmark") {
                    if (!string.IsNullOrEmpty(node.InnerText)) {
                        newArtifact.version = node.InnerText;
                        break;
                    }
                }
            }
            // get the release information
            nodeListing = xmlDoc.GetElementsByTagName("plain-text");
            foreach (XmlNode node in nodeListing) {
                if (node.ParentNode != null && node.ParentNode.Name == "Benchmark") {
                    if (!string.IsNullOrEmpty(node.InnerText)) {
                        newArtifact.stigRelease = node.InnerText;
                        break;
                    }
                }
            }

            // the dc:identifier equates to the asset target field
            nodeListing = xmlDoc.GetElementsByTagName("dc:identifier");
            foreach (XmlNode node in nodeListing) {
                if (!string.IsNullOrEmpty(node.InnerText)) {
                    newArtifact.CHECKLIST.ASSET.TARGET_KEY = node.InnerText;
                    break;
                }
            }
            newArtifact.CHECKLIST.ASSET.WEB_OR_DATABASE = "false";
            newArtifact.CHECKLIST.ASSET.ROLE = "None";
            newArtifact.CHECKLIST.ASSET.ASSET_TYPE = "Computing";

            // get all the STIG_INFO stuff for the main pieces of the checklist
            STIG_INFO mainStigInfo = new STIG_INFO();
            newArtifact.CHECKLIST.STIGS.iSTIG.STIG_INFO = mainStigInfo;
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "version", SID_DATA = newArtifact.version});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "classification", SID_DATA = "UNCLASSIFIED"});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "customname"});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "stigid", SID_DATA = newArtifact.stigId});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "description", SID_DATA = stigDescription});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "filename", SID_DATA = filename});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "releaseinfo", SID_DATA = newArtifact.stigRelease});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "title", SID_DATA = newArtifact.title});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "uuid", SID_DATA = stigGUID});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "notice", SID_DATA = "terms-of-use"});
            mainStigInfo.SI_DATA.Add(new SI_DATA() {SID_NAME = "source"});
            mainStigInfo = null;

            // here is the big one, get all the vulnerabilities from 1 to N and put them into the VULN listing
            VULN vulnitem = new VULN();
            Vulnerability vulnListing = new Vulnerability();
            string ruledescription = "";
            nodeListing = xmlDoc.GetElementsByTagName("Group");
            foreach (XmlNode node in nodeListing) {
                if (node != null) {
                    // get the Vulnerability Number
                    XmlAttributeCollection attrs = node.Attributes;
                    foreach (XmlAttribute xml in attrs) {
                        if (!string.IsNullOrEmpty(xml.Name) && xml.Name == "id") {
                            vulnListing.Vuln_Num = xml.Value;
                            break;
                        }
                    }
                    // get the rule information
                    foreach (XmlNode data in node.ChildNodes) {
                        if (data.Name == "title") {
                            vulnListing.Group_Title = data.InnerText;
                        } else if (data.Name == "Rule") {
                            // get all attributes
                            foreach (XmlAttribute xml in data.Attributes) {
                                if (!string.IsNullOrEmpty(xml.Name) && xml.Name == "id") {
                                    vulnListing.Rule_ID = xml.Value;
                                } else if (!string.IsNullOrEmpty(xml.Name) && xml.Name == "severity") {
                                    vulnListing.Severity = xml.Value;
                                } else if (!string.IsNullOrEmpty(xml.Name) && xml.Name == "weight") {
                                    vulnListing.Weight = xml.Value;
                                }
                            }

                            // cycle through the child nodes to get data from Rules also
                            foreach (XmlNode rule in data.ChildNodes) {
                                if (rule.Name == "version") {
                                    vulnListing.Rule_Ver = rule.InnerText;
                                } else if (rule.Name == "title") {
                                    vulnListing.Rule_Title = rule.InnerText.Trim();
                                } else if (rule.Name == "description") {
                                    // get the data and put the tags in it from the &lt and &gt text
                                    ruledescription = rule.InnerText.Replace("&lt;","<").Replace("&gt;",">");
                                    // now that you have the inside tags, you can chop up the data
                                    // VulnDiscussion, which always has data
                                    vulnListing.Vuln_Discuss = ruledescription.Substring(16, ruledescription.IndexOf("VulnDiscussion",16)-18);
                                    ruledescription = ruledescription.Substring(vulnListing.Vuln_Discuss.Length+33);
                                    // FalsePositives
                                    if (ruledescription.Substring(16, ruledescription.IndexOf("FalsePositives",16)-17) == "<")
                                        vulnListing.False_Positives = ""; // empty
                                    else {
                                        vulnListing.False_Positives = ruledescription.Substring(16, ruledescription.IndexOf("FalsePositives",16)-18);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.False_Positives.Length+33);
                                    // FalseNegatives
                                    if (ruledescription.Substring(16, ruledescription.IndexOf("FalseNegatives",16)-17) == "<")
                                        vulnListing.False_Negatives = ""; // empty
                                    else {
                                        vulnListing.False_Negatives = ruledescription.Substring(16, ruledescription.IndexOf("FalseNegatives",16)-18);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.False_Negatives.Length+33);
                                    // Documentable
                                    if (ruledescription.Substring(14, ruledescription.IndexOf("Documentable",14)-15) == "<")
                                        vulnListing.Documentable = ""; // empty
                                    else {
                                        vulnListing.Documentable = ruledescription.Substring(14, ruledescription.IndexOf("Documentable",14)-16);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.Documentable.Length+29);
                                    // Mitigations
                                    if (ruledescription.Substring(13, ruledescription.IndexOf("Mitigations",13)-14) == "<")
                                        vulnListing.Mitigations = ""; // empty
                                    else {
                                        vulnListing.Mitigations = ruledescription.Substring(13, ruledescription.IndexOf("Mitigations",13)-15);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.Mitigations.Length+27);
                                    // SeverityOverrideGuidance
                                    if (ruledescription.Substring(26, ruledescription.IndexOf("SeverityOverrideGuidance",26)-27) == "<")
                                        vulnListing.Security_Override_Guidance = ""; // empty
                                    else {
                                        vulnListing.Security_Override_Guidance = ruledescription.Substring(26, ruledescription.IndexOf("SeverityOverrideGuidance",26)-28);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.Security_Override_Guidance.Length+53);
                                    // PotentialImpacts
                                    if (ruledescription.Substring(18, ruledescription.IndexOf("PotentialImpacts",18)-19) == "<")
                                        vulnListing.Potential_Impact = ""; // empty
                                    else {
                                        vulnListing.Potential_Impact = ruledescription.Substring(18, ruledescription.IndexOf("PotentialImpacts",18)-20);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.Potential_Impact.Length+37);
                                    // ThirdPartyTools
                                    if (ruledescription.Substring(17, ruledescription.IndexOf("ThirdPartyTools",17)-18) == "<")
                                        vulnListing.Third_Party_Tools = ""; // empty
                                    else {
                                        vulnListing.Third_Party_Tools = ruledescription.Substring(17, ruledescription.IndexOf("ThirdPartyTools",17)-19);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.Third_Party_Tools.Length+35);
                                    // MitigationControl
                                    if (ruledescription.Substring(19, ruledescription.IndexOf("MitigationControl",19)-20) == "<")
                                        vulnListing.Mitigation_Control = ""; // empty
                                    else {
                                        vulnListing.Mitigation_Control = ruledescription.Substring(19, ruledescription.IndexOf("MitigationControl",19)-21);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.Mitigation_Control.Length+39);
                                    // Responsibility
                                    if (ruledescription.Substring(16, ruledescription.IndexOf("Responsibility",16)-17) == "<")
                                        vulnListing.Responsibility = ""; // empty
                                    else {
                                        vulnListing.Responsibility = ruledescription.Substring(16, ruledescription.IndexOf("Responsibility",16)-18);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.Responsibility.Length+33);
                                    // in case there is another Reponsibility, trim it and then move to IAControls
                                    ruledescription = ruledescription.Substring(ruledescription.IndexOf("<IAControls"));
                                    // IAControls
                                    if (ruledescription.Substring(12, ruledescription.IndexOf("IAControls",12)-13) == "<")
                                        vulnListing.IA_Controls = ""; // empty
                                    else {
                                        vulnListing.IA_Controls = ruledescription.Substring(12, ruledescription.IndexOf("IAControls",12)-14);
                                    }
                                    ruledescription = ruledescription.Substring(vulnListing.IA_Controls.Length+25);
                                } else if (rule.Name == "ident") {
                                    vulnListing.CCI_REF = rule.InnerText + "||"; // could be many of these CCI-xxx numbers
                                } else if (rule.Name == "fixtext") {
                                    vulnListing.Fix_Text = rule.InnerText;
                                } else if (rule.Name == "check") {
                                    foreach (XmlNode check in rule.ChildNodes){
                                        if (check.Name == "check-content") {
                                            // check content
                                            vulnListing.Check_Content = check.InnerText; // check text for manually checking the STIG
                                        } else if (check.Name == "check-content-ref") {
                                            // check content ref name
                                            foreach (XmlAttribute xml in check.Attributes) {
                                                if (!string.IsNullOrEmpty(xml.Name) && xml.Name == "name") {
                                                    vulnListing.Check_Content_Ref = xml.Value;
                                                }
                                            }
                                        }                           
                                    }
                                } // done checking the Rule child node, go see if there is more

                            } // end parsing inside child noted of the Rule tag; there are a lot in here we use!
                        } // end parsing inside the Rule tag
                    } // check if the group node is null
                } // end parsing w/in the Group tag, now process the data

                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Vuln_Num", ATTRIBUTE_DATA = vulnListing.Vuln_Num});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Severity", ATTRIBUTE_DATA = vulnListing.Severity});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Group_Title", ATTRIBUTE_DATA = vulnListing.Group_Title});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Rule_ID", ATTRIBUTE_DATA = vulnListing.Rule_ID});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Rule_Ver", ATTRIBUTE_DATA = vulnListing.Rule_Ver});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Rule_Title", ATTRIBUTE_DATA = vulnListing.Rule_Title});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Vuln_Discuss", ATTRIBUTE_DATA = vulnListing.Vuln_Discuss}); //.Replace("\r\n",Environment.NewLine).Replace("\"","'")
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "IA_Controls", ATTRIBUTE_DATA = vulnListing.IA_Controls});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Check_Content", ATTRIBUTE_DATA = vulnListing.Check_Content}); //.Replace("\r\n",Environment.NewLine).Replace("\"","'")
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Fix_Text", ATTRIBUTE_DATA = vulnListing.Fix_Text}); //.Replace("\r\n",Environment.NewLine).Replace("\"","'")
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "False_Positives", ATTRIBUTE_DATA = vulnListing.False_Positives});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "False_Negatives", ATTRIBUTE_DATA = vulnListing.False_Negatives});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Documentable", ATTRIBUTE_DATA = vulnListing.Documentable});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Mitigations", ATTRIBUTE_DATA = vulnListing.Mitigations});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Potential_Impact", ATTRIBUTE_DATA = vulnListing.Potential_Impact});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Third_Party_Tools", ATTRIBUTE_DATA = vulnListing.Third_Party_Tools});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Mitigation_Control", ATTRIBUTE_DATA = vulnListing.Mitigation_Control});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Responsibility", ATTRIBUTE_DATA = vulnListing.Responsibility});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Security_Override_Guidance", ATTRIBUTE_DATA = vulnListing.Security_Override_Guidance});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Check_Content_Ref", ATTRIBUTE_DATA = vulnListing.Check_Content_Ref});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Weight", ATTRIBUTE_DATA = vulnListing.Weight});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "Class", ATTRIBUTE_DATA = vulnListing.Class});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "STIGRef", ATTRIBUTE_DATA = newArtifact.title + " :: Version " + newArtifact.version + ", " + newArtifact.stigRelease});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "TargetKey", ATTRIBUTE_DATA = newArtifact.CHECKLIST.ASSET.TARGET_KEY});
                vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "STIG_UUID", ATTRIBUTE_DATA = stigGUID}); // use the same UUID throughout
                // there can be more than one in here; make sure we have at least one
                foreach(string str in vulnListing.CCI_REF.Split("||")) {
                    if (!string.IsNullOrEmpty(str) && str.Length > 3) // make sure it is valid
                        vulnitem.STIG_DATA.Add(new STIG_DATA() {VULN_ATTRIBUTE = "CCI_REF", ATTRIBUTE_DATA = str});
                }
                vulnitem.STATUS = "Not_Reviewed"; // by default
                vulnitem.COMMENTS = "";
                vulnitem.FINDING_DETAILS = "";
                vulnitem.SEVERITY_JUSTIFICATION = "";
                vulnitem.SEVERITY_OVERRIDE = "";

                // add the item to the CHECKLIST format for later serialization
                newArtifact.CHECKLIST.STIGS.iSTIG.VULN.Add(vulnitem);

                // reset the VULN item in the Checklist
                vulnitem = new VULN();
                // reset the temporary class to read all the variables into
                vulnListing = new Vulnerability();

            } // for each group item in the manual XCCDF XML file

            // take the class structure and stringify it for the rawChecklist
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(CHECKLIST));
            using(StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, newArtifact.CHECKLIST);
                newArtifact.rawChecklist = textWriter.ToString().Replace("\n","").Replace("\t","");
            }
            // get all the extra crap out of it that MS puts in there
            newArtifact.rawChecklist = newArtifact.rawChecklist.Substring(newArtifact.rawChecklist.IndexOf("<ASSET>"));
            newArtifact.rawChecklist = "<?xml version='1.0' encoding='UTF-8'?><!--DISA STIG Viewer :: 2.9.1--><CHECKLIST>" + 
                newArtifact.rawChecklist.Replace("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"","")
                .Replace("<CHECKLIST >","<CHECKLIST>")
                .Replace("<HOST_NAME />","<HOST_NAME></HOST_NAME>")
                .Replace("<HOST_IP />","<HOST_IP></HOST_IP>")
                .Replace("<HOST_MAC />","<HOST_MAC></HOST_MAC>")
                .Replace("<HOST_FQDN />","<HOST_FQDN></HOST_FQDN>")
                .Replace("<TECH_AREA />","<TECH_AREA></TECH_AREA>")
                .Replace("<WEB_DB_SITE />","<WEB_DB_SITE></WEB_DB_SITE>")
                .Replace("<WEB_DB_INSTANCE />","<WEB_DB_INSTANCE></WEB_DB_INSTANCE>");

            // nullify the CHECKLIST data
            newArtifact.CHECKLIST = new CHECKLIST();
            // Return the new record to save into the database
            return newArtifact;
        }
    }
}