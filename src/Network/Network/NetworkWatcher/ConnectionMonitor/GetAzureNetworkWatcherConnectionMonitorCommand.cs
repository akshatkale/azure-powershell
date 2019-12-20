﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using AutoMapper;
using Microsoft.Azure.Commands.Network.Models;
using Microsoft.Azure.Commands.ResourceManager.Common.ArgumentCompleters;
using Microsoft.Azure.Management.Internal.Resources.Utilities.Models;
using Microsoft.Azure.Management.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using MNM = Microsoft.Azure.Management.Network.Models;

namespace Microsoft.Azure.Commands.Network
{
    [Cmdlet("Get", ResourceManager.Common.AzureRMConstants.AzureRMPrefix + "NetworkWatcherConnectionMonitor", DefaultParameterSetName = "SetByName"), OutputType(typeof(PSConnectionMonitorResult))]
    public class GetAzureNetworkWatcherConnectionMonitorCommand : ConnectionMonitorBaseCmdlet
    {
        [Parameter(
             Mandatory = true,
             ValueFromPipeline = true,
             HelpMessage = "The network watcher resource.",
             ParameterSetName = "SetByResource")]
        [ValidateNotNull]
        public PSNetworkWatcher NetworkWatcher { get; set; }

        [Parameter(
            Mandatory = true,
            HelpMessage = "The name of network watcher.",
            ParameterSetName = "SetByName")]
        [ResourceNameCompleter("Microsoft.Network/networkWatchers", "ResourceGroupName")]
        [ValidateNotNullOrEmpty]
        public string NetworkWatcherName { get; set; }

        [Parameter(
            Mandatory = true,
            HelpMessage = "The name of the network watcher resource group.",
            ParameterSetName = "SetByName")]
        [ResourceGroupCompleter]
        [ValidateNotNullOrEmpty]
        public string ResourceGroupName { get; set; }

        [Parameter(
            Mandatory = true,
            HelpMessage = "Location of the network watcher.",
            ParameterSetName = "SetByLocation")]
        [LocationCompleter("Microsoft.Network/networkWatchers/connectionMonitors")]
        [ValidateNotNull]
        public string Location { get; set; }

        [Parameter(
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Resource ID.",
            ParameterSetName = "SetByResourceId")]
        [ValidateNotNull]
        public string ResourceId { get; set; }

        [Alias("ConnectionMonitorName")]
        [Parameter(
            Mandatory = false,
            HelpMessage = "The connection monitor name.",
            ParameterSetName = "SetByName")]
        [Parameter(
            Mandatory = false,
            HelpMessage = "The connection monitor name.",
            ParameterSetName = "SetByResource")]
        [Parameter(
            Mandatory = false,
            HelpMessage = "The connection monitor name.",
            ParameterSetName = "SetByLocation")]
        [ResourceNameCompleter("Microsoft.Network/networkWatchers/connectionMonitors", "ResourceGroupName", "NetworkWatcherName")]
        [ValidateNotNullOrEmpty]
        [SupportsWildcards]
        public string Name { get; set; }

        public override void Execute()
        {
            base.Execute();

            string connectionMonitorName = this.Name;
            string resourceGroupName = this.ResourceGroupName;
            string networkWatcherName = this.NetworkWatcherName;

            if (ParameterSetName.Contains("SetByResourceId"))
            {
                ConnectionMonitorDetails connectionMonitorDetails = new ConnectionMonitorDetails();
                connectionMonitorDetails = this.GetConnectionMonitorDetails(this.ResourceId);

                connectionMonitorName = connectionMonitorDetails.ConnectionMonitorName;
                resourceGroupName = connectionMonitorDetails.ResourceGroupName;
                networkWatcherName = connectionMonitorDetails.NetworkWatcherName;
            }
            else if (ParameterSetName.Contains("SetByResource"))
            {
                resourceGroupName = this.NetworkWatcher.ResourceGroupName;
                networkWatcherName = this.NetworkWatcher.Name;
            }
            else if (ParameterSetName.Contains("SetByLocation"))
            {
                var networkWatcher = this.GetNetworkWatcherByLocation(this.Location);
                
                if (networkWatcher == null)
                {
                    throw new ArgumentException("There is no network watcher in location {0}", this.Location);
                }

                resourceGroupName = NetworkBaseCmdlet.GetResourceGroup(networkWatcher.Id);
                networkWatcherName = networkWatcher.Name;
            }

            if (ShouldGetByName(resourceGroupName, connectionMonitorName))
            {
                PSConnectionMonitorResult connectionMonitor = new PSConnectionMonitorResult();
                connectionMonitor = this.GetConnectionMonitor(resourceGroupName, networkWatcherName, connectionMonitorName);

                WriteObject(connectionMonitor);
            }
            else
            {
                List<PSConnectionMonitorResult> psConnectionMonitorList = new List<PSConnectionMonitorResult>();
                var connectionMonitorList = this.ConnectionMonitors.List(resourceGroupName, networkWatcherName);

                foreach (var cm in connectionMonitorList)
                {
                    PSConnectionMonitorResult psConnectionMonitor = NetworkResourceManagerProfile.Mapper.Map<PSConnectionMonitorResult>(cm);
                    psConnectionMonitorList.Add(psConnectionMonitor);
                }

                // This is manual conversion from V2 to V1
                foreach (PSConnectionMonitorResult ConnectionMonitorResult in psConnectionMonitorList)
                {
                    if (String.Compare(ConnectionMonitorResult.ConnectionMonitorType, "SingleSourceDestination", true) == 0)
                    {
                        //convert V2 to V1
                        ConnectionMonitorResult.Source.ResourceId = ConnectionMonitorResult.TestGroup[0]?.Sources[0]?.ResourceId;
                        // getConnectionMonitor.Source.Port

                        ConnectionMonitorResult.Destination.ResourceId = ConnectionMonitorResult.TestGroup[0]?.Destinations[0]?.ResourceId;
                        ConnectionMonitorResult.Destination.Address = ConnectionMonitorResult.TestGroup[0]?.Destinations[0]?.Address;
                        ConnectionMonitorResult.Destination.Port = ConnectionMonitorResult.TestConfiguration[0]?.TcpConfiguration?.Port ?? default(int);
                        ConnectionMonitorResult.MonitoringIntervalInSeconds = ConnectionMonitorResult.TestConfiguration[0]?.TestFrequencySec;
                        
                        // These parameters do not need mapping 
                        // ConnectionMonitorResult.AutoStart = false;
                        // getConnectionMonitor.StartTime
                        // getConnectionMonitor.MonitoringStatus
                    }
                }

                WriteObject(SubResourceWildcardFilter(Name, psConnectionMonitorList), true);
            }
        }
    }
}
