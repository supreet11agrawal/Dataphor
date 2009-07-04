using System;
using System.Data;
using System.Data.SqlClient;

namespace Alphora.Dataphor.DAE.Connection
{
    public class MSSQLCommand : DotNetCommand
    {
        public MSSQLCommand(MSSQLConnection AConnection, IDbCommand ACommand)
            : base(AConnection, ACommand) 
        {
            FUseOrdinalBinding = true;
        }
		
        protected override void PrepareParameters()
        {
            // Prepare parameters
            SQLParameter LParameter;
            for (int LIndex = 0; LIndex < FParameterIndexes.Length; LIndex++)
            {
                LParameter = Parameters[FParameterIndexes[LIndex]];
                SqlParameter LSQLCEParameter = (SqlParameter)FCommand.CreateParameter();
                LSQLCEParameter.ParameterName = String.Format("@{0}", LParameter.Name);
                switch (LParameter.Direction)
                {
                    case SQLDirection.Out : LSQLCEParameter.Direction = System.Data.ParameterDirection.Output; break;
                    case SQLDirection.InOut : LSQLCEParameter.Direction = System.Data.ParameterDirection.InputOutput; break;
                    case SQLDirection.Result : LSQLCEParameter.Direction = System.Data.ParameterDirection.ReturnValue; break;
                    default : LSQLCEParameter.Direction = System.Data.ParameterDirection.Input; break;
                }

                if (LParameter.Type is SQLStringType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.NVarChar;
                    LSQLCEParameter.Size = ((SQLStringType)LParameter.Type).Length;
                }
                else if (LParameter.Type is SQLBooleanType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.Bit;
                }
                else if (LParameter.Type is SQLByteArrayType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.Binary;
                    LSQLCEParameter.Size = ((SQLByteArrayType)LParameter.Type).Length;
                }
                else if (LParameter.Type is SQLIntegerType)
                {
                    switch (((SQLIntegerType)LParameter.Type).ByteCount)
                    {
                        case 1 : LSQLCEParameter.SqlDbType = SqlDbType.TinyInt; break;
                        case 2 : LSQLCEParameter.SqlDbType = SqlDbType.SmallInt; break;
                        case 8 : LSQLCEParameter.SqlDbType = SqlDbType.BigInt; break;
                        default : LSQLCEParameter.SqlDbType = SqlDbType.Int; break;
                    }
                }
                else if (LParameter.Type is SQLNumericType)
                {
                    SQLNumericType LType = (SQLNumericType)LParameter.Type;
                    LSQLCEParameter.SqlDbType = SqlDbType.Decimal;
                    LSQLCEParameter.Scale = LType.Scale;
                    LSQLCEParameter.Precision = LType.Precision;
                }
                else if (LParameter.Type is SQLFloatType)
                {
                    SQLFloatType LType = (SQLFloatType)LParameter.Type;
                    if (LType.Width == 1)
                        LSQLCEParameter.SqlDbType = SqlDbType.Real;
                    else
                        LSQLCEParameter.SqlDbType = SqlDbType.Float;
                }
                else if (LParameter.Type is SQLBinaryType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.Image;
                }
                else if (LParameter.Type is SQLTextType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.NText;
                }
                else if (LParameter.Type is SQLDateType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.DateTime;
                }
                else if (LParameter.Type is SQLTimeType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.DateTime;
                }
                else if (LParameter.Type is SQLDateTimeType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.DateTime;
                }
                else if (LParameter.Type is SQLGuidType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.UniqueIdentifier;
                }
                else if (LParameter.Type is SQLMoneyType)
                {
                    LSQLCEParameter.SqlDbType = SqlDbType.Money;
                }
                else
                    throw new ConnectionException(ConnectionException.Codes.UnknownSQLDataType, LParameter.Type.GetType().Name);
                FCommand.Parameters.Add(LSQLCEParameter);
            }
        }
    }
}