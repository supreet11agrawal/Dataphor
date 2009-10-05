﻿/*
	Alphora Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;

using Alphora.Dataphor.DAE;
using Alphora.Dataphor.DAE.Contracts;
using Alphora.Dataphor.DAE.Server;
using Alphora.Dataphor.DAE.Listener;

namespace Alphora.Dataphor.DAE.Client
{
	public class ClientServer : ClientObject, IRemoteServer
	{
		public ClientServer() { }
		public ClientServer(string AHostName, int AOverridePortNumber, string AInstanceName)
		{
			FHostName = AHostName;
			FOverridePortNumber = AOverridePortNumber;
			FInstanceName = AInstanceName;
			Open();
		}
		
		private string FHostName;
		public string HostName
		{
			get	{ return FHostName; }
			set 
			{
				CheckInactive();
				FHostName = value;
			}
		}
		
		private int FOverridePortNumber;
		public int OverridePortNumber
		{
			get { return FOverridePortNumber; }
			set
			{
				CheckInactive();
				FOverridePortNumber = value; 
			}
		}
		
		private string FInstanceName;
		public string InstanceName
		{
			get { return FInstanceName; }
			set
			{
				CheckInactive();
				FInstanceName = value;
			}
		}
		
		private ChannelFactory<IClientDataphorService> FChannelFactory;
		
		public bool IsActive { get { return FChannelFactory != null; } }
		
		private void CheckActive()
		{
			if (!IsActive)
				throw new ServerException(ServerException.Codes.ServerInactive);
		}
		
		private void CheckInactive()
		{
			if (IsActive)
				throw new ServerException(ServerException.Codes.ServerActive);
		}
		
		public void Open()
		{
			if (!IsActive)
				if (FOverridePortNumber == 0)
				{
					FChannelFactory =
						new ChannelFactory<IClientDataphorService>
						(
							DataphorServiceUtility.GetBinding(), 
							new EndpointAddress(ListenerFactory.GetInstanceURI(FHostName, FInstanceName))
						);
				}
				else
				{
					FChannelFactory = 
						new ChannelFactory<IClientDataphorService>
						(
							DataphorServiceUtility.GetBinding(), 
							new EndpointAddress(DataphorServiceUtility.BuildInstanceURI(FHostName, FOverridePortNumber, FInstanceName))
						);
				}
		}
		
		public void Close()
		{
			if (IsActive)
			{
				if (FChannel != null)
					CloseChannel(FChannel);
				SetChannel(null);
				if (FChannelFactory != null)
				{
					CloseChannelFactory();
					FChannelFactory = null;
				}
			}
		}

		protected override void InternalDispose()
		{
			Close();
		}
		
		private IClientDataphorService FChannel;
		
		private bool IsChannelFaulted(IClientDataphorService AChannel)
		{
			return ((ICommunicationObject)AChannel).State == CommunicationState.Faulted;
		}
		
		private bool IsChannelValid(IClientDataphorService AChannel)
		{
			return ((ICommunicationObject)AChannel).State == CommunicationState.Opened;
		}
		
		private void CloseChannel(IClientDataphorService AChannel)
		{
			ICommunicationObject LChannel = (ICommunicationObject)AChannel;
			if (LChannel.State == CommunicationState.Opened)
				LChannel.Close();
			else
				LChannel.Abort();
		}
		
		private void CloseChannelFactory()
		{
			if (FChannelFactory.State == CommunicationState.Opened)
				FChannelFactory.Close();
			else
				FChannelFactory.Abort();
		}
		
		private void SetChannel(IClientDataphorService AChannel)
		{
			if (FChannel != null)
				((ICommunicationObject)FChannel).Faulted -= new EventHandler(ChannelFaulted);
			FChannel = AChannel;
			if (FChannel != null)
				((ICommunicationObject)FChannel).Faulted += new EventHandler(ChannelFaulted);
		}
		
		private void ChannelFaulted(object ASender, EventArgs AArgs)
		{
			((ICommunicationObject)ASender).Faulted -= new EventHandler(ChannelFaulted);
			if (FChannel == ASender)
				FChannel = null;
		}
		
		public IClientDataphorService GetServiceInterface()
		{
			CheckActive();
			if ((FChannel == null) || !IsChannelValid(FChannel))
				SetChannel(FChannelFactory.CreateChannel());
			return FChannel;
		}

		#region IRemoteServer Members

		public IRemoteServerConnection Establish(string AConnectionName, string AHostName)
		{
			return new ClientConnection(this, AConnectionName, AHostName);
		}

		public void Relinquish(IRemoteServerConnection AConnection)
		{
			// Nothing to do here
		}

		#endregion

		#region IServerBase Members

		public string Name
		{
			get 
			{ 
				IAsyncResult LResult = GetServiceInterface().BeginGetServerName(null, null);
				LResult.AsyncWaitHandle.WaitOne();
				return GetServiceInterface().EndGetServerName(LResult);
			}
		}

		public void Start()
		{
			throw new NotImplementedException();
		}

		public void Stop()
		{
			throw new NotImplementedException();
		}

		public ServerState State
		{
			get 
			{ 
				throw new NotImplementedException();
			}
		}

		public long CacheTimeStamp
		{
			get 
			{ 
				IAsyncResult LResult = GetServiceInterface().BeginGetCacheTimeStamp(null, null);
				LResult.AsyncWaitHandle.WaitOne();
				return GetServiceInterface().EndGetCacheTimeStamp(LResult);
			}
		}

		public long DerivationTimeStamp
		{
			get 
			{ 
				IAsyncResult LResult = GetServiceInterface().BeginGetDerivationTimeStamp(null, null);
				LResult.AsyncWaitHandle.WaitOne();
				return GetServiceInterface().EndGetDerivationTimeStamp(LResult);
			}
		}

		#endregion
	}
}