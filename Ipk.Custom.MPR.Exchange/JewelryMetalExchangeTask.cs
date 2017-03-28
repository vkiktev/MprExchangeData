//-----------------------------------------------------------------------
// <copyright file="JewelryMetalExchangeTask.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Linq;
using System.Transactions;
using Ipk.Custom.MPR.Model;
using Ipk.Custom.MPR.Model.Models;
using Ipk.Custom.MPR.Repository.Base;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// Descripts rules for updating JewelryMetal table 
    /// </summary>
    public class JewelryMetalExchangeTask : BaseExchangeTask, IExchangeTask
    {
        private DataTable _dataTable;
        private SqlConnection _argoConnection;
        private ExchangeEntity _exchangeEntity;

        private byte[] _lastStamp;

        /// <summary>
        /// Ctor
        /// </summary>
        public JewelryMetalExchangeTask()
        {
            _lastStamp = new byte[8];
            _dataTable = new DataTable();
        }

        /// <summary>
        /// Entity name
        /// </summary>
        public string EntityName
        {
            get { return "JewelryMetal"; }
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
        /// Method for loading data from the source
        /// </summary>
        /// <param name="argoConnection">Source side connection string</param>
        /// <param name="exchangeEntity">Object of exchange with historic info (last timestamp)</param>
        public void PrepareArgoData(System.Data.SqlClient.SqlConnection argoConnection, Model.Models.ExchangeEntity exchangeEntity)
        {
            try
            {
                _argoConnection = argoConnection;
                _exchangeEntity = exchangeEntity;

                _lastStamp = exchangeEntity.LastState;

                PublishEventLog(ExchangeStatusType.Unknown, "Запрос данных \"Металлы\" из АРГО", null);

                if (_argoConnection.State != ConnectionState.Open)
                    _argoConnection.Open();

                string cmdText = @"SELECT  [UID]
                                      ,[Name]
                                      ,[ShortName]
                                      ,[IsDeleted]
                                      ,[State]
                               FROM [dbo].[JewelryMetal]
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
                Log.Error("JewelryMetalExchangeTask. PrepareArgoData Method Error. ", ex);
                throw;
            }
        }

        /// <summary>
        /// Method for creating new JewelryMetal from DataRow 
        /// </summary>
        /// <param name="row">Row contains data</param>
        /// <returns>The new JewelryMetal object</returns>
        private JewelryMetal NewJewelryMetal(DataRow row)
        {
            JewelryMetal metal = new JewelryMetal();
            metal.UID = Guid.Parse(row["UID"].ToString());
            metal.IsDeleted = Convert.ToBoolean(row["IsDeleted"]);
            metal.Name = row["Name"].ToString();
            if (!row.IsNull("ShortName"))
                metal.ShortName = row["ShortName"].ToString();

            return metal;
        }

        /// <summary>
        /// Method for saving data to destination
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        public void ApplyChanges(UnitOfWork unitOfWork)
        {
            try
            {
                var repository = unitOfWork.JewelryMetalRepository;
                var jewelrymetals = repository.GetFullList();
                foreach (DataRow row in _dataTable.Rows)
                {
                    byte[] lastStamp = (byte[]) row["State"];

                    if (new SqlBinary(lastStamp) > new SqlBinary(_lastStamp))
                        _lastStamp = lastStamp;

                    var metal = NewJewelryMetal(row);

                    var existMetal = jewelrymetals.FirstOrDefault(x => x.UID == metal.UID);

                    if (existMetal != null)
                    {
                        using (var t = new TransactionScope())
                        {
                            existMetal.IsDeleted = metal.IsDeleted;
                            existMetal.Name = metal.Name;
                            existMetal.ShortName = metal.ShortName;

                            repository.Update(existMetal);
                            repository.Save();

                            t.Complete();
                        }
                        PublishEventLog(ExchangeStatusType.Update, "Успешное обновление", null);
                    }
                    else
                    {
                        repository.Add(metal);
                        repository.Save();
                        PublishEventLog(ExchangeStatusType.Insert, "Успешная вставка", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("JewelryMetalExchangeTask. ApplyChanges Method Error. ", ex);
                throw;
            }
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
