// Copyright (c) Cingulara LLC 2019 and Tutela LLC 2019. All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using NATS.Client;
using openrmf_templates_api.Models;
using Newtonsoft.Json;
using System.Linq;

namespace openrmf_templates_api.Classes
{
    public static class NATSClient
    {        
        /// <summary>
        /// Return a single checklist based on the unique ID.
        /// </summary>
        /// <param name="systemGroupId">The system ID for the checklist to return.</param>
        /// <param name="artifactId">The checklist ID for the checklist to return.</param>
        /// <returns></returns>
        public static Artifact GetCurrentChecklist(string systemGroupId, string artifactId)
        {
            List<Artifact> arts = new List<Artifact>();
            
            // Create a new connection factory to create a connection.
            ConnectionFactory cf = new ConnectionFactory();
            // add the options for the server, reconnecting, and the handler events
            Options opts = ConnectionFactory.GetDefaultOptions();
            opts.MaxReconnect = -1;
            opts.ReconnectWait = 1000;
            opts.Name = "openrmf-api-compliance";
            opts.Url = Environment.GetEnvironmentVariable("NATSSERVERURL");
            opts.AsyncErrorEventHandler += (sender, events) =>
            {
                Console.WriteLine(string.Format("NATS client error. Server: {0}. Message: {1}. Subject: {2}", events.Conn.ConnectedUrl, events.Error, events.Subscription.Subject));
            };

            opts.ServerDiscoveredEventHandler += (sender, events) =>
            {
                Console.WriteLine(string.Format("A new server has joined the cluster: {0}", events.Conn.DiscoveredServers));
            };

            opts.ClosedEventHandler += (sender, events) =>
            {
                Console.WriteLine(string.Format("Connection Closed: {0}", events.Conn.ConnectedUrl));
            };

            opts.ReconnectedEventHandler += (sender, events) =>
            {
                Console.WriteLine(string.Format("Connection Reconnected: {0}", events.Conn.ConnectedUrl));
            };

            opts.DisconnectedEventHandler += (sender, events) =>
            {
                Console.WriteLine(string.Format("Connection Disconnected: {0}", events.Conn.ConnectedUrl));
            };
            
            // Creates a live connection to the NATS Server with the above options
            IConnection c = cf.CreateConnection(opts);

            // publish to get this list of Artifact checklists back via system
            Msg reply = c.Request("openrmf.system.checklists.read", Encoding.UTF8.GetBytes(systemGroupId), 30000); 
            c.Flush();
            // save the reply and get back the list of all checklists
            if (reply != null) {
                arts = JsonConvert.DeserializeObject<List<Artifact>>(Compression.DecompressString(Encoding.UTF8.GetString(reply.Data)));
                // let's make sure this checklist ID matches the system ID
                if (arts.Where(z => z.InternalId.ToString() == artifactId).FirstOrDefault() != null) { 
                    // it is in here so let's go get the actual full up checklist artifact
                    reply = c.Request("openrmf.checklist.read", Encoding.UTF8.GetBytes(arts.Where(z => z.InternalId.ToString() == artifactId).FirstOrDefault().InternalId.ToString()), 3000);
                    if (reply != null) {
                        Artifact art = JsonConvert.DeserializeObject<Artifact>(Compression.DecompressString(Encoding.UTF8.GetString(reply.Data)));
                        art.CHECKLIST = ChecklistLoader.LoadChecklist(art.rawChecklist);
                        c.Close();
                        return art;
                    }
                    else
                        return null;
                }
                else
                    return null;
            }
            else 
                return null;
        }
    }
}