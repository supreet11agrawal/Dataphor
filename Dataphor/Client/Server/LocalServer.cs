/*
	Dataphor
	© Copyright 2000-2009 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

#define USESPINLOCK
#define LOGFILECACHEEVENTS

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Alphora.Dataphor.DAE.Language.D4;
using Alphora.Dataphor.DAE.Runtime;
using Alphora.Dataphor.DAE.Streams;
using Alphora.Dataphor.DAE.Contracts;
using Alphora.Dataphor.DAE.Device.Catalog;

namespace Alphora.Dataphor.DAE.Server
{
	/*
		LocalServerObject
			|- LocalServer
		LocalServerChildObject
			|- LocalSession
			|- LocalProcess
			|- LocalScript
			|- LocalBatch
			|- LocalPlan
			|	|- LocalExpressionPlan
			|	|- LocalStatementPlan
			|- LocalCursor
	*/
	
    public class LocalServer : LocalServerObject, IServer, IDisposable
    {
		/// <remarks>The buffer size to use when copying library files over the CLI.</remarks>
		public const int CFileCopyBufferSize = 16384;
		
		public LocalServer(IRemoteServer AServer, bool AClientSideLoggingEnabled, string AHostName) : base()
		{
			FServer = AServer;
			FHostName = AHostName;
			FReferenceCount = 1;
			FInternalServer = new Engine();
			FInternalServer.Name = Schema.Object.NameFromGuid(Guid.NewGuid());
			FInternalServer.LoggingEnabled = AClientSideLoggingEnabled;
			FInternalServer.Start();
			FServerConnection = FServer.Establish(FInternalServer.Name, FHostName);
			FServerCacheTimeStamp = AServer.CacheTimeStamp;
			FClientCacheTimeStamp = 1;
		}
		
		public void Dispose()
		{
			Dispose(true);
		}

		protected override void Dispose(bool ADisposing)
		{
			try
			{
				try
				{
					if (FInternalServer != null)
					{
						FInternalServer.Dispose();
						FInternalServer = null;
					}
				}
				finally
				{
					if (FServer != null)
					{
						try
						{
							if (FServerConnection != null)
								FServer.Relinquish(FServerConnection);
						}
						finally
						{
							FServerConnection = null;
							FServer = null;
						}
					}
				}
			}
			finally
			{
				base.Dispose(ADisposing);
			}
		}
		
		// Reference Counting
		protected int FReferenceCount;

		/// <summary> Increments the reference counter. </summary>
		internal protected void AddReference()
		{
			FReferenceCount++;
		}

		/// <summary> Decrements the reference counter, disposes when no references. </summary>
		internal protected void RemoveReference()
		{
			FReferenceCount--;
			if (FReferenceCount == 0)
				Dispose();
		}

		private string FServerName;
		public string Name 
		{ 
			get 
			{ 
				if (FServerName == null)
					FServerName = FServer.Name;
				return FServerName;
			} 
		}
		
		protected IRemoteServer FServer;
		public IRemoteServer RemoteServer { get	{ return FServer; } }
		
		protected IRemoteServerConnection FServerConnection;
		public IRemoteServerConnection ServerConnection { get { return FServerConnection; } }

		// An internal server used to evaluate remote proposable calls
		protected internal Engine FInternalServer;
		
		private string FHostName;
		
		// Client-side catalog cache
		
		// The catalog in FInternalServer is used as a client-side catalog cache both to retrieve the result set descriptions
		// of expressions opened remotely, and to allow for the remote evaluation of proposable calls. The cache is transparently
		// maintained for each connection to the remote server using a set of timestamps. When the connection is established, the
		// client-side cache timestamp is initialized to 1, and the cachetimestamp of the catalog is recorded. As expressions are
		// evaluated, the catalog necessary to support the expressions is downloaded to the client-side cache. On the server side,
		// any time the catalog cachetimestamp is incremented (as a result of a catalog change such as an alter table statement),
		// the new catalog cachetimestamp is recorded. Client-side, the following logic is used to ensure that the catalog cache
		// consistent and contains the necessary objects:
		
		// given that:
		//	FServerCacheTimeStamp: The current catalog cachetimestamp of the client-side catalog cache.
		//	AServerCacheTimeStamp: The catalog cachetimestamp of the client-side catalog cache required to support a given expression.
		//  FClientCacheTimeStamp: The current timestamp of the client-side catalog cache.
		//  AClientCacheTimeStamp: The timestamp of the client-side catalog cache required to support a given expression.
		
		// When an expression is evaluated, the resulting plan descriptor will include the catalog cachetimestamp of the catalog
		// at the moment that this expression was prepared (AServerCacheTimeStamp), and the timestamp of the client-side catalog
		// cache required to support compilation of the expression locally and any associated remote proposable calls.
		// if the expression requires new objects to be added to the client-side catalog cache
			// wait for AClientCacheTimeStamp - 1
				// while FClientCacheTimeStamp < AClientCacheTimeStamp and we have waited less than CCachLockTimeout milliseconds
					// Reset the cache sync event and then wait CCacheLockTimeout milliseconds for it be signaled
			// Acquire the catalog cache lock in exclusive mode
			// if FServerCacheTimeStamp < AServerCacheTimeStamp
				// clear the client-side catalog cache
				// set FServerCacheTimeStamp = AServerCacheTimeStamp
			// Execute the DDL statements returned by the server to modify the client-side catalog cache.
			// set the client cache timestamp
			// set the CacheSyncEvent to signaled, waking up any threads that may be waiting for this client cache timestamp
			// Release the catalog cache lock
			
		// All operations that require read access to the client-side catalog cache must take the catalog cache lock in shared mode.
				
		protected internal long FServerCacheTimeStamp;
		protected internal long FClientCacheTimeStamp;

		#if USESPINLOCK		
		private int FCacheSyncRoot = 0;
		#else
		private object FCacheSyncRoot = new object();
		#endif
		
		private SignalPool FCacheSignalPool = new SignalPool();
		private Dictionary<long, ManualResetEvent> FCacheSignals = new Dictionary<long, ManualResetEvent>();

		protected internal void WaitForCacheTimeStamp(IServerProcess AProcess, long AClientCacheTimeStamp)
		{
			#if LOGCACHEEVENTS
			FInternalServer.LogMessage(LogEntryType.Information, String.Format("Thread {0} checking for cache time stamp {1}", Thread.CurrentThread.GetHashCode(), AClientCacheTimeStamp.ToString()));
			#endif
			try
			{
				ManualResetEvent LSignal = null;
				bool LSignalAdded = false;
				
				#if USESPINLOCK
				while (Interlocked.CompareExchange(ref FCacheSyncRoot, 1, 0) == 1)
					Thread.SpinWait(100);  // Prevents CPU starvation
				try
				#else
				lock (FCacheSyncRoot)
				#endif
				{
					if (FClientCacheTimeStamp == AClientCacheTimeStamp)
						return;
						
					if (FClientCacheTimeStamp > AClientCacheTimeStamp)
					{
						AProcess.Execute(".System.UpdateTimeStamps();", null);
						throw new ServerException(ServerException.Codes.CacheSerializationError, AClientCacheTimeStamp, FClientCacheTimeStamp);
					}
					
					LSignalAdded = !FCacheSignals.TryGetValue(AClientCacheTimeStamp, out LSignal);
					if (LSignalAdded)
					{
						LSignal = FCacheSignalPool.Acquire();
						FCacheSignals.Add(AClientCacheTimeStamp, LSignal);
					}

					//Error.AssertFail(FCacheSyncEvent.Reset(), "Internal error: CacheSyncEvent reset failed");
				}
				#if USESPINLOCK
				finally
				{
					Interlocked.Decrement(ref FCacheSyncRoot);
				}
				#endif

				#if LOGCACHEEVENTS
				FInternalServer.LogMessage(LogEntryType.Information, String.Format("Thread {0} waiting for cache time stamp {1}", Thread.CurrentThread.GetHashCode(), AClientCacheTimeStamp.ToString()));
				#endif
				
				try
				{
					if (!(LSignal.WaitOne(CCacheSerializationTimeout)))
						throw new ServerException(ServerException.Codes.CacheSerializationTimeout);
				}
				finally
				{
					if (LSignalAdded)
					{
						#if USESPINLOCK
						while (Interlocked.CompareExchange(ref FCacheSyncRoot, 1, 0) == 1)
							Thread.SpinWait(100); // Prevents CPU starvation
						try
						#else
						lock (FCacheSyncRoot)
						#endif
						{
							FCacheSignals.Remove(AClientCacheTimeStamp);
						}
						#if USESPINLOCK
						finally
						{
							Interlocked.Decrement(ref FCacheSyncRoot);
						}
						#endif
						
						FCacheSignalPool.Relinquish(LSignal);
					}
				}
			}
			catch (Exception E)
			{
				FInternalServer.LogError(E);
				throw E;
			}
		}
		
		protected internal void SetCacheTimeStamp(IServerProcess AProcess, long AClientCacheTimeStamp)
		{
			#if LOGCACHEEVENTS
			FInternalServer.LogMessage(LogEntryType.Information, String.Format("Thread {0} updating cache time stamp to {1}", Thread.CurrentThread.GetHashCode(), AClientCacheTimeStamp.ToString()));
			#endif
			
			#if USESPINLOCK
			while (Interlocked.CompareExchange(ref FCacheSyncRoot, 1, 0) == 1)
				Thread.SpinWait(100);
			try
			#else
			lock (FCacheSyncRoot)
			#endif
			{
				FClientCacheTimeStamp = AClientCacheTimeStamp;
				
				ManualResetEvent LSignal;
				if (FCacheSignals.TryGetValue(AClientCacheTimeStamp, out LSignal))
					LSignal.Set();
			}
			#if USESPINLOCK
			finally
			{
				Interlocked.Decrement(ref FCacheSyncRoot);
			}
			#endif
		}
		
		protected ReaderWriterLock FCacheLock = new ReaderWriterLock();
		
		public const int CCacheLockTimeout = 10000;
		public const int CCacheSerializationTimeout = 30000; // Wait at most 30 seconds for a timestamp to be deserialized
		
		protected internal void AcquireCacheLock(IServerProcess AProcess, LockMode AMode)
		{
			try
			{
				if (AMode == LockMode.Exclusive)
					FCacheLock.AcquireWriterLock(CCacheLockTimeout);
				else
					FCacheLock.AcquireReaderLock(CCacheLockTimeout);
			}
			catch
			{
				throw new ServerException(ServerException.Codes.CacheLockTimeout);
			}
		}
		
		protected internal void ReleaseCacheLock(IServerProcess AProcess, LockMode AMode)
		{
			if (AMode == LockMode.Exclusive)
				FCacheLock.ReleaseWriterLock();
			else
				FCacheLock.ReleaseReaderLock();
		}

		public long CacheTimeStamp { get { return FServer.CacheTimeStamp; } }
		
		public long DerivationTimeStamp { get { return FServer.DerivationTimeStamp; } }
		
		public event CacheClearedEvent OnCacheClearing;
		private void DoCacheClearing()
		{
			if (OnCacheClearing != null)
				OnCacheClearing(this);
		}
		
		public event CacheClearedEvent OnCacheCleared;
		private void DoCacheCleared()
		{
			if (OnCacheCleared != null)
				OnCacheCleared(this);
		}
		
		public void EnsureCacheConsistent(long AServerCacheTimeStamp)
		{
			if (FServerCacheTimeStamp < AServerCacheTimeStamp)
			{
				DoCacheClearing();
				Schema.Catalog LCatalog = FInternalServer.Catalog;
				FInternalServer.ClearCatalog();
				foreach (Schema.RegisteredAssembly LAssembly in LCatalog.ClassLoader.Assemblies)
					if (LAssembly.Library.Name != Engine.CSystemLibraryName)
						FInternalServer.Catalog.ClassLoader.Assemblies.Add(LAssembly);
				foreach (Schema.RegisteredClass LClass in LCatalog.ClassLoader.Classes)
					if (!FInternalServer.Catalog.ClassLoader.Classes.Contains(LClass))
						FInternalServer.Catalog.ClassLoader.Classes.Add(LClass);
				foreach (ServerSession LSession in FInternalServer.Sessions)
				{
					if (LSession.User.ID == FInternalServer.SystemUser.ID)
						LSession.SetUser(FInternalServer.SystemUser);
					if (LSession.User.ID == FInternalServer.AdminUser.ID)
						LSession.SetUser(FInternalServer.AdminUser);
					LSession.SessionObjects.Clear();
				}
				FServerCacheTimeStamp = AServerCacheTimeStamp;
				DoCacheCleared();
			}
		}
		
		private List<string> FFilesCached = new List<string>();
		private List<string> FAssembliesCached = new List<string>();
		
		#if SILVERLIGHT

		public static Assembly LoadAssembyFromRemote(LocalProcess AProcess, string ALibraryName, string AFileName)
		{
			using (Stream LSourceStream = new RemoteStreamWrapper(AProcess.RemoteProcess.GetFile(ALibraryName, AFileName)))
			{
				var LAssembly = new System.Windows.AssemblyPart().Load(LSourceStream);
				ReflectionUtility.RegisterAssembly(LAssembly);
				return LAssembly;
			}
		}
		
		public void ClassLoaderMissed(LocalProcess AProcess, ClassLoader AClassLoader, ClassDefinition AClassDefinition)
		{
			AcquireCacheLock(AProcess, LockMode.Exclusive);
			try
			{
				if (!AClassLoader.Classes.Contains(AClassDefinition.ClassName))
				{
					// The local process has attempted to create an object from an unknown class alias.
					// Use the remote server to attempt to download and install the necessary assemblies.

					// Retrieve the list of all files required to load the assemblies required to load the class.
					ServerFileInfo[] LFileInfos = AProcess.RemoteProcess.GetFileNames(AClassDefinition.ClassName, AProcess.Session.SessionInfo.Environment);

					for (int LIndex = 0; LIndex < LFileInfos.Length; LIndex++)
						if (LFileInfos[LIndex].IsDotNetAssembly)
							LoadAndRegister(AProcess, AClassLoader, LFileInfos[LIndex].LibraryName, LFileInfos[LIndex].FileName, LFileInfos[LIndex].ShouldRegister);
				}
			}
			finally
			{
				ReleaseCacheLock(AProcess, LockMode.Exclusive);
			}
		}

		public void LoadAndRegister(LocalProcess AProcess, ClassLoader AClassLoader, string ALibraryName, string AFileName, bool AShouldRegister)
		{
			try
			{
				if (!FFilesCached.Contains(AFileName))
				{
					Assembly LAssembly = LoadAssembyFromRemote(AProcess, ALibraryName, AFileName);
					if (AShouldRegister && !AClassLoader.Assemblies.Contains(LAssembly.FullName))
						AClassLoader.RegisterAssembly(Catalog.LoadedLibraries[Engine.CSystemLibraryName], LAssembly);
					FAssembliesCached.Add(AFileName);
					FFilesCached.Add(AFileName);
				}
			}
			catch (IOException E)
			{
				FInternalServer.LogError(E);
			}
		}
		
		#else
		private string FLocalBinDirectory;
		private string LocalBinDirectory
		{
			get
			{
				if (FLocalBinDirectory == null)
				{
					FLocalBinDirectory = Path.Combine(Path.Combine(Alphora.Dataphor.Windows.PathUtility.GetBinDirectory(), "LocalBin"), FServer.Name);
					Directory.CreateDirectory(FLocalBinDirectory);
				}
				return FLocalBinDirectory;
			}
		}
		
		private string GetLocalFileName(string ALibraryName, string AFileName, bool AIsDotNetAssembly)
		{
			return Path.Combine(((ALibraryName == Engine.CSystemLibraryName) || !AIsDotNetAssembly) ? Alphora.Dataphor.Windows.PathUtility.GetBinDirectory() : LocalBinDirectory, Path.GetFileName(AFileName));
		}
		
		public string GetFile(LocalProcess AProcess, string ALibraryName, string AFileName, DateTime AFileDate, bool AIsDotNetAssembly, out bool AShouldLoad)
		{
			AShouldLoad = false;
			string LFullFileName = GetLocalFileName(ALibraryName, AFileName, AIsDotNetAssembly);

			if (!FFilesCached.Contains(AFileName))
			{
				if (!File.Exists(LFullFileName) || (File.GetLastWriteTimeUtc(LFullFileName) < AFileDate))
				{
					#if LOGFILECACHEEVENTS
					if (!File.Exists(LFullFileName))
						FInternalServer.LogMessage(String.Format(@"Downloading file ""{0}"" from server because it does not exist on the client.", LFullFileName));
					else
						FInternalServer.LogMessage(String.Format(@"Downloading newer version of file ""{0}"" from server. Client write time: ""{1}"". Server write time: ""{2}"".", LFullFileName, File.GetLastWriteTimeUtc(LFullFileName), AFileDate.ToString()));
					#endif
					using (Stream LSourceStream = new RemoteStreamWrapper(AProcess.RemoteProcess.GetFile(ALibraryName, AFileName)))
					{
						Alphora.Dataphor.Windows.FileUtility.EnsureWriteable(LFullFileName);
						try
						{
							using (FileStream LTargetStream = File.Open(LFullFileName, FileMode.Create, FileAccess.Write, FileShare.None))
							{
								StreamUtility.CopyStreamWithBufferSize(LSourceStream, LTargetStream, CFileCopyBufferSize);
							}
							
							File.SetLastWriteTimeUtc(LFullFileName, AFileDate);
						}
						catch (IOException E)
						{
							FInternalServer.LogError(E);
						}
					}
					
					#if LOGFILECACHEEVENTS
					FInternalServer.LogMessage("Download complete");
					#endif
				}
				
				FFilesCached.Add(AFileName);

				// Indicate that the assembly should be loaded
				if (AIsDotNetAssembly)
					AShouldLoad = true;
			}
			
			return LFullFileName;
		}
		
		public void ClassLoaderMissed(LocalProcess AProcess, ClassLoader AClassLoader, ClassDefinition AClassDefinition)
		{
			AcquireCacheLock(AProcess, LockMode.Exclusive);
			try
			{
				if (!AClassLoader.Classes.Contains(AClassDefinition.ClassName))
				{
					// The local process has attempted to create an object from an unknown class alias.
					// Use the remote server to attempt to download and install the necessary assemblies.
					//string AFullClassName = AProcess.RemoteProcess.GetClassName(AClassDefinition.ClassName); // BTR 5/17/2004 -> As far as I can tell, this doesn't do anything

					// Retrieve the list of all files required to load the assemblies required to load the class.
					ServerFileInfo[] LFileInfos = AProcess.RemoteProcess.GetFileNames(AClassDefinition.ClassName, AProcess.Session.SessionInfo.Environment);

					string[] LFullFileNames = new string[LFileInfos.Length];
					for (int LIndex = 0; LIndex < LFileInfos.Length; LIndex++)
					{
						bool LShouldLoad;
						string LFullFileName = GetFile(AProcess, LFileInfos[LIndex].LibraryName, LFileInfos[LIndex].FileName, LFileInfos[LIndex].FileDate, LFileInfos[LIndex].IsDotNetAssembly, out LShouldLoad);

						// Register/Load the assembly if necessary
						if ((LFileInfos[LIndex].ShouldRegister || LShouldLoad) && !FAssembliesCached.Contains(LFileInfos[LIndex].FileName))
						{
							Assembly LAssembly = Assembly.LoadFrom(LFullFileName);
							ReflectionUtility.RegisterAssembly(LAssembly);
							if (LFileInfos[LIndex].ShouldRegister && !AClassLoader.Assemblies.Contains(LAssembly.FullName))
								AClassLoader.RegisterAssembly(Catalog.LoadedLibraries[Engine.CSystemLibraryName], LAssembly);
							FAssembliesCached.Add(LFileInfos[LIndex].FileName);
						}
					}
				}
			}
			finally
			{
				ReleaseCacheLock(AProcess, LockMode.Exclusive);
			}
		}
		#endif

        /// <summary>Starts the server instance.  If it is already running, the call has no effect.</summary>
        public void Start()
        {
			FServer.Start();
		}
		
        /// <summary>Stops the server instance.  If it is not running, the call has no effect.</summary>
        public void Stop()
        {
			FServer.Stop();
        }

		public Schema.Catalog Catalog { get { return FInternalServer.Catalog; } }
		
		/// <value>Returns the state of the server. </value>
		public ServerState State { get { return FServer.State; } }
        
		/// <summary>
        ///     Connects to the server using the given parameters and returns an interface to the connection.
        ///     Will raise if the server is not running.
        /// </summary>
        /// <param name='ASessionInfo'>A <see cref="SessionInfo"/> object describing session configuration information for the connection request.</param>
        /// <returns>An <see cref="IServerSession"/> interface to the open session.</returns>
        public IServerSession Connect(SessionInfo ASessionInfo)
        {
			if ((ASessionInfo.HostName == null) || (ASessionInfo.HostName == ""))
				ASessionInfo.HostName = FHostName;
			ASessionInfo.CatalogCacheName = FInternalServer.Name;
			IRemoteServerSession LSession = FServerConnection.Connect(ASessionInfo);
			return new LocalSession(this, ASessionInfo, LSession);
        }
        
        /// <summary>
        ///     Disconnects an active session.  If the server is not running, an exception will be raised.
        ///     If the given session is not a valid session for this server, an exception will be raised.
        /// </summary>
        public void Disconnect(IServerSession ASession)
        {
			try
			{
				FServerConnection.Disconnect(((LocalSession)ASession).RemoteSession);
			}
			catch
			{
				// do nothing on an exception here
			}
			((LocalSession)ASession).Dispose();
        }
    }
}
