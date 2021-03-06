#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.XPath;
using MediaPortal.Utilities.Exceptions;
using UPnP.Infrastructure.CP.SSDP;
using UPnP.Infrastructure.Utils;

namespace UPnP.Infrastructure.CP
{
  /// <summary>
  /// Tracks UPnP devices which are available in the network. Provides materialized descriptions for each available
  /// device and service. Provides an event <see cref="RootDeviceAdded"/> which gets fired when all description documents
  /// were fetched from the device's server. Provides an event <see cref="RootDeviceRemoved"/> which gets fired when
  /// the device disappears. SSDP events for device configuration changes are completely hidden by this class and mapped
  /// to calls to <see cref="RootDeviceRemoved"/> and <see cref="RootDeviceAdded"/>.
  /// </summary>
  public class UPnPNetworkTracker : IDisposable
  {
    /// <summary>
    /// Delegate to be called when a network device was added and its given <paramref name="rootDescriptor"/> was filled
    /// completely.
    /// </summary>
    /// <param name="rootDescriptor">Descriptor containing all description documents of the new UPnP device.</param>
    public delegate void DeviceAddedDlgt(RootDescriptor rootDescriptor);

    /// <summary>
    /// Delegate to be called when a network device was removed.
    /// </summary>
    /// <param name="rootDescriptor">Descriptor of the UPnP device which was removed.</param>
    public delegate void DeviceRemovedDlgt(RootDescriptor rootDescriptor);

    /// <summary>
    /// Delegate to be called when a reboot of a network device was detected.
    /// </summary>
    /// <param name="rootDescriptor">Descriptor of the UPnP device which was rebooted.</param>
    public delegate void DeviceRebootedDlgt(RootDescriptor rootDescriptor);

    protected class DescriptionRequestState
    {
      protected RootDescriptor _rootDescriptor;
      protected HttpWebRequest _httpWebRequest;
      protected IDictionary<ServiceDescriptor, string> _pendingServiceDescriptions =
          new Dictionary<ServiceDescriptor, string>();
      protected ServiceDescriptor _currentServiceDescriptor = null;

      public DescriptionRequestState(RootDescriptor rootDescriptor, HttpWebRequest httpWebRequest)
      {
        _rootDescriptor = rootDescriptor;
        _httpWebRequest = httpWebRequest;
      }

      public RootDescriptor RootDescriptor
      {
        get { return _rootDescriptor; }
      }

      public HttpWebRequest Request
      {
        get { return _httpWebRequest; }
        set { _httpWebRequest = value; }
      }

      public ServiceDescriptor CurrentServiceDescriptor
      {
        get { return _currentServiceDescriptor; }
        set { _currentServiceDescriptor = value; }
      }

      public IDictionary<ServiceDescriptor, string> PendingServiceDescriptions
      {
        get { return _pendingServiceDescriptions; }
      }
    }

    /// <summary>
    /// Timeout for a pending request for a description document in seconds.
    /// </summary>
    protected const int PENDING_REQUEST_TIMEOUT = 30;

    protected const string KEY_ROOT_DESCRIPTOR = "ROOT-DESCRIPTOR";

    protected ICollection<DescriptionRequestState> _pendingRequests = new List<DescriptionRequestState>();
    protected bool _active = false;
    protected CPData _cpData;

    #region Ctor

    /// <summary>
    /// Creates a new UPnP network tracker instance.
    /// </summary>
    /// <param name="cpData">Shared control point data instance.</param>
    public UPnPNetworkTracker(CPData cpData)
    {
      _cpData = cpData;
    }

    public void Dispose()
    {
      Close();
    }

    #endregion

    #region Public members

    /// <summary>
    /// Gets called when a new device appeared at the network. When this event is called, all description documents from the
    /// sub devices have already been loaded.
    /// </summary>
    public event DeviceAddedDlgt RootDeviceAdded;

    /// <summary>
    /// Gets called when a device disappeared from the network. Will be called when the device explicitly cancelled its
    /// advertisement as well as when its advertisements expired.
    /// </summary>
    public event DeviceRemovedDlgt RootDeviceRemoved;

    /// <summary>
    /// Gets called when the SSDP subsystem detects a reboot of one of our known devices.
    /// </summary>
    public event DeviceRebootedDlgt DeviceRebooted;

    /// <summary>
    /// Returns the information whether this UPnP network tracker is active, i.e. the network listener is active and this
    /// instance is tracking network devices.
    /// </summary>
    public bool IsActive
    {
      get { return _active; }
    }

    /// <summary>
    /// Returns a mapping of root device UUIDs to descriptors containing the information about that device for all known
    /// UPnP network devices. Returns <c>null</c> if this network tracker isn't active.
    /// </summary>
    public IDictionary<string, RootDescriptor> KnownRootDevices
    {
      get
      {
        lock (_cpData.SyncObj)
        {
          if (!_active)
            return null;
          IDictionary<string, RootDescriptor> result = new Dictionary<string, RootDescriptor>();
          foreach (RootEntry entry in _cpData.SSDPController.RootEntries)
          {
            RootDescriptor rd = GetRootDescriptor(entry);
            if (rd == null)
              continue;
            result.Add(rd.SSDPRootEntry.RootDeviceID, rd);
          }
          return result;
        }
      }
    }

    /// <summary>
    /// Data which is shared among all components of the control point system.
    /// </summary>
    public CPData SharedControlPointData
    {
      get { return _cpData; }
    }

    /// <summary>
    /// Starts this UPnP network tracker. After the tracker is started, its <see cref="RootDeviceAdded"/> and
    /// <see cref="RootDeviceRemoved"/> events will begin to be raised when UPnP devices become available at the network of when
    /// UPnP devices disappear from the network.
    /// </summary>
    public void Start()
    {
      lock (_cpData.SyncObj)
      {
        if (_active)
          throw new IllegalCallException("UPnPNetworkTracker is already active");
        _active = true;
        SSDPClientController ssdpController = new SSDPClientController(_cpData);
        ssdpController.RootDeviceAdded += OnSSDPRootDeviceAdded;
        ssdpController.RootDeviceRemoved += OnSSDPRootDeviceRemoved;
        ssdpController.DeviceRebooted += OnSSDPDeviceRebooted;
        ssdpController.DeviceConfigurationChanged += OnSSDPDeviceConfigurationChanged;
        _cpData.SSDPController = ssdpController;
        ssdpController.Start();
        ssdpController.SearchAll(null);
      }
    }

    /// <summary>
    /// Stops this UPnP network tracker's activity, i.e. closes the UPnP network listener and clears all
    /// <see cref="KnownRootDevices"/>.
    /// </summary>
    public void Close()
    {
      lock (_cpData.SyncObj)
      {
        if (!_active)
          return;
        _active = false;
        foreach (RootEntry entry in _cpData.SSDPController.RootEntries)
        {
          RootDescriptor rd = GetRootDescriptor(entry);
          if (rd == null)
            continue;
          InvalidateDescriptor(rd);
        }
        SSDPClientController ssdpController = _cpData.SSDPController;
        ssdpController.Close();
        ssdpController.RootDeviceAdded -= OnSSDPRootDeviceAdded;
        ssdpController.RootDeviceRemoved -= OnSSDPRootDeviceRemoved;
        ssdpController.DeviceRebooted -= OnSSDPDeviceRebooted;
        ssdpController.DeviceConfigurationChanged -= OnSSDPDeviceConfigurationChanged;
        _cpData.SSDPController = null;
        foreach (DescriptionRequestState state in _pendingRequests)
          state.Request.Abort();
        _pendingRequests.Clear();
      }
    }

    #endregion

    #region Private/protected members

    protected void InvokeRootDeviceAdded(RootDescriptor rd)
    {
      DeviceAddedDlgt dlgt = RootDeviceAdded;
      if (dlgt != null)
        dlgt(rd);
    }

    protected void InvokeRootDeviceRemoved(RootDescriptor rd)
    {
      DeviceRemovedDlgt dlgt = RootDeviceRemoved;
      if (dlgt != null)
        dlgt(rd);
    }

    protected void InvokeDeviceRebooted(RootDescriptor rd)
    {
      DeviceRebootedDlgt dlgt = DeviceRebooted;
      if (dlgt != null)
        dlgt(rd);
    }

    private void OnSSDPRootDeviceAdded(RootEntry rootEntry)
    {
      InitializeRootDescriptor(rootEntry);
    }

    protected void InitializeRootDescriptor(RootEntry rootEntry)
    {
      RootDescriptor rd = new RootDescriptor(rootEntry)
        {
            State = RootDescriptorState.AwaitingDeviceDescription
        };
      lock (_cpData.SyncObj)
        SetRootDescriptor(rootEntry, rd);
      try
      {
        HttpWebRequest request = CreateHttpGetRequest(new Uri(rootEntry.PreferredLink.DescriptionLocation));
        DescriptionRequestState state = new DescriptionRequestState(rd, request);
        lock (_cpData.SyncObj)
          _pendingRequests.Add(state);
        IAsyncResult result = request.BeginGetResponse(OnDeviceDescriptionReceived, state);
        NetworkHelper.AddTimeout(request, result, PENDING_REQUEST_TIMEOUT * 1000);
      }
      catch (Exception) // Don't log messages at this low protocol level
      {
        lock (_cpData.SyncObj)
          rd.State = RootDescriptorState.Erroneous;
      }
    }

    private void OnDeviceDescriptionReceived(IAsyncResult asyncResult)
    {
      DescriptionRequestState state = (DescriptionRequestState) asyncResult.AsyncState;
      RootDescriptor rd = state.RootDescriptor;
      HttpWebRequest request = state.Request;
      try
      {
        WebResponse response = request.EndGetResponse(asyncResult);
        if (rd.State != RootDescriptorState.AwaitingDeviceDescription)
          return;
        try
        {
          using (Stream body = response.GetResponseStream())
          {
            XPathDocument doc = new XPathDocument(body);
            lock (_cpData.SyncObj)
            {
              rd.DeviceDescription = doc;
              XPathNavigator nav = doc.CreateNavigator();
              nav.MoveToChild(XPathNodeType.Element);
              XPathNodeIterator rootDeviceIt = nav.SelectChildren("device", "urn:schemas-upnp-org:device-1-0");
              if (rootDeviceIt.MoveNext())
              {
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(nav.NameTable);
                nsmgr.AddNamespace("d", UPnPConsts.NS_DEVICE_DESCRIPTION);
                ExtractServiceDescriptorsRecursive(rd, rootDeviceIt.Current, nsmgr, rd.ServiceDescriptors,
                    state.PendingServiceDescriptions);
              }
              rd.State = RootDescriptorState.AwaitingServiceDescriptions;
            }
          }
          ContinueGetServiceDescription(state);
        }
        catch (Exception) // Don't log exceptions at this low protocol level
        {
          rd.State = RootDescriptorState.Erroneous;
        }
        finally
        {
          response.Close();
        }
      }
      catch (WebException e)
      {
        rd.State = RootDescriptorState.Erroneous;
        if (e.Response != null)
          e.Response.Close();
      }
    }

    private void ContinueGetServiceDescription(DescriptionRequestState state)
    {
      RootDescriptor rootDescriptor = state.RootDescriptor;

      IEnumerator<KeyValuePair<ServiceDescriptor, string>> enumer = state.PendingServiceDescriptions.GetEnumerator();
      if (!enumer.MoveNext())
      {
        lock (_cpData.SyncObj)
        {
          if (rootDescriptor.State != RootDescriptorState.AwaitingServiceDescriptions)
            return;
          _pendingRequests.Remove(state);
          rootDescriptor.State = RootDescriptorState.Ready;
        }
        InvokeRootDeviceAdded(rootDescriptor);
      }
      else
      {
        lock (_cpData.SyncObj)
          if (rootDescriptor.State != RootDescriptorState.AwaitingServiceDescriptions)
            return;
        state.CurrentServiceDescriptor = enumer.Current.Key;
        string url = state.PendingServiceDescriptions[state.CurrentServiceDescriptor];
        state.PendingServiceDescriptions.Remove(state.CurrentServiceDescriptor);
        state.CurrentServiceDescriptor.State = ServiceDescriptorState.AwaitingDescription;
        try
        {
          HttpWebRequest request = CreateHttpGetRequest(new Uri(new Uri(rootDescriptor.SSDPRootEntry.PreferredLink.DescriptionLocation), url));
          state.Request = request;
          IAsyncResult result = request.BeginGetResponse(OnServiceDescriptionReceived, state);
          NetworkHelper.AddTimeout(request, result, PENDING_REQUEST_TIMEOUT * 1000);
        }
        catch (Exception) // Don't log exceptions at this low protocol level
        {
          lock (_cpData.SyncObj)
            state.CurrentServiceDescriptor.State = ServiceDescriptorState.Erroneous;
        }
      }
    }

    private void OnServiceDescriptionReceived(IAsyncResult asyncResult)
    {
      DescriptionRequestState state = (DescriptionRequestState) asyncResult.AsyncState;
      RootDescriptor rd = state.RootDescriptor;
      lock (_cpData.SyncObj)
      {
        HttpWebRequest request = state.Request;
        try
        {
          using (WebResponse response = request.EndGetResponse(asyncResult))
          {
            if (rd.State != RootDescriptorState.AwaitingServiceDescriptions)
              return;
            try
            {
              using (Stream body = response.GetResponseStream())
              {
                XPathDocument doc = new XPathDocument(body);
                state.CurrentServiceDescriptor.ServiceDescription = doc;
                state.CurrentServiceDescriptor.State = ServiceDescriptorState.Ready;
              }
            }
            catch (Exception) // Don't log exceptions at this low protocol level
            {
              state.CurrentServiceDescriptor.State = ServiceDescriptorState.Erroneous;
              lock (_cpData.SyncObj)
                rd.State = RootDescriptorState.Erroneous;
            }
            finally
            {
              response.Close();
            }
          }
        }
        catch (WebException e)
        {
          state.CurrentServiceDescriptor.State = ServiceDescriptorState.Erroneous;
          lock (_cpData.SyncObj)
            rd.State = RootDescriptorState.Erroneous;
        if (e.Response != null)
            e.Response.Close();
        }
      }
      // Don't hold the lock while calling ContinueGetServiceDescription - that method is calling event handlers
      try
      {
        ContinueGetServiceDescription(state);
      }
      catch (Exception) // Don't log exceptions at this low protocol level
      {
        rd.State = RootDescriptorState.Erroneous;
      }
    }

    private void OnSSDPRootDeviceRemoved(RootEntry rootEntry)
    {
      RootDescriptor rd = GetRootDescriptor(rootEntry);
      if (rd == null)
        return;
      lock (_cpData.SyncObj)
        InvalidateDescriptor(rd);
      InvokeRootDeviceRemoved(rd);
    }

    private void OnSSDPDeviceRebooted(RootEntry rootEntry, bool configurationChanged)
    {
      RootDescriptor rd = GetRootDescriptor(rootEntry);
      if (rd == null)
        return;
      if (configurationChanged)
        HandleDeviceConfigurationChanged(rd);
      else
        InvokeDeviceRebooted(rd);
    }

    private void OnSSDPDeviceConfigurationChanged(RootEntry rootEntry)
    {
      RootDescriptor rd = GetRootDescriptor(rootEntry);
      if (rd == null)
        return;
      HandleDeviceConfigurationChanged(rd);
    }

    private void HandleDeviceConfigurationChanged(RootDescriptor rootDescriptor)
    {
      // Configuration changes cannot be given to our clients because they need a re-initialization of the
      // device and all service description documents. So configuration changes will be handled by invocing a
      // root device remove/add event combination.
      InvokeRootDeviceRemoved(rootDescriptor);
      InitializeRootDescriptor(rootDescriptor.SSDPRootEntry);
    }

    /// <summary>
    /// Given an XML &lt;device&gt; element containing a device description, this method extracts two kinds of
    /// data:
    /// <list type="bullet">
    /// <item><see cref="ServiceDescriptor"/>s for services of the given device and all embedded devices (organized
    /// in a dictionary mapping device UUIDs to lists of service descriptors for services in the device) in
    /// <paramref name="serviceDescriptors"/></item>
    /// <item>A mapping of all service descriptors which are returned in <paramref name="serviceDescriptors"/> to
    /// their SCPD urls in <paramref name="pendingServiceDescriptions"/></item>
    /// </list>
    /// </summary>
    /// <param name="rd">Root descriptor of the services.</param>
    /// <param name="deviceNav">XPath navigator pointing to an XML &lt;device&gt; element containing the device
    /// description.</param>
    /// <param name="nsmgr">Namespace manager mapping the "d" namespace prefix to the namespace URI
    /// "urn:schemas-upnp-org:device-1-0".</param>
    /// <param name="serviceDescriptors">Dictionary of device UUIDs to collections of service descriptors, containing
    /// descriptors of services which are contained in the device with the key UUID.</param>
    /// <param name="pendingServiceDescriptions">Dictionary of device service descriptors mapped to the SCPD url of the
    /// service.</param>
    private static void ExtractServiceDescriptorsRecursive(RootDescriptor rd, XPathNavigator deviceNav, IXmlNamespaceResolver nsmgr,
        IDictionary<string, IDictionary<string, ServiceDescriptor>> serviceDescriptors,
        IDictionary<ServiceDescriptor, string> pendingServiceDescriptions)
    {
      string deviceUuid = ParserHelper.ExtractUUIDFromUDN(RootDescriptor.GetDeviceUDN(deviceNav, nsmgr));
      XPathNodeIterator it = deviceNav.Select("d:serviceList/d:service", nsmgr);
      if (it.MoveNext())
      {
        IDictionary<string, ServiceDescriptor> sds = serviceDescriptors[deviceUuid] = new Dictionary<string, ServiceDescriptor>();
        do
        {
          string descriptionURL;
          ServiceDescriptor sd = ExtractServiceDescriptor(rd, it.Current, nsmgr, out descriptionURL);
          sds.Add(sd.ServiceTypeVersion_URN, sd);
          pendingServiceDescriptions[sd] = descriptionURL;
        } while (it.MoveNext());
      }
      it = deviceNav.Select("d:deviceList/d:device", nsmgr);
      while (it.MoveNext())
        ExtractServiceDescriptorsRecursive(rd, it.Current, nsmgr, serviceDescriptors, pendingServiceDescriptions);
    }

    /// <summary>
    /// Given an XML &lt;service&gt; element containing a service description, this method extracts the returned
    /// <see cref="ServiceDescriptor"/> and the SCPD description url.
    /// </summary>
    /// <param name="rd">Root descriptor of the service descriptor to be built.</param>
    /// <param name="serviceNav">XPath navigator pointing to an XML &lt;service&gt; element containing the service
    /// description.</param>
    /// <param name="nsmgr">Namespace manager mapping the "d" namespace prefix to the namespace URI
    /// "urn:schemas-upnp-org:device-1-0".</param>
    /// <param name="descriptionURL">Returns the description URL for the service.</param>
    /// <returns>Extracted service descriptor.</returns>
    private static ServiceDescriptor ExtractServiceDescriptor(RootDescriptor rd, XPathNavigator serviceNav, IXmlNamespaceResolver nsmgr,
        out string descriptionURL)
    {
      descriptionURL = ParserHelper.SelectText(serviceNav, "d:SCPDURL/text()", nsmgr);
      string serviceType;
      int serviceTypeVersion;
      if (!ParserHelper.TryParseTypeVersion_URN(ParserHelper.SelectText(serviceNav, "d:serviceType/text()", nsmgr),
          out serviceType, out serviceTypeVersion))
        throw new ArgumentException("'serviceType' content has the wrong format");
      string controlURL = ParserHelper.SelectText(serviceNav, "d:controlURL", nsmgr);
      string eventSubURL = ParserHelper.SelectText(serviceNav, "d:eventSubURL", nsmgr);
      return new ServiceDescriptor(rd, serviceType, serviceTypeVersion,
          ParserHelper.SelectText(serviceNav, "d:serviceId/text()", nsmgr), controlURL, eventSubURL);
    }

    private static HttpWebRequest CreateHttpGetRequest(Uri uri)
    {
      HttpWebRequest request = (HttpWebRequest) WebRequest.Create(uri);
      request.Method = "GET";
      request.KeepAlive = true;
      request.AllowAutoRedirect = true;
      request.UserAgent = UPnPConfiguration.UPnPMachineInfoHeader;
      return request;
    }

    protected void InvalidateDescriptor(RootDescriptor rd)
    {
      rd.State = RootDescriptorState.Invalid;
      foreach (IDictionary<string, ServiceDescriptor> sdDict in rd.ServiceDescriptors.Values)
        foreach (ServiceDescriptor sd in sdDict.Values)
          sd.State = ServiceDescriptorState.Invalid;
    }

    protected RootDescriptor GetRootDescriptor(RootEntry rootEntry)
    {
      object rdObj;
      if (!rootEntry.ClientProperties.TryGetValue(KEY_ROOT_DESCRIPTOR, out rdObj))
        return null;
      return (RootDescriptor) rdObj;
    }

    protected void SetRootDescriptor(RootEntry rootEntry, RootDescriptor rootDescriptor)
    {
      rootEntry.ClientProperties[KEY_ROOT_DESCRIPTOR] = rootDescriptor;
    }

    #endregion
  }
}
