﻿using System;
using System.Windows;
using System.ComponentModel;
using Alphora.Dataphor.DAE.Listener;
using System.Windows.Controls;
using Alphora.Dataphor.DAE.Client;

namespace Alphora.Dataphor.Frontend.Client.Silverlight
{
	/// <summary> Manages the connection work-flow. </summary>
	public class ConnectWorkItem  : INotifyPropertyChanged
	{
		private ConnectStatus FStatus;

		public ConnectStatus Status
		{
			get { return FStatus; }
			set
			{
				if (FStatus != value)
				{
					FStatus = value;
					NotifyPropertyChanged("Status");
				}
			}
		}
		
		private Exception FException;
		
		public Exception Exception
		{
			get { return FException; }
			set
			{
				if (value != FException)
				{
					FException = value;
					NotifyPropertyChanged("Exception");
				}
			}
		}
		
		private string FHostName;
		
		public string HostName
		{
			get { return FHostName; }
			set
			{
				if (FHostName != value)
				{
					FHostName = value;
					NotifyPropertyChanged("HostName");
				}
			}
		}
		
		private string[] FInstances;
		
		public string[] Instances
		{
			get { return FInstances; }
			set 
			{ 
				if (FInstances != value)
				{
					FInstances = value; 
					NotifyPropertyChanged("Instances");
				}
			}
		}

		public void BeginLoadInstances()
		{
			Status = ConnectStatus.LoadingInstances;
			Session.BeginInvoke<string[]>
			(
				() => { return ListenerFactory.EnumerateInstances(FHostName); },
				(Exception AException) => 
				{ 
					Exception = AException; 
					Status = ConnectStatus.SelectHost;
				},
				(string[] AInstances) => 
				{
					Exception = null;
					Instances = AInstances; 
					Status = ConnectStatus.SelectInstance;
				}
			);
		}
		
		private string FInstanceName;
		
		public string InstanceName
		{
			get { return FInstanceName; }
			set
			{
				if (FInstanceName != value)
				{
					FInstanceName = value;
					NotifyPropertyChanged("InstanceName");
				}
			}
		}

		private string FUserName;
		
		public string UserName
		{
			get { return FUserName; }
			set
			{
				if (value != FUserName)
				{
					FUserName = value;
					NotifyPropertyChanged("UserName");
				}
			}
		}

		private string FPassword;
		
		public string Password
		{
			get { return FPassword; }
			set
			{
				if (value != FPassword)
				{
					FPassword = value;
					NotifyPropertyChanged("Password");
				}
			}
		}
		
		public void BeginLogin()
		{
    		Status = ConnectStatus.Connecting;

    		Session.BeginInvoke<DataView>
    		(
    			() =>
    			{
    				var LDataSession = 
    					new DataSession()
    					{
    						Alias = 
    							new ConnectionAlias()
    							{
    								HostName = FHostName,
    								InstanceName = FInstanceName
    							}
    					};
    				LDataSession.Alias.SessionInfo.UserID = FUserName;
    				LDataSession.Alias.SessionInfo.Password = FPassword;
    				LDataSession.Open();
    				try
    				{
    					return LDataSession.OpenReadOnlyDataView(".Frontend.Applications");
    				}
    				catch
    				{
    					LDataSession.Dispose();
    					throw;
    				}
    			}, 
    			(Exception AException) => 
    			{
    				Exception = AException;
    				Status = ConnectStatus.Login;
    			},
    			(DataView AApplications) =>
    			{
    				Exception = null;
    				DataSession = AApplications.Session;
   					Status = ConnectStatus.SelectApplication;
    			}
    		);
		}

		private DataSession FDataSession;
		
		public DataSession DataSession
		{
			get { return FDataSession; }
			set
			{
				if (FDataSession != value)
				{
					FDataSession = value;
					NotifyPropertyChanged("DataSession");
				}
			}
		}
		
		private string FApplicationID;
		
		public string Application
		{
			get { return FApplicationID; }
			set
			{
				if (FApplicationID != value)
				{
					FApplicationID = value;
					NotifyPropertyChanged("ApplicationID");
				}
			}
		}
		
		private void BeginStartApplication(EventHandler AOnComplete, ContentControl AContainer)
		{
			Silverlight.Session.BeginInvoke<Silverlight.Session>
			(
				() =>
				{
					var LSession = new Silverlight.Session(DataSession, true);
					var LStartingDocument = LSession.SetApplication(FApplicationID);
					LSession.Start(LStartingDocument, AOnComplete, AContainer);
					return LSession;
				},
				(Exception LException) =>
				{
					Exception = LException;
					Status = ConnectStatus.SelectApplication;
				},
				(Silverlight.Session ASession) =>
				{
					Session = ASession;
					Status = ConnectStatus.Complete;
				}
			);
		}
		
		private Silverlight.Session FSession;
		
		public Silverlight.Session Session
		{
			get { return FSession; }
			set
			{
				if (FSession != value)
				{
					FSession = value;
					NotifyPropertyChanged("Session");
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	
		public void NotifyPropertyChanged(string AName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(AName));
		}
	}
	
	public enum ConnectStatus 
	{ 
		SelectHost, 
		LoadingInstances, 
		SelectInstance, 
		Login, 
		Connecting, 
		SelectApplication, 
		StartingSession, 
		Complete 
	};
}
