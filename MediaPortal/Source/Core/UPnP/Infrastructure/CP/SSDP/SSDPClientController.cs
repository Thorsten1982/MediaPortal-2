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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MediaPortal.Utilities.Exceptions;
using UPnP.Infrastructure.Common;
using UPnP.Infrastructure.Utils.HTTP;
using UPnP.Infrastructure.Utils;

namespace UPnP.Infrastructure.CP.SSDP
{
  /// <summary>
  /// Delegate used for the <see cref="SSDPClientController.RootDeviceAdded"/> event.
  /// </summary>
  /// <param name="rootEntry">The root entry which was added to the network.</param>
  public delegate void RootDeviceAddedDlgt(RootEntry rootEntry);

  /// <summary>
  /// Delegate used for the <see cref="SSDPClientController.DeviceAdded"/> event.
  /// </summary>
  /// <param name="rootEntry">Root entry of the device which was added.</param>
  /// <param name="deviceEntry">Device entry of the device which was added.</param>
  public delegate void DeviceAddedDlgt(RootEntry rootEntry, DeviceEntry deviceEntry);

  /// <summary>
  /// Delegate used for the <see cref="SSDPClientController.ServiceAdded"/> event.
  /// </summary>
  /// <param name="rootEntry">Root entry of the service which was added.</param>
  /// <param name="deviceEntry">Device entry of the service which was added.</param>
  /// <param name="serviceTypeVersion_URN">URN of the added service's type and version.</param>
  public delegate void ServiceAddedDlgt(RootEntry rootEntry, DeviceEntry deviceEntry, string serviceTypeVersion_URN);

  /// <summary>
  /// Delegate used for the <see cref="SSDPClientController.RootDeviceRemoved"/> event.
  /// </summary>
  /// <param name="rootEntry">The root entry which was removed from the network.</param>
  public delegate void RootDeviceRemovedDlgt(RootEntry rootEntry);

  /// <summary>
  /// Delegate used for the <see cref="SSDPClientController.DeviceRebooted"/> event.
  /// </summary>
  /// <param name="rootEntry">The root entry of the rebooted device.</param>
  /// <param name="configurationChanged">Provides the information if the rebooted device
  /// also did change its configuration.</param>
  public delegate void DeviceRebootedDlgt(RootEntry rootEntry, bool configurationChanged);

  /// <summary>
  /// Delegate used for the <see cref="SSDPClientController.DeviceConfigurationChanged"/> event.
  /// </summary>
  /// <param name="rootEntry">The root entry of the device which changed its configuration.</param>
  public delegate void DeviceConfigurationChangedDlgt(RootEntry rootEntry);

  /// <summary>
  /// Active SSDP listener and controller class which attends the SSDP protocol in a UPnP control point.
  /// Invokes events when devices or services appear or disappear at the network.
  /// </summary>
  public class SSDPClientController : IDisposable
  {
    /// <summary>
    /// Timer interval when the expiration check will occur.
    /// </summary>
    public static int EXPIRATION_TIMER_INTERVAL = 1000;

    protected bool _isActive = false;
    protected Timer _expirationTimer = null;
    protected CPData _cpData;

    /// <summary>
    /// Creates a new instance of <see cref="SSDPClientController"/>.
    /// </summary>
    public SSDPClientController(CPData cpData)
    {
      _cpData = cpData;
    }

    public void Dispose()
    {
      Close();
    }

    #region Event handlers

    private void OnSSDPMulticastReceive(IAsyncResult ar)
    {
      lock (_cpData.SyncObj)
        if (!_isActive)
          return;
      UDPAsyncReceiveState<EndpointConfiguration> state = (UDPAsyncReceiveState<EndpointConfiguration>) ar.AsyncState;
      EndpointConfiguration config = state.Endpoint;
      Socket socket = config.SSDP_UDP_MulticastReceiveSocket;
      if (socket == null)
        return;
      try
      {
        EndPoint remoteEP = new IPEndPoint(state.Endpoint.AddressFamily == AddressFamily.InterNetwork ?
            IPAddress.Any : IPAddress.IPv6Any, 0);
        using (Stream stream = new MemoryStream(state.Buffer, 0, socket.EndReceiveFrom(ar, ref remoteEP)))
        {
          try
          {
            SimpleHTTPRequest header;
            SimpleHTTPRequest.Parse(stream, out header);
            HandleSSDPRequest(header, config, (IPEndPoint) remoteEP);
          }
          catch (Exception e)
          {
            UPnPConfiguration.LOGGER.Debug("SSDPClientController: Problem parsing incoming multicast UDP packet. Error message: '{0}'",
                e.Message);
          }
        }
        StartMulticastReceive(state);
      }
      catch (Exception) // SocketException, ObjectDisposedException
      {
        // Socket was closed - ignore this exception
        UPnPConfiguration.LOGGER.Info("SSDPClientController: Stopping listening for multicast messages at IP endpoint '{0}'",
            NetworkHelper.IPAddrToString(config.EndPointIPAddress));
      }
    }

    private void OnSSDPUnicastReceive(IAsyncResult ar)
    {
      UDPAsyncReceiveState<EndpointConfiguration> state = (UDPAsyncReceiveState<EndpointConfiguration>) ar.AsyncState;
      EndpointConfiguration config = state.Endpoint;
      Socket socket = config.SSDP_UDP_UnicastSocket;
      if (socket == null)
        return;
      try
      {
        EndPoint remoteEP = new IPEndPoint(state.Endpoint.AddressFamily == AddressFamily.InterNetwork ?
            IPAddress.Any : IPAddress.IPv6Any, 0);
        Stream stream = new MemoryStream(state.Buffer, 0, socket.EndReceiveFrom(ar, ref remoteEP));
        try
        {
          SimpleHTTPResponse header;
          SimpleHTTPResponse.Parse(stream, out header);
          HandleSSDPResponse(header, config, (IPEndPoint) remoteEP);
        }
        catch (Exception e)
        {
          UPnPConfiguration.LOGGER.Debug("SSDPClientController: Problem parsing incoming unicast UDP packet. Error message: '{0}'", e.Message);
        }
        StartUnicastReceive(state);
      }
      catch (Exception) // SocketException, ObjectDisposedException
      {
        // Socket was closed - ignore this exception
        UPnPConfiguration.LOGGER.Info("SSDPClientController: Stopping listening for unicast messages at address '{0}'",
            NetworkHelper.IPAddrToString(config.EndPointIPAddress));
      }
    }

    private void OnExpirationTimerElapsed(object state)
    {
      lock (_cpData.SyncObj)
      {
        DateTime now = DateTime.Now;
        ICollection<KeyValuePair<string, RootEntry>> removeEntries = new List<KeyValuePair<string, RootEntry>>(
            _cpData.DeviceEntries.Where(kvp => kvp.Value.ExpirationTime < now));
        foreach (KeyValuePair<string, RootEntry> kvp in removeEntries)
        {
          _cpData.DeviceEntries.Remove(kvp);
          InvokeRootDeviceRemoved(kvp.Value);
        }
      }
    }

    #endregion

    #region Events

    /// <summary>
    /// Invoked when the first notification message for a root device arrives. At the time this event gets invoked, at least
    /// the <see cref="RootEntry.RootDeviceID"/> is set and the <see cref="RootEntry.Devices"/> entry with that uuid is filled.
    /// </summary>
    public event RootDeviceAddedDlgt RootDeviceAdded;

    /// <summary>
    /// Invoked when the first notification message for a device arrives. Gets invoked for each device. Also invoked for root devices.
    /// At the time this event gets invoked, at least the given device entry is present in the <see cref="RootEntry.Devices"/>
    /// collection and its <see cref="DeviceEntry.DeviceTypeVersion_URN"/> property is set.
    /// </summary>
    public event DeviceAddedDlgt DeviceAdded;

    /// <summary>
    /// Invoked when the first notification message for a service arrives. Gets invoked for each service.
    /// At the time this event gets invoked, at least the given service's type and version URN is present in the given
    /// device entry.
    /// </summary>
    public event ServiceAddedDlgt ServiceAdded;

    /// <summary>
    /// Invoked when the first ssdp:byebye-message arrives for any device or service in the given root entry. The whole root entry
    /// should be considered as expired.
    /// </summary>
    public event RootDeviceRemovedDlgt RootDeviceRemoved;

    /// <summary>
    /// Invoked when a UPnP reboot of a device is determined. A reboot means that possibly all event subscriptions at any of
    /// the included services are gone and should be re-triggered. The root entry also might have changed its configuration;
    /// in that case the parameter <c>configurationChanged</c> in the event delegate will be set to <c>true</c>. The new
    /// value of the configuration id can be get by calling <see cref="RootEntry.GetConfigID"/> with the sender's endpoint.
    /// </summary>
    public event DeviceRebootedDlgt DeviceRebooted;

    /// <summary>
    /// Invoked when a UPnP device changed its configuration. That includes changes of the device description document
    /// or any of the SCPD documents of its embedded services. Note that also changes in the URLs for description,
    /// control or eventing (including IP address and port) might have occured.
    /// </summary>
    public event DeviceConfigurationChangedDlgt DeviceConfigurationChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Returns a collection of root entries which are available at the network.
    /// </summary>
    public ICollection<RootEntry> RootEntries
    {
      get
      {
        lock (_cpData.SyncObj)
          return new List<RootEntry>(_cpData.DeviceEntries.Values);
      }
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Starts this SSDP listener. When this method was called, the listener will receive SSDP messages.
    /// </summary>
    public void Start()
    {
      lock (_cpData.SyncObj)
      {
        if (_isActive)
          throw new IllegalCallException("SSDPClientController is already active");
        _isActive = true;
        IList<IPAddress> addresses = NetworkHelper.OrderAddressesByScope(NetworkHelper.GetUPnPEnabledIPAddresses());

        // Add endpoints
        foreach (IPAddress address in addresses)
        {
          AddressFamily family = address.AddressFamily;
          if (family == AddressFamily.InterNetwork && !UPnPConfiguration.USE_IPV4)
            continue;
          if (family == AddressFamily.InterNetworkV6 && !UPnPConfiguration.USE_IPV6)
            continue;
          EndpointConfiguration config = new EndpointConfiguration
            {
                SSDPMulticastAddress = NetworkHelper.GetSSDPMulticastAddressForInterface(address),
                EndPointIPAddress = address
            };

          // Multicast receiver socket - used for receiving multicast messages
          Socket socket = new Socket(family, SocketType.Dgram, ProtocolType.Udp);

          config.SSDP_UDP_MulticastReceiveSocket = socket;
          try
          {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            NetworkHelper.BindAndConfigureSSDPMulticastSocket(socket, address);

            StartMulticastReceive(new UDPAsyncReceiveState<EndpointConfiguration>(config, UPnPConsts.UDP_SSDP_RECEIVE_BUFFER_SIZE, socket));
          }
          catch (Exception) // SocketException, SecurityException
          {
            UPnPConfiguration.LOGGER.Info("SSDPClientController: Unable to bind to multicast address(es) for endpoint '{0}'",
                NetworkHelper.IPAddrToString(config.EndPointIPAddress));
          }

          // Unicast sender and receiver socket - used for sending M-SEARCH queries and receiving its responses.
          // We need a second socket here because the search responses which arrive at this port are structured
          // in another way than the notifications which arrive at our multicast socket.
          socket = new Socket(family, SocketType.Dgram, ProtocolType.Udp);
          config.SSDP_UDP_UnicastSocket = socket;
          try
          {
            socket.Bind(new IPEndPoint(config.EndPointIPAddress, 0));
            _cpData.Endpoints.Add(config);
            StartUnicastReceive(new UDPAsyncReceiveState<EndpointConfiguration>(config, UPnPConsts.UDP_SSDP_RECEIVE_BUFFER_SIZE, socket));
          }
          catch (Exception e) // SocketException, SecurityException
          {
            UPnPConfiguration.LOGGER.Info("SSDPClientController: Unable to bind to unicast address '{0}'", e,
                NetworkHelper.IPAddrToString(config.EndPointIPAddress));
          }
        }

        _expirationTimer = new Timer(OnExpirationTimerElapsed, null, EXPIRATION_TIMER_INTERVAL, EXPIRATION_TIMER_INTERVAL);
        SearchAll(null);
      }
    }

    /// <summary>
    /// Closes this SSDP listener. No more messages will be received.
    /// </summary>
    public void Close()
    {
      lock (_cpData.SyncObj)
      {
        if (!_isActive)
          return;
        _isActive = false;
        foreach (EndpointConfiguration config in _cpData.Endpoints)
        {
          Socket socket = config.SSDP_UDP_MulticastReceiveSocket;
          if (socket != null)
            NetworkHelper.DisposeSSDPMulticastSocket(socket);
          socket = config.SSDP_UDP_UnicastSocket;
          if (socket != null)
            socket.Close();
        }
        _cpData.Endpoints.Clear();
        _cpData.DeviceEntries.Clear();
      }
    }

    /// <summary>
    /// Searches for all UPnP objects in the network.
    /// </summary>
    /// <param name="endPoint">If known, the IP endpoint of the devices to search can be given here. If set to <c>null</c>,
    /// a multicast search will be triggered.</param>
    public void SearchAll(IPEndPoint endPoint)
    {
      SearchForST("ssdp:all", endPoint);
    }

    /// <summary>
    /// Searches for all UPnP root devices in the network.
    /// </summary>
    /// <param name="endPoint">If known, the IP endpoint of the devices to search can be given here. If set to <c>null</c>,
    /// a multicast search will be triggered.</param>
    public void SearchRootDevices(IPEndPoint endPoint)
    {
      SearchForST("upnp:rootdevice", endPoint);
    }

    /// <summary>
    /// Searches for the UPnP device with the specified device's UUID.
    /// </summary>
    /// <param name="uuid">UUID of the device to search. Must be the device's GUID string.</param>
    /// <param name="endPoint">If known, the IP endpoint of the device to search can be given here. If set to <c>null</c>,
    /// a multicast search will be triggered.</param>
    public void SearchDeviceByUUID(string uuid, IPEndPoint endPoint)
    {
      SearchForST("uuid:" + uuid, endPoint);
    }

    /// <summary>
    /// Searches for the UPnP device with the specified device's type and version URN.
    /// </summary>
    /// <param name="deviceType">Device type to search for.</param>
    /// <param name="deviceTypeVersion">Device type version to search for.</param>
    /// <param name="endPoint">If known, the IP endpoint of the device to search can be given here. If set to <c>null</c>,
    /// a multicast search will be triggered.</param>
    public void SearchDeviceByDeviceTypeVersion(string deviceType, string deviceTypeVersion, IPEndPoint endPoint)
    {
      SearchForST("urn:" + deviceType + ":" + deviceTypeVersion, endPoint);
    }

    #endregion

    #region Protected and private methods

    protected void StartMulticastReceive(UDPAsyncReceiveState<EndpointConfiguration> state)
    {
      try
      {
        Socket socket = state.Endpoint.SSDP_UDP_MulticastReceiveSocket;
        EndPoint remoteEP = new IPEndPoint(state.Endpoint.AddressFamily == AddressFamily.InterNetwork ?
            IPAddress.Any : IPAddress.IPv6Any, 0);
        if (socket != null)
          socket.BeginReceiveFrom(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ref remoteEP, OnSSDPMulticastReceive, state);
      }
      catch (Exception e) // SocketException and ObjectDisposedException
      {
        UPnPConfiguration.LOGGER.Error("SSDPClientController: Problem receiving multicast SSDP packets: '{0}'", e.Message);
      }
    }

    protected void StartUnicastReceive(UDPAsyncReceiveState<EndpointConfiguration> state)
    {
      try
      {
        Socket socket = state.Endpoint.SSDP_UDP_UnicastSocket;
        EndPoint remoteEP = new IPEndPoint(state.Endpoint.AddressFamily == AddressFamily.InterNetwork ?
            IPAddress.Any : IPAddress.IPv6Any, 0);
        if (socket != null)
          socket.BeginReceiveFrom(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ref remoteEP, OnSSDPUnicastReceive, state);
      }
      catch (Exception e) // SocketException and ObjectDisposedException
      {
        UPnPConfiguration.LOGGER.Error("SSDPClientController: Problem receiving unicast SSDP packets: '{0}'", e.Message);
      }
    }

    private static bool TryParseMaxAge(string cacheControl, out int maxAge)
    {
      maxAge = 0;
      if (cacheControl == null)
        return false;
      string[] directives = cacheControl.Split(',');
      foreach (string directive in directives)
      {
        int index = directive.IndexOf('=');
        if (index == -1)
          continue;
        if (directive.Substring(0, index).Trim() != "max-age")
          continue;
        if (int.TryParse(directive.Substring(index + 1).Trim(), out maxAge))
          return true;
      }
      return false;
    }

    protected RootEntry GetOrCreateRootEntry(string rootDeviceUUID, UPnPVersion upnpVersion,
        string osVersion, string productVersion, DateTime expirationTime,
        EndpointConfiguration endpoint, string descriptionLocation, HTTPVersion httpVersion, int searchPort, out LinkData link, out bool wasAdded)
    {
      lock (_cpData.SyncObj)
      {
        RootEntry result;
        if (_cpData.DeviceEntries.TryGetValue(rootDeviceUUID, out result))
        {
          result.ExpirationTime = expirationTime;
          wasAdded = false;
        }
        else
        {
          result = _cpData.DeviceEntries[rootDeviceUUID] = new RootEntry(_cpData.SyncObj, rootDeviceUUID, upnpVersion, osVersion, productVersion, expirationTime);
          wasAdded = true;
        }
        link = result.AddOrUpdateLink(endpoint, descriptionLocation, httpVersion, searchPort);
        return result;
      }
    }

    protected RootEntry GetRootEntryByRootDeviceUUID(string deviceUUID)
    {
      lock (_cpData.SyncObj)
      {
        RootEntry result;
        if (_cpData.DeviceEntries.TryGetValue(deviceUUID, out result))
          return result;
      }
      return null;
    }

    protected void RemoveRootEntry(RootEntry rootEntry)
    {
      lock (_cpData.SyncObj)
        _cpData.DeviceEntries.Remove(rootEntry.RootDeviceID);
    }

    /// <summary>
    /// Returns the root entry which contains any device of the given <paramref name="uuid"/>.
    /// </summary>
    /// <param name="uuid">UUID of any device to search the enclosing root entry for.</param>
    /// <returns>Root entry instance or <c>null</c>, if no device with the given UUID was found.</returns>
    protected RootEntry GetRootEntryByContainedDeviceUUID(string uuid)
    {
      lock (_cpData.SyncObj)
      {
        foreach (KeyValuePair<string, RootEntry> kvp in _cpData.DeviceEntries)
          if (kvp.Value.Devices.ContainsKey(uuid))
            return kvp.Value;
      }
      return null;
    }

    protected void SearchForST(string st, IPEndPoint endPoint)
    {
      if (endPoint == null)
        MulticastSearchForST(st);
      else
        UnicastSearchForST(st, endPoint);
    }

    protected void UnicastSearchForST(string st, IPEndPoint endPoint)
    {
      SimpleHTTPRequest request = new SimpleHTTPRequest("M-SEARCH", "*");
      request.SetHeader("HOST", NetworkHelper.IPEndPointToString(endPoint));
      request.SetHeader("MAN", "\"ssdp:discover\"");
      request.SetHeader("ST", st);
      request.SetHeader("USER-AGENT", UPnPConfiguration.UPnPMachineInfoHeader);
      lock (_cpData.SyncObj)
      {
        foreach (EndpointConfiguration config in _cpData.Endpoints)
        {
          if (config.AddressFamily != endPoint.AddressFamily)
            continue;
          Socket socket = config.SSDP_UDP_UnicastSocket;
          if (socket == null)
            return;
          byte[] bytes = request.Encode();
          NetworkHelper.SendData(socket, endPoint, bytes, 1); // The server will send the answer to the same socket as we use to send
          return;
        }
      }
    }

    protected void MulticastSearchForST(string st)
    {
      SimpleHTTPRequest request = new SimpleHTTPRequest("M-SEARCH", "*");
      request.SetHeader("MAN", "\"ssdp:discover\"");
      request.SetHeader("MX", UPnPConfiguration.SEARCH_MX.ToString());
      request.SetHeader("ST", st);
      request.SetHeader("USER-AGENT", UPnPConfiguration.UPnPMachineInfoHeader);
      lock (_cpData.SyncObj)
      {
        foreach (EndpointConfiguration config in _cpData.Endpoints)
        {
          IPEndPoint ep = new IPEndPoint(config.SSDPMulticastAddress, UPnPConsts.SSDP_MULTICAST_PORT);
          request.SetHeader("HOST", NetworkHelper.IPEndPointToString(ep));
          Socket socket = config.SSDP_UDP_UnicastSocket;
          if (socket == null)
            continue;
          byte[] bytes = request.Encode();
          NetworkHelper.MulticastMessage(socket, config.SSDPMulticastAddress, bytes); // The server will send the answer to the same socket as we use to send
        }
      }
    }

    protected void HandleSSDPRequest(SimpleHTTPRequest header, EndpointConfiguration config, IPEndPoint remoteEndPoint)
    {
      switch (header.Method)
      {
        case "NOTIFY":
          HandleNotifyRequest(header, config, remoteEndPoint);
          break;
        case "UPDATE":
          HandleUpdatePacket(header, config);
          break;
      }
    }

    protected void HandleSSDPResponse(SimpleHTTPResponse header, EndpointConfiguration config, IPEndPoint remoteEndPoint)
    {
      HTTPVersion httpVersion;
      if (!HTTPVersion.TryParse(header.HttpVersion, out httpVersion))
        // Invalid response
        return;
      string cacheControl = header["CACHE-CONTROL"];
      string date = header["DATE"];
      // EXT is not used
      //string ext = header["EXT"];
      string location = header["LOCATION"];
      string server = header["SERVER"];
      // ST is not used
      //string st = header["ST"];
      string usn = header["USN"];
      string bi = header["BOOTID.UPNP.ORG"];
      string ci = header["CONFIGID.UPNP.ORG"];
      string sp = header["SEARCHPORT.UPNP.ORG"];
      HandleNotifyPacket(config, remoteEndPoint, httpVersion, date, cacheControl, location, server, "ssdp:alive", usn, bi, ci, sp);
    }

    protected void HandleNotifyRequest(SimpleHTTPRequest header, EndpointConfiguration config, IPEndPoint remoteEndPoint)
    {
      if (header.Param != "*")
          // Invalid message
        return;
      HTTPVersion httpVersion;
      if (!HTTPVersion.TryParse(header.HttpVersion, out httpVersion))
        // Invalid message
        return;
      // HOST not interesting
      //string host = header["HOST"];
      string cacheControl = header["CACHE-CONTROL"];
      string location = header["LOCATION"];
      string server = header["SERVER"];
      // NT is not evaluated, we get all information from the USN header
      //string nt = header["NT"];
      string nts = header["NTS"];
      string usn = header["USN"];
      string bi = header["BOOTID.UPNP.ORG"];
      string ci = header["CONFIGID.UPNP.ORG"];
      string sp = header["SEARCHPORT.UPNP.ORG"];
      HandleNotifyPacket(config, remoteEndPoint, httpVersion, DateTime.Now.ToUniversalTime().ToString("R"), cacheControl, location, server, nts, usn, bi, ci, sp);
    }

    protected void HandleNotifyPacket(EndpointConfiguration config, IPEndPoint remoteEndPoint, HTTPVersion httpVersion,
        string date, string cacheControl, string location, string server, string nts, string usn, string bi, string ci, string sp)
    {
      uint bootID = 0;
      if (bi != null && !uint.TryParse(bi, out bootID))
        // Invalid message
        return;
      uint configID = 0;
      if (ci != null && !uint.TryParse(ci, out configID))
        // Invalid message
        return;
      if (!usn.StartsWith("uuid:"))
        // Invalid usn
        return;
      string deviceUUID;
      string messageType;
      if (!ParserHelper.TryParseUSN(usn, out deviceUUID, out messageType))
        // We only use messages of the type "uuid:device-UUID::..." and discard the "uuid:device-UUID" message
        return;
      if (nts == "ssdp:alive")
      {
        if (server == null)
          // Invalid message
          return;
        int maxAge;
        if (!TryParseMaxAge(cacheControl, out maxAge))
          // Invalid message
          return;
        DateTime d;
        if (!DateTime.TryParse(date, out d))
          d = DateTime.Now;
        DateTime expirationTime = d.AddSeconds(maxAge);
        string[] versionInfos = server.Split(' ');
        string upnpVersionInfo = versionInfos.FirstOrDefault(v => v.StartsWith(UPnPVersion.VERSION_PREFIX));
        if (upnpVersionInfo == null)
          // Invalid message
          return;
        UPnPVersion upnpVersion;
        if (!UPnPVersion.TryParse(upnpVersionInfo, out upnpVersion))
          // Invalid message
          return;
        if (upnpVersion.VerMax != 1)
          // Incompatible UPnP version
          return;
        int searchPort = 1900;
        if (upnpVersion.VerMin >= 1)
        {
          if (bi == null || ci == null)
            // Invalid message
            return;
          if (sp != null && (!int.TryParse(sp, out searchPort) || searchPort < 49152 || searchPort > 65535))
            // Invalid message
            return;
        }
        RootEntry rootEntry;
        DeviceEntry deviceEntry = null;
        string serviceType = null;
        bool fireDeviceRebooted = false;
        bool fireConfigurationChanged = false;
        bool fireRootDeviceAdded = false;
        bool fireDeviceAdded = false;
        bool fireServiceAdded = false;
        lock (_cpData.SyncObj)
        {
          bool rootEntryAdded;
          LinkData link;
          rootEntry = GetOrCreateRootEntry(deviceUUID, upnpVersion, versionInfos[0], versionInfos[2],
              expirationTime, config, location, httpVersion, searchPort, out link, out rootEntryAdded);
          if (bi != null && rootEntry.BootID > bootID)
            // Invalid message
            return;
          uint currentConfigId = rootEntry.GetConfigID(remoteEndPoint);
          if (currentConfigId != 0 && currentConfigId != configID)
            fireConfigurationChanged = true;
          rootEntry.SetConfigID(remoteEndPoint, configID);
          if (!rootEntryAdded && bi != null && rootEntry.BootID < bootID)
            // Device reboot
            fireDeviceRebooted = true;
          rootEntry.BootID = bootID;
          if (messageType == "upnp:rootdevice")
          {
            rootEntry.GetOrCreateDeviceEntry(deviceUUID);
            if (rootEntryAdded)
              fireRootDeviceAdded = true;
          }
          else if (messageType.StartsWith("urn:"))
          {
            if (messageType.IndexOf(":device:") > -1)
            {
              string deviceType;
              int deviceTypeVersion;
              if (!ParserHelper.TryParseTypeVersion_URN(messageType, out deviceType, out deviceTypeVersion))
                // Invalid message
                return;
              deviceEntry = rootEntry.GetOrCreateDeviceEntry(deviceUUID);
              fireDeviceAdded = string.IsNullOrEmpty(deviceEntry.DeviceTypeVersion_URN);
              deviceEntry.DeviceType = deviceType;
              deviceEntry.DeviceTypeVersion = deviceTypeVersion;
            }
            else if (messageType.IndexOf(":service:") > -1)
            {
              deviceEntry = rootEntry.GetOrCreateDeviceEntry(deviceUUID);
              serviceType = messageType;
              if (deviceEntry.Services.Contains(serviceType))
                return;
              deviceEntry.Services.Add(serviceType);
              fireServiceAdded = true;
            }
          }
          else
            // Invalid message
            return;
        }
        // Raise events after returning the lock
        if (fireDeviceRebooted)
          InvokeDeviceRebooted(rootEntry, fireConfigurationChanged);
        else if (fireConfigurationChanged)
          InvokeDeviceConfigurationChanged(rootEntry);
        if (fireRootDeviceAdded)
          InvokeRootDeviceAdded(rootEntry);
        if (fireDeviceAdded)
          InvokeDeviceAdded(rootEntry, deviceEntry);
        if (fireServiceAdded)
          InvokeServiceAdded(rootEntry, deviceEntry, serviceType);
      }
      else if (nts == "ssdp:byebye")
      {
        RootEntry rootEntry = GetRootEntryByContainedDeviceUUID(deviceUUID);
        if (rootEntry != null)
        {
          if (bi != null && rootEntry.BootID > bootID)
            // Invalid message
            return;
          RemoveRootEntry(rootEntry);
          InvokeRootDeviceRemoved(rootEntry);
        }
      }
    }

    protected void HandleUpdatePacket(SimpleHTTPRequest header, EndpointConfiguration config)
    {
      if (header.Param != "*")
        // Invalid message
        return;
      HTTPVersion httpVersion;
      if (!HTTPVersion.TryParse(header.HttpVersion, out httpVersion))
        // Invalid message
        return;
      // Host, NT, NTS, USN are not interesting
      //string host = header["HOST"];
      //string nt = header["NT"];
      //string nts = header["NTS"];
      string usn = header["USN"];
      //string location = header["LOCATION"];
      string bi = header["BOOTID.UPNP.ORG"];
      uint bootID;
      if (!uint.TryParse(bi, out bootID))
        // Invalid message
        return;
      string nbi = header["NEXTBOOTID.UPNP.ORG"];
      uint nextBootID;
      if (!uint.TryParse(nbi, out nextBootID))
        // Invalid message
        return;
      if (!usn.StartsWith("uuid:"))
        // Invalid usn
        return;
      int separatorIndex = usn.IndexOf("::");
      if (separatorIndex < 6) // separatorIndex == -1 or separatorIndex not after "uuid:" prefix with at least one char UUID
        // We only use messages containing a "::" substring and discard the "uuid:device-UUID" message
        return;
      string deviceUUID = usn.Substring(5, separatorIndex - 5);
      RootEntry rootEntry = GetRootEntryByRootDeviceUUID(deviceUUID);
      if (rootEntry == null)
        return;
      if (rootEntry.BootID > bootID)
        // Invalid message
        return;
      bool fireDeviceRebooted = false;
      lock (_cpData.SyncObj)
      {
        if (rootEntry.BootID < bootID)
          // Device reboot
          fireDeviceRebooted = true;
        rootEntry.BootID = nextBootID;
      }
      if (fireDeviceRebooted)
        InvokeDeviceRebooted(rootEntry, false);
    }

    protected void InvokeRootDeviceAdded(RootEntry rootEntry)
    {
      RootDeviceAddedDlgt dlgt = RootDeviceAdded;
      if (dlgt != null)
        dlgt(rootEntry);
    }

    protected void InvokeDeviceAdded(RootEntry rootEntry, DeviceEntry deviceEntry)
    {
      DeviceAddedDlgt dlgt = DeviceAdded;
      if (dlgt != null)
        dlgt(rootEntry, deviceEntry);
    }

    protected void InvokeServiceAdded(RootEntry rootEntry, DeviceEntry deviceEntry, string serviceTypeVersion_URN)
    {
      ServiceAddedDlgt dlgt = ServiceAdded;
      if (dlgt != null)
        dlgt(rootEntry, deviceEntry, serviceTypeVersion_URN);
    }

    protected void InvokeRootDeviceRemoved(RootEntry rootEntry)
    {
      RootDeviceRemovedDlgt dlgt = RootDeviceRemoved;
      if (dlgt != null)
        dlgt(rootEntry);
    }

    protected void InvokeDeviceRebooted(RootEntry rootEntry, bool configChanged)
    {
      DeviceRebootedDlgt dlgt = DeviceRebooted;
      if (dlgt != null)
        dlgt(rootEntry, configChanged);
    }

    protected void InvokeDeviceConfigurationChanged(RootEntry rootEntry)
    {
      DeviceConfigurationChangedDlgt dlgt = DeviceConfigurationChanged;
      if (dlgt != null)
        dlgt(rootEntry);
    }

    #endregion
  }
}
