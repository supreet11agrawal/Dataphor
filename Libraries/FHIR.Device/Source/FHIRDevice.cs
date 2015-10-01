﻿/*
	Dataphor
	© Copyright 2000-2015 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Alphora.Dataphor;
using Alphora.Dataphor.DAE;
using Alphora.Dataphor.DAE.Compiling;
using Alphora.Dataphor.DAE.Device;
using Alphora.Dataphor.DAE.Language.D4;
using Alphora.Dataphor.DAE.Runtime;
using Alphora.Dataphor.DAE.Runtime.Data;
using Alphora.Dataphor.DAE.Runtime.Instructions;
using Alphora.Dataphor.DAE.Server;
using Alphora.Dataphor.FHIR.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Schema = Alphora.Dataphor.DAE.Schema;

namespace Aphora.Dataphor.FHIR.Device
{
	public class FHIRDevice : Schema.Device
	{
		public FHIRDevice(int iD, string name) : base(iD, name) 
		{ 
			_supportsTransactions = true;
			_supportsNestedTransactions = true;
		}

		public override Schema.DeviceCapability Capabilities
		{
			get
			{
				return 
					Schema.DeviceCapability.RowLevelInsert 
						| Schema.DeviceCapability.RowLevelUpdate 
						| Schema.DeviceCapability.RowLevelDelete;
			}
		}

		private string _endpoint;
		public string Endpoint
		{
			get { return _endpoint; }
			set { _endpoint = value; }
		}

		protected override Schema.DeviceSession InternalConnect(ServerProcess serverProcess, Schema.DeviceSessionInfo deviceSessionInfo)
		{
			return new FHIRDeviceSession(this, serverProcess, deviceSessionInfo);
		}

		protected override DevicePlanNode InternalPrepare(Schema.DevicePlan plan, PlanNode planNode)
		{
			// return a DevicePlanNode appropriate for execution of the given node
			BaseTableVarNode baseTableVarNode = planNode as BaseTableVarNode;
			if (baseTableVarNode != null)
			{
				var fhirTableNode = new FHIRDeviceTableNode(baseTableVarNode);
				fhirTableNode.ResourceType = MetaData.GetTag(baseTableVarNode.TableVar.MetaData, "FHIR.ResourceType", Schema.Object.Unqualify(baseTableVarNode.TableVar.Name));
				return fhirTableNode;
			}

			CreateTableNode createTableNode = planNode as CreateTableNode;
			if (createTableNode != null)
			{
				var fhirCreateTableNode = new FHIRCreateTableNode(createTableNode);
				return fhirCreateTableNode;
			}

			DropTableNode dropTableNode = planNode as DropTableNode;
			if (dropTableNode != null)
			{
				var fhirDropTableNode = new FHIRDropTableNode(dropTableNode);
				return fhirDropTableNode;
			}

			return null;
		}

		private string ToFHIRTableName(string fullTypeName)
		{
			return Schema.Object.Unqualify(fullTypeName) + "s"; // Pluralize the name of the resource to produce the table name
		}

		public override Schema.Catalog GetDeviceCatalog(ServerProcess process, Schema.Catalog serverCatalog, Schema.TableVar tableVar)
		{
			Schema.Catalog catalog = base.GetDeviceCatalog(process, serverCatalog, tableVar);

			using (Plan plan = new Plan(process))
			{

				// Need to support reverse lookup to determine the scalar type for a given native type

				Type[] types = typeof(Base).Assembly.GetTypes();

				foreach (Type type in types)
				{
					// create a table var for each DomainResource descendent
					if (type.IsSubclassOf(typeof(DomainResource)))
						if (!type.IsGenericTypeDefinition)
						{
							string tableName = Schema.Object.Qualify(ToFHIRTableName(type.Name), plan.CurrentLibrary.Name);
							if (tableVar == null || Schema.Object.NamesEqual(tableName, tableVar.Name))
							{
								Schema.BaseTableVar localTableVar = new Schema.BaseTableVar(tableName, null);
								localTableVar.Owner = plan.User;
								localTableVar.Library = plan.CurrentLibrary;
								localTableVar.Device = this;
								localTableVar.MetaData = new MetaData();
				
								// with a FHIR.ResourceType tag
								localTableVar.MetaData.Tags.Add(new Tag("FHIR.ResourceType", type.Name));
								localTableVar.AddDependency(this);
								localTableVar.DataType = new Schema.TableType();

								var d4TypeName = Schema.Object.Qualify(GenerateTypesNode.GetD4TypeName(type.FullName), "FHIR.Core");
								var d4Type = Compiler.ResolveCatalogIdentifier(plan, d4TypeName, false) as Schema.ScalarType;
								if (d4Type != null)
								{
									AddColumnsForType(localTableVar, d4Type);
								
									localTableVar.Keys.Add(new Schema.Key(new Schema.TableVarColumn[] { localTableVar.Columns["Id"] })); 

									catalog.Add(localTableVar);
								}
							}
						}
				}

				return catalog;
			}
		}

		private void AddColumnsForType(Schema.TableVar tableVar, Schema.ScalarType d4Type)
		{
			// Add columns for the base type, if any
			foreach (var parentType in d4Type.ParentTypes)
				AddColumnsForType(tableVar, parentType);

			// Add columns for the default representation
			var representation = Compiler.FindDefaultRepresentation(d4Type);

			// And columns for each property of the default representation
			foreach (var property in representation.Properties)
			{
				var column = new Schema.Column(property.Name, property.DataType);
				var tableVarColumn = new Schema.TableVarColumn(column);
				tableVar.DataType.Columns.Add(column);
				tableVar.Columns.Add(tableVarColumn);
			}

			tableVar.AddDependency(d4Type);
		}
	}

	public class FHIRDevicePlanNode : DevicePlanNode
	{
		public FHIRDevicePlanNode(PlanNode node) : base(node) { }
	}

	public class FHIRDropTableNode : FHIRDevicePlanNode
	{
		public FHIRDropTableNode(DropTableNode node) : base(node) { }

		public new DropTableNode Node { get { return (DropTableNode)base.Node; } }
	}

	public class FHIRCreateTableNode : FHIRDevicePlanNode
	{
		public FHIRCreateTableNode(CreateTableNode node) : base(node) { }

		public new CreateTableNode Node { get { return (CreateTableNode)base.Node; } }
	}

	public class FHIRDeviceTableNode : FHIRDevicePlanNode
	{
		public FHIRDeviceTableNode(TableNode node) : base(node) { }

		public new TableNode Node { get { return (TableNode)base.Node; } }

		private string _resourceType;
		public string ResourceType 
		{ 
			get { return _resourceType; } 
			set { _resourceType = value; }
		}

		private SearchParams _searchParams = new SearchParams();
		public SearchParams SearchParams { get { return _searchParams; } }
	}

	public class FHIRDeviceSession : Schema.DeviceSession
	{
		public FHIRDeviceSession(FHIRDevice device, ServerProcess serverProcess, Schema.DeviceSessionInfo deviceSessionInfo) : base(device, serverProcess, deviceSessionInfo)
		{
			// Initialize the FHIR Client
			_client = new FhirClient(device.Endpoint);
			_client.PreferredFormat = ResourceFormat.Json;

			// TODO: Establish authentication/authorization tokens?
		}

		private FhirClient _client;
		public FhirClient Client { get { return _client; } }

		protected override bool IsConnectionFailure(Exception exception)
		{
			return base.IsConnectionFailure(exception);
		}

		protected override bool IsTransactionFailure(Exception exception)
		{
			return base.IsTransactionFailure(exception);
		}

		protected override object InternalExecute(Program program, PlanNode planNode)
		{
            if (planNode.DeviceNode == null)
            {
                throw new DeviceException(DeviceException.Codes.UnpreparedDevicePlan, ErrorSeverity.System);
            }

			var fhirTableNode = planNode.DeviceNode as FHIRDeviceTableNode;
			if (fhirTableNode != null)
			{
				var fhirTable = new FHIRTable(this, program, fhirTableNode);
				fhirTable.Open();
				return fhirTable;
			}

			var fhirCreateTableNode = planNode.DeviceNode as FHIRCreateTableNode;
			if (fhirCreateTableNode != null)
			{
				return null; // TODO: Throw an error if reconilication is not none...
			}

			var fhirDropTableNode = planNode.DeviceNode as FHIRDropTableNode;
			if (fhirDropTableNode != null)
			{
				return null; // TODO: Throw an error if reconciliation is not none...
			}

			return base.InternalExecute(program, planNode);
		}
		
		protected override void InternalInsertRow(Program program, Schema.TableVar table, IRow row, BitArray valueFlags)
		{
			base.InternalInsertRow(program, table, row, valueFlags);
			// TODO: Log the operation so it can be performed during the commit
			// NOTE: This will require that the operation log be considered a "cache" that will be read locally prior to returning a result (or as part of post-processing a result?)
			// Can use a PUT if we manage Id generation, assuming the server supports it
			// Must use a POST to allow the server to assign the Id, in that case the Location header of the response will contain the newly assigned Id
		}

		protected override void InternalUpdateRow(Program program, Schema.TableVar table, IRow oldRow, IRow newRow, BitArray valueFlags)
		{
			// TODO: Log the operation so it can be performed during the commit
			base.InternalUpdateRow(program, table, oldRow, newRow, valueFlags);
			// PUT, w/ ETag header equal to oldRow.meta.version
		}

		protected override void InternalDeleteRow(Program program, Schema.TableVar table, IRow row)
		{
			// TODO: Log the operation so it can be performed during the commit
			base.InternalDeleteRow(program, table, row);
			// DELETE, w/ ETag header equal to row.meta.version
		}

		protected override void InternalBeginTransaction(IsolationLevel isolationLevel)
		{
			// Do nothing, the transaction is managed as a single request/response to the server
		}

		protected override void InternalPrepareTransaction()
		{
			// If this is the root transaction, invoke the operations for the current transaction against the server
		}

		protected override void InternalCommitTransaction()
		{
			// If no errors occurred during the prepare, the commit is a no-op.
		}

		protected override void InternalRollbackTransaction()
		{
			// Do nothing, the prepare is what actually performs the operations
		}
	}

	public class FHIRTable : Table
	{
		public FHIRTable(FHIRDeviceSession deviceSession, Program program, FHIRDeviceTableNode fhirTableNode) : base(fhirTableNode.Node, program)
		{
			_deviceSession = deviceSession;
			_fhirTableNode = fhirTableNode;
		}

		private FHIRDeviceSession _deviceSession;
		private FHIRDeviceTableNode _fhirTableNode;
		private Bundle _bundle;
		private List<Resource> _resources;
		private Type _fhirResourceType;
		private int _currentIndex;

		private void InitializeResourceType()
		{
			_fhirResourceType = _program.Catalog.ClassLoader.CreateType(_program.CatalogDeviceSession, new ClassDefinition(String.Format("Hl7.Fhir.Model.{0}", _fhirTableNode.ResourceType)));
		}

		private void OpenBundle(Bundle bundle)
		{
			_bundle = bundle;
			_resources = new List<Resource>();
			foreach (var entry in _bundle.Entry)
			{
				if (!entry.IsDeleted() && (entry.Search == null || entry.Search.Mode == Bundle.SearchEntryMode.Match) && entry.Resource.TypeName == _fhirTableNode.ResourceType)
				{
					_resources.Add(entry.Resource);
				}
			}
		}

		private bool FirstPage()
		{
			if (_bundle.FirstLink != null)
			{
				OpenBundle(_deviceSession.Client.Continue(_bundle, PageDirection.First));
			}
			else
			{
				// Same as initial open...
				OpenBundle(_deviceSession.Client.Search(_fhirTableNode.SearchParams, _fhirTableNode.ResourceType));
			}

			return true;
		}

		private bool LastPage()
		{
			if (_bundle.LastLink != null)
			{
				OpenBundle(_deviceSession.Client.Continue(_bundle, PageDirection.Last));
				return true;
			}

			return false;
		}

		private bool NextPage()
		{
			// TODO: How to tell when we hit the last page and we shouldn't be able to go next?
			// Working hypothesis: If there is a next link, we can go next...
			if (_bundle.NextLink != null)
			{
				OpenBundle(_deviceSession.Client.Continue(_bundle, PageDirection.Next));
				return true;
			}

			return false;
		}

		private bool PriorPage()
		{
			// TODO: How to tell when we hit the first page and we shouldn't be able to go prior?
			if (_bundle.PreviousLink != null)
			{
				OpenBundle(_deviceSession.Client.Continue(_bundle, PageDirection.Previous));
				return true;
			}

			return false;
		}

		private bool IsFirstPage()
		{
			// TODO: This is not a good way to do this...
			return (_bundle.FirstLink == _bundle.SelfLink);
		}

		private bool IsLastPage()
		{
			// TODO: This is not a good way to do this...
			return (_bundle.LastLink == _bundle.SelfLink);
		}

		protected override void InternalOpen()
		{
			InitializeResourceType();
			OpenBundle(_deviceSession.Client.Search(_fhirTableNode.SearchParams, _fhirTableNode.ResourceType));
			_currentIndex = -1; // Set to the BOF crack
		}

		protected override void InternalClose()
		{
			// Nothing to do
		}

		public override CursorCapability Capabilities
		{
			get
			{
				return CursorCapability.Navigable
					| CursorCapability.BackwardsNavigable;
			}
		}

		public override CursorType CursorType
		{
			get { return CursorType.Dynamic; }
		}

		protected override bool InternalRefresh(IRow row)
		{
			return base.InternalRefresh(row);
		}

		protected override void InternalSelect(IRow row)
		{
			// TODO: Will column order in this row always be guaranteed... if so we could avoid the lookup, but...
			// Return a row containing the values for the current resource in the bundle
			var resource = _resources[_currentIndex];
			for (int index = 0; index < row.DataType.Columns.Count; index++)
			{
				var value = ObjectMarshal.ToNativeOf(Program.ValueManager, row.DataType.Columns[index].DataType, _fhirResourceType.GetProperty(row.DataType.Columns[index].Name).GetValue(resource, null));
				row[index] = value;
			}
		}

		protected override bool InternalNext()
		{
			_currentIndex++;
			if (_currentIndex >= _resources.Count)
			{
				if (NextPage())
					_currentIndex = 0;
			}
			return _currentIndex < _resources.Count;
		}

		protected override bool InternalPrior()
		{
			_currentIndex--;
			if (_currentIndex < 0)
			{
				if (PriorPage())
					_currentIndex = _resources.Count;
			}
			return _currentIndex < 0;
		}

		protected override bool InternalBOF()
		{
			return IsFirstPage() && _currentIndex < 0;
		}

		protected override bool InternalEOF()
		{
			return IsLastPage() && _currentIndex >= _resources.Count;
		}

		protected override void InternalFirst()
		{
			FirstPage();
			_currentIndex = -1;
		}

		protected override void InternalLast()
		{
			LastPage();
			_currentIndex = _resources.Count;
		}
	}
}
