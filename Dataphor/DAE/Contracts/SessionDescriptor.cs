﻿/*
	Alphora Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Alphora.Dataphor.DAE.Contracts
{
	[DataContract]
	public class SessionDescriptor
	{
		public SessionDescriptor(int handle, long iD)
		{
			_handle = handle;
			_iD = iD;
		}
		
		[DataMember]
		internal int _handle;
		public int Handle { get { return _handle; } }

		[DataMember]
		internal long _iD;
		public long ID { get { return _iD; } }
	}
}
