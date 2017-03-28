//-----------------------------------------------------------------------
// <copyright file="JewelryTypeExchangeTask.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Transactions;
using Ipk.Custom.MPR.Model;
using Ipk.Custom.MPR.Model.Models;
using Ipk.Custom.MPR.Repository.Base;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// Descripts rules for updating JewelryType table
    /// </summary>
    public class JewelryTypeExchangeTask : BaseExchangeTask, IExchangeTask
    {
        private DataTable _dataTable;
        private SqlConnection _argoConnection;
        private ExchangeEntity _exchangeEntity;

        private byte[] _lastStamp;

        /// <summary>
        /// Ctor
        /// </summary>
        public JewelryTypeExchangeTask()
        {
            _lastStamp = new byte[8];
            _dataTable = new DataTable();
        }

        /// <summary>
        /// Entity name
        /// </summary>
        public string EntityName
        {
            get { return "JewelryType"; }
        }

        /// <summary>
        /// Exchange entity
        /// </summary>
        public override ExchangeEntity ExchangeEntity
        {
            get
            {
                return _exchangeEntity;
            }
        }
        
        /// <summary>
        /// Method for loading data from source
        /// </summary>
        /// <param name="argoConnection">Source's connetion string</param>
        /// <param name="exchangeEntity">Object of exchange with historic info (last timestamp)</param>
        public void PrepareArgoData(System.Data.SqlClient.SqlConnection argoConnection,
                                    Model.Models.ExchangeEntity exchangeEntity)
        {
            try
            {
                _argoConnection = argoConnection;
                _exchangeEntity = exchangeEntity;

                _lastStamp = exchangeEntity.LastState;

                PublishEventLog(ExchangeStatusType.Unknown, "Запрос данных \"Виды ЮИ\" из АРГО", null);

                if (_argoConnection.State != ConnectionState.Open)
                    _argoConnection.Open();

                string cmdText = @"SELECT  [UID]
                                      ,[Name]
                                      ,[ShortName]
                                      ,[IsDeleted]
                                      ,[State]
                               FROM [dbo].[JewelryType]
                               WHERE [State] > @State";

                SqlCommand command = new SqlCommand(cmdText, argoConnection);
                command.Parameters.AddWithValue("@State", _exchangeEntity.LastState);

                SqlDataAdapter adapter = new SqlDataAdapter(command);

                _dataTable.Clear();

                adapter.Fill(_dataTable);

                PublishEventLog(ExchangeStatusType.Unknown,
                                string.Format("Подготовлено {0} записей для обновления", _dataTable.Rows.Count), null);
            }
            catch (Exception ex)
            {
                Log.Error("JewelryTypeExchangeTask. PrepareArgoData Method Error. ", ex);
                throw;
            }
        }

        /// <summary>
        /// Method for saving data to destination
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        public void ApplyChanges(UnitOfWork unitOfWork)
        {
            try
            {
                var repository = unitOfWork.JewelryTypeRepository;
                var jewelrytypes = repository.GetFullList();
                foreach (DataRow row in _dataTable.Rows)
                {
                    byte[] lastStamp = (byte[]) row["State"];
                    if (new SqlBinary(lastStamp) > new SqlBinary(_lastStamp))
                        _lastStamp = lastStamp;

                    var type = NewJewelryType(row);

                    var existType = jewelrytypes.FirstOrDefault(x => x.UID == type.UID);

                    if (existType != null)
                    {
                        using (var t = new TransactionScope())
                        {
                            existType.IsDeleted = type.IsDeleted;
                            existType.Name = type.Name;
                            existType.ShortName = type.ShortName;

                            repository.Update(existType);
                            repository.Save();

                            t.Complete();
                        }
                        PublishEventLog(ExchangeStatusType.Update, "Успешное обновление", null);
                    }
                    else
                    {
                        repository.Add(type);
                        repository.Save();
                        PublishEventLog(ExchangeStatusType.Insert, "Успешная вставка", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("JewelryTypeExchangeTask. ApplyChanges Method Error. ", ex);
                throw;
            }
        }

        /// <summary>
        /// Method for creating new JewelryType from DataRow 
        /// </summary>
        /// <param name="row">Row contains data</param>
        /// <returns>The new JewelryType object</returns>
        private JewelryType NewJewelryType (DataRow row)
        {
            JewelryType type = new JewelryType();
            type.UID = Guid.Parse(row["UID"].ToString());
            type.IsDeleted = Convert.ToBoolean(row["IsDeleted"]);
            type.Name = row["Name"].ToString();
            if (!row.IsNull("ShortName"))
                type.ShortName = row["ShortName"].ToString();
            return type;
        }

        /// <summary>
        /// Gets maximum timestamp for preparing query
        /// </summary>
        /// <returns>Timestamp</returns>
        public byte[] GetMaxTimeStamp()
        {
            return _lastStamp;
        }
    }
}
